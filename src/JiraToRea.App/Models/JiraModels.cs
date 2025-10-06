using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JiraToRea.App.Models;

public sealed class JiraMyselfResponse
{
    [JsonPropertyName("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class JiraSearchResponse
{
    [JsonPropertyName("issues")]
    public List<JiraIssue> Issues { get; set; } = new();
}

public sealed class JiraIssue
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public JiraIssueFields Fields { get; set; } = new();
}

public sealed class JiraIssueFields
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
}

public sealed class JiraIssueWorklogResponse
{
    [JsonPropertyName("worklogs")]
    public List<JiraWorklog> Worklogs { get; set; } = new();
}

public sealed class JiraWorklog
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public JiraWorklogAuthor Author { get; set; } = new();

    [JsonPropertyName("started")]
    public string Started { get; set; } = string.Empty;

    [JsonPropertyName("timeSpentSeconds")]
    public int TimeSpentSeconds { get; set; }

    [JsonPropertyName("comment")]
    public JiraWorklogComment? Comment { get; set; }
}

public sealed class JiraWorklogAuthor
{
    [JsonPropertyName("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class JiraWorklogComment
{
    [JsonPropertyName("content")]
    public List<JiraCommentNode> Content { get; set; } = new();
}

public sealed class JiraCommentNode
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("content")]
    public List<JiraCommentNode> Children { get; set; } = new();
}
