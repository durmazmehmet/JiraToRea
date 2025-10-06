using System;
using System.Text.Json.Serialization;
using JiraToRea.App.Serialization;

namespace JiraToRea.App.Models;

public sealed class ReaApiResponse<T>
{
    [JsonPropertyName("data")]
    public T? Data { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("status")]
    public int Status { get; init; }

    [JsonPropertyName("isSuccess")]
    public bool IsSuccess { get; init; }
}

public sealed class ReaLoginRequest
{
    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

public sealed class ReaTimeEntry
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("projectId")]
    public string ProjectId { get; set; } = string.Empty;

    [JsonPropertyName("task")]
    public string Task { get; set; } = string.Empty;

    [JsonPropertyName("startDate")]
    [JsonConverter(typeof(DateOnlyJsonConverter))]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("endDate")]
    [JsonConverter(typeof(DateOnlyJsonConverter))]
    public DateTime EndDate { get; set; }

    [JsonPropertyName("effort")]
    public double Effort { get; set; }

    [JsonPropertyName("comment")]
    public string Comment { get; set; } = string.Empty;
}

public sealed class ReaUserProfile
{
    public ReaUserProfile(string userId, string? name)
    {
        UserId = userId;
        Name = name ?? string.Empty;
    }

    public string UserId { get; }

    public string Name { get; }
}

public sealed class ReaProject
{
    public ReaProject(string id, string name, string? code)
    {
        Id = id;
        Name = name;
        Code = code ?? string.Empty;
    }

    public string Id { get; }

    public string Name { get; }

    public string Code { get; }

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Code)
        ? Name
        : $"{Code} - {Name}";

    public override string ToString() => DisplayName;
}

public sealed class ReaProjectPayload
{
    [JsonPropertyName("projectId")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? ProjectId { get; init; }

    [JsonPropertyName("id")]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? Id { get; init; }

    [JsonPropertyName("projectName")]
    public string? ProjectName { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("projectCode")]
    public string? ProjectCode { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("shortName")]
    public string? ShortName { get; init; }

    [JsonPropertyName("key")]
    public string? Key { get; init; }

    [JsonPropertyName("projectKey")]
    public string? ProjectKey { get; init; }
}
