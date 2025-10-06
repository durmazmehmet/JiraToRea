using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JiraToRea.App.Models;

namespace JiraToRea.App.Services;

public sealed class JiraApiClient : IDisposable
{
    private const string BaseAddress = "https://borusanotomotiv.atlassian.net/";

    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
    private string? _accountId;
    private bool _isAuthenticated;

    public JiraApiClient()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseAddress)
        };
    }

    public bool IsAuthenticated => _isAuthenticated;

    public string? DisplayName { get; private set; }

    public async Task LoginAsync(string email, string apiToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("E-posta adresi zorunludur.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(apiToken))
        {
            throw new ArgumentException("API token zorunludur.", nameof(apiToken));
        }

        SetAuthenticationHeader(email, apiToken);

        using var response = await _httpClient.GetAsync("rest/api/3/myself", cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Jira oturumu açılamadı: {(int)response.StatusCode} {response.ReasonPhrase}. {errorBody}");
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var myself = JsonSerializer.Deserialize<JiraMyselfResponse>(responseBody, _serializerOptions) ?? throw new InvalidOperationException("Jira'dan kullanıcı bilgileri alınamadı.");
        _accountId = myself.AccountId;
        DisplayName = myself.DisplayName;
        _isAuthenticated = true;
    }

    public void Logout()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        _accountId = null;
        _isAuthenticated = false;
        DisplayName = null;
    }

    public async Task<IReadOnlyList<WorklogEntryViewModel>> GetWorklogsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        if (!_isAuthenticated || string.IsNullOrEmpty(_accountId))
        {
            throw new InvalidOperationException("Worklogları almadan önce Jira'ya giriş yapın.");
        }

        if (endDate < startDate)
        {
            throw new ArgumentException("Bitiş tarihi başlangıç tarihinden küçük olamaz.", nameof(endDate));
        }

        var jql = BuildJql(startDate, endDate);
        var requestUri = $"rest/api/3/search?jql={Uri.EscapeDataString(jql)}&fields=summary&maxResults=200";
        using var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Jira worklog araması başarısız: {(int)response.StatusCode} {response.ReasonPhrase}. {errorBody}");
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var searchResult = JsonSerializer.Deserialize<JiraSearchResponse>(responseBody, _serializerOptions) ?? new JiraSearchResponse();

        var results = new List<WorklogEntryViewModel>();
        foreach (var issue in searchResult.Issues)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var worklogs = await GetIssueWorklogsAsync(issue.Key, cancellationToken).ConfigureAwait(false);
            foreach (var worklog in worklogs)
            {
                if (!string.Equals(worklog.Author.AccountId, _accountId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!DateTime.TryParse(worklog.Started, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var startedUtc))
                {
                    continue;
                }

                var localStart = startedUtc.ToLocalTime();
                if (localStart.Date < startDate.Date || localStart.Date > endDate.Date)
                {
                    continue;
                }

                var duration = TimeSpan.FromSeconds(worklog.TimeSpentSeconds);
                results.Add(new WorklogEntryViewModel
                {
                    IssueKey = issue.Key,
                    IssueSummary = issue.Fields?.Summary ?? string.Empty,
                    Task = $"{issue.Key} - {issue.Fields?.Summary}",
                    StartDate = localStart,
                    EndDate = localStart + duration,
                    EffortHours = Math.Round(duration.TotalHours, 2, MidpointRounding.AwayFromZero),
                    Comment = ExtractCommentText(worklog.Comment) ?? (issue.Fields?.Summary ?? string.Empty),
                    JiraWorklogId = worklog.Id
                });
            }
        }

        return results;
    }

    private async Task<IReadOnlyList<JiraWorklog>> GetIssueWorklogsAsync(string issueKey, CancellationToken cancellationToken)
    {
        var requestUri = $"rest/api/3/issue/{issueKey}/worklog?startAt=0&maxResults=1000";
        using var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"{issueKey} işinin worklog kayıtları alınamadı: {(int)response.StatusCode} {response.ReasonPhrase}. {errorBody}");
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var worklogResponse = JsonSerializer.Deserialize<JiraIssueWorklogResponse>(responseBody, _serializerOptions);
        return worklogResponse?.Worklogs ?? Array.Empty<JiraWorklog>();
    }

    private static string BuildJql(DateTime startDate, DateTime endDate)
    {
        var start = startDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
        var end = endDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
        return $"worklogAuthor = currentUser() AND worklogDate >= \"{start}\" AND worklogDate <= \"{end}\"";
    }

    private static string? ExtractCommentText(JiraWorklogComment? comment)
    {
        if (comment == null)
        {
            return null;
        }

        var builder = new StringBuilder();
        AppendCommentNodes(comment.Content, builder);
        return builder.Length == 0 ? null : builder.ToString().Trim();
    }

    private static void AppendCommentNodes(IEnumerable<JiraCommentNode> nodes, StringBuilder builder)
    {
        foreach (var node in nodes)
        {
            if (!string.IsNullOrWhiteSpace(node.Text))
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(node.Text);
            }

            if (node.Children != null && node.Children.Count > 0)
            {
                AppendCommentNodes(node.Children, builder);
            }
        }
    }

    private void SetAuthenticationHeader(string email, string apiToken)
    {
        var raw = Encoding.UTF8.GetBytes($"{email}:{apiToken}");
        var encoded = Convert.ToBase64String(raw);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encoded);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
