using System;
using System.Collections.Generic;
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
    private const string UserProfileEndpoint = "api/Auth/GetUserProfileInfo";
    private const string ProjectListEndpoint = "api/Project/GetAll";
    private const string TimeEntryEndpoint = "api/TimeSheet/Create";
    private const string TimeEntryLookupEndpoint = "api/TimeSheet/GetByUserId";

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
        var responseBody = await EnsureSuccessAndReadContentAsync(response, "login to the Rea portal", cancellationToken).ConfigureAwait(false);
        _accessToken = ExtractToken(responseBody) ?? throw new InvalidOperationException("Rea portal login response did not include an access token.");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    public void Logout()
    {
        _accessToken = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<ReaUserProfile> GetUserProfileAsync(CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        using var response = await _httpClient.GetAsync(UserProfileEndpoint, cancellationToken).ConfigureAwait(false);
        var responseBody = await EnsureSuccessAndReadContentAsync(response, "retrieve the Rea user profile", cancellationToken).ConfigureAwait(false);

        using var jsonDocument = JsonDocument.Parse(responseBody);
        var dataElement = ExtractDataElement(jsonDocument.RootElement);

        var userId = FindString(dataElement, "userId", "id")
            ?? throw new InvalidOperationException("Rea portal user profile response did not include a user identifier.");
        var name = FindString(dataElement, "name", "fullName", "displayName");

        return new ReaUserProfile(userId, name);
    }

    public async Task<IReadOnlyList<ReaProject>> GetProjectsAsync(string? userId = null, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var requestUri = string.IsNullOrWhiteSpace(userId)
            ? ProjectListEndpoint
            : $"{ProjectListEndpoint}?userId={Uri.EscapeDataString(userId)}";
        using var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        var responseBody = await EnsureSuccessAndReadContentAsync(response, "retrieve the Rea project list", cancellationToken).ConfigureAwait(false);

        var projects = new List<ReaProject>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!TryParseProjectsFromEnvelope(responseBody, projects, seenIds))
        {
            using var jsonDocument = JsonDocument.Parse(responseBody);
            var dataElement = ExtractDataElement(jsonDocument.RootElement);

            foreach (var item in EnumerateProjectObjects(dataElement))
            {
                var id = FindString(item, "projectId", "id");
                if (string.IsNullOrWhiteSpace(id) || !seenIds.Add(id))
                {
                    continue;
                }

                var name = FindString(item, "projectName", "name", "title") ?? id;
                var code = FindString(item, "projectCode", "code", "shortName", "key", "projectKey");

                projects.Add(new ReaProject(id, name, code));
            }
        }

        return projects;
    }

    public async Task CreateTimeEntryAsync(ReaTimeEntry entry, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var payload = JsonSerializer.Serialize(entry, _serializerOptions);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(TimeEntryEndpoint, content, cancellationToken).ConfigureAwait(false);
        _ = await EnsureSuccessAndReadContentAsync(response, "create the Rea time entry", cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ReaTimeEntry>> GetTimeEntriesAsync(string userId, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User identifier is required to retrieve Rea time entries.", nameof(userId));
        }

        var requestUri = $"{TimeEntryLookupEndpoint}?userId={Uri.EscapeDataString(userId)}";

        using var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        var responseBody = await EnsureSuccessAndReadContentAsync(response, "retrieve the Rea time entries", cancellationToken).ConfigureAwait(false);

        if (TryDeserializeTimeEntries(responseBody) is { } entries)
        {
            return entries;
        }

        using var jsonDocument = JsonDocument.Parse(responseBody);
        var dataElement = ExtractDataElement(jsonDocument.RootElement);

        if (TryDeserializeTimeEntries(dataElement) is { } fallbackEntries)
        {
            return fallbackEntries;
        }

        return Array.Empty<ReaTimeEntry>();
    }

    private static string? ExtractToken(string responseBody)
    {
        using var jsonDocument = JsonDocument.Parse(responseBody);
        var root = jsonDocument.RootElement;

        if (TryGetToken(root, out var token))
        {
            return token;
        }

        if (root.TryGetProperty("data", out var dataElement))
        {
            if (dataElement.ValueKind == JsonValueKind.String)
            {
                if (TryParseStringAsJson(dataElement.GetString(), out var parsedData) && TryGetToken(parsedData, out token))
                {
                    return token;
                }

                return dataElement.GetString();
            }

            if (TryGetToken(dataElement, out token))
            {
                return token;
            }
        }

        return null;
    }

    private static bool TryGetToken(JsonElement element, out string? token)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (IsPropertyName(property.Name, "token") || IsPropertyName(property.Name, "accessToken"))
                {
                    if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        token = property.Value.GetString();
                        return !string.IsNullOrWhiteSpace(token);
                    }
                }
            }
        }

        token = null;
        return false;
    }

    private static bool TryParseStringAsJson(string? value, out JsonElement element)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            try
            {
                using var document = JsonDocument.Parse(value);
                element = document.RootElement.Clone();
                return true;
            }
            catch (JsonException)
            {
                // Ignore and fall through to return false below.
            }
        }

        element = default;
        return false;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private void EnsureAuthenticated()
    {
        if (!IsAuthenticated)
        {
            throw new InvalidOperationException("Login to the Rea portal before performing this operation.");
        }
    }

    private static JsonElement ExtractDataElement(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.String)
        {
            var stringValue = root.GetString();
            if (!string.IsNullOrWhiteSpace(stringValue))
            {
                try
                {
                    using var parsed = JsonDocument.Parse(stringValue);
                    return parsed.RootElement.Clone();
                }
                catch (JsonException)
                {
                    return root;
                }
            }
        }

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind != JsonValueKind.Null && dataElement.ValueKind != JsonValueKind.Undefined)
        {
            if (dataElement.ValueKind == JsonValueKind.String)
            {
                var dataString = dataElement.GetString();
                if (!string.IsNullOrWhiteSpace(dataString))
                {
                    try
                    {
                        using var parsed = JsonDocument.Parse(dataString);
                        return parsed.RootElement.Clone();
                    }
                    catch (JsonException)
                    {
                        return dataElement;
                    }
                }
            }

            return dataElement;
        }

        return root;
    }

    private static IEnumerable<JsonElement> EnumerateProjectObjects(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (ContainsProjectSignature(element))
                {
                    yield return element;
                }

                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String &&
                        TryParseJsonElement(property.Value.GetString(), out var stringElement))
                    {
                        foreach (var nested in EnumerateProjectObjects(stringElement))
                        {
                            yield return nested;
                        }

                        continue;
                    }

                    foreach (var nested in EnumerateProjectObjects(property.Value))
                    {
                        yield return nested;
                    }
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String &&
                        TryParseJsonElement(item.GetString(), out var arrayItemElement))
                    {
                        foreach (var nested in EnumerateProjectObjects(arrayItemElement))
                        {
                            yield return nested;
                        }

                        continue;
                    }

                    foreach (var nested in EnumerateProjectObjects(item))
                    {
                        yield return nested;
                    }
                }

                break;
        }
    }

    private static bool ContainsProjectSignature(JsonElement element)
    {
        var hasProjectId = false;
        var hasGenericId = false;
        var hasName = false;

        foreach (var property in element.EnumerateObject())
        {
            if (IsPropertyName(property.Name, "projectId") || IsPropertyName(property.Name, "project_id") || IsPropertyName(property.Name, "projectID") || IsPropertyName(property.Name, "projectid"))
            {
                hasProjectId = true;
            }

            if (IsPropertyName(property.Name, "id") && (property.Value.ValueKind == JsonValueKind.String || property.Value.ValueKind == JsonValueKind.Number))
            {
                hasGenericId = true;
            }

            if (IsPropertyName(property.Name, "projectName") || IsPropertyName(property.Name, "name") || IsPropertyName(property.Name, "title"))
            {
                hasName = true;
            }
        }

        return hasProjectId || (hasGenericId && hasName);
    }

    private static string? FindString(JsonElement element, params string[] propertyNames)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                foreach (var target in propertyNames)
                {
                    if (IsPropertyName(property.Name, target))
                    {
                        var value = ConvertToNonEmptyString(property.Value);
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            return value;
                        }
                    }
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                var nested = FindString(property.Value, propertyNames);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindString(item, propertyNames);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private bool TryParseProjectsFromEnvelope(string responseBody, List<ReaProject> projects, HashSet<string> seenIds)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<ReaApiResponse<List<ReaProjectPayload>>>(responseBody, _serializerOptions);
            if (envelope?.Data is { Count: > 0 } payloads)
            {
                foreach (var payload in payloads)
                {
                    var id = FirstNonEmpty(payload.ProjectId, payload.Id);
                    if (string.IsNullOrWhiteSpace(id) || !seenIds.Add(id))
                    {
                        continue;
                    }

                    var name = FirstNonEmpty(payload.ProjectName, payload.Name, payload.Title) ?? id;
                    var code = FirstNonEmpty(payload.ProjectCode, payload.Code, payload.ShortName, payload.Key, payload.ProjectKey);

                    projects.Add(new ReaProject(id, name, code));
                }

                return projects.Count > 0;
            }
        }
        catch (JsonException)
        {
            // Ignore and fall back to the resilient JSON element parsing logic below.
        }

        return false;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string? ConvertToNonEmptyString(JsonElement element)
    {
        string? value = element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private List<ReaTimeEntry>? TryDeserializeTimeEntries(string responseBody)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<ReaApiResponse<List<ReaTimeEntry>>>(responseBody, _serializerOptions);
            if (envelope?.Data is { Count: > 0 } data)
            {
                return data;
            }
        }
        catch (JsonException)
        {
            // Ignore and fall back to other parsing mechanisms.
        }

        try
        {
            var direct = JsonSerializer.Deserialize<List<ReaTimeEntry>>(responseBody, _serializerOptions);
            if (direct is { Count: > 0 })
            {
                return direct;
            }
        }
        catch (JsonException)
        {
            // Ignore and fall back to resilient parsing below.
        }

        return null;
    }

    private List<ReaTimeEntry>? TryDeserializeTimeEntries(JsonElement element)
    {
        try
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                var items = JsonSerializer.Deserialize<List<ReaTimeEntry>>(element.GetRawText(), _serializerOptions);
                if (items is { Count: > 0 })
                {
                    return items;
                }
            }
            else if (element.ValueKind == JsonValueKind.Object)
            {
                var item = JsonSerializer.Deserialize<ReaTimeEntry>(element.GetRawText(), _serializerOptions);
                if (item is not null)
                {
                    return new List<ReaTimeEntry> { item };
                }
            }
            else if (element.ValueKind == JsonValueKind.String && TryParseJsonElement(element.GetString(), out var parsed))
            {
                return TryDeserializeTimeEntries(parsed);
            }
        }
        catch (JsonException)
        {
            // Ignore and fall back to return null below.
        }

        return null;
    }

    private static bool TryParseJsonElement(string? candidate, out JsonElement element)
    {
        element = default;

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(candidate);
            element = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsPropertyName(string actual, string expected)
    {
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> EnsureSuccessAndReadContentAsync(HttpResponseMessage response, string action, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new InvalidOperationException($"Failed to {action}: {(int)response.StatusCode} {response.ReasonPhrase}. {errorBody}");
    }
}
