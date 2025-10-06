using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JiraToRea.App.Models;

namespace JiraToRea.App.Services;

public sealed class ReaApiClient : IDisposable
{
    private const string LoginEndpoint = "api/Auth/Login";
    private const string TimeEntryEndpoint = "api/TimeSheet/Create";

    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
    private string? _accessToken;

    public ReaApiClient()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://portalapi.reatech.uk/")
        };
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);

    public async Task LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var loginRequest = new ReaLoginRequest
        {
            UserName = username,
            Password = password
        };

        var payload = JsonSerializer.Serialize(loginRequest, _serializerOptions);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(LoginEndpoint, content, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Rea portal login failed: {(int)response.StatusCode} {response.ReasonPhrase}. {errorBody}");
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        _accessToken = ExtractToken(responseBody) ?? throw new InvalidOperationException("Rea portal login response did not include an access token.");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    public void Logout()
    {
        _accessToken = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public async Task CreateTimeEntryAsync(ReaTimeEntry entry, CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new InvalidOperationException("Login to the Rea portal before creating time entries.");
        }

        var payload = JsonSerializer.Serialize(entry, _serializerOptions);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(TimeEntryEndpoint, content, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Failed to create Rea time entry: {(int)response.StatusCode} {response.ReasonPhrase}. {errorBody}");
        }
    }

    private static string? ExtractToken(string responseBody)
    {
        using var jsonDocument = JsonDocument.Parse(responseBody);
        var root = jsonDocument.RootElement;
        if (root.TryGetProperty("token", out var tokenElement) && tokenElement.ValueKind == JsonValueKind.String)
        {
            return tokenElement.GetString();
        }

        if (root.TryGetProperty("data", out var dataElement))
        {
            if (dataElement.ValueKind == JsonValueKind.String)
            {
                return dataElement.GetString();
            }

            if (dataElement.ValueKind == JsonValueKind.Object && dataElement.TryGetProperty("token", out var nestedToken) && nestedToken.ValueKind == JsonValueKind.String)
            {
                return nestedToken.GetString();
            }
        }

        return null;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
