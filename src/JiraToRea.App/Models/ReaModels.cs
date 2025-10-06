using System;
using System.Text.Json.Serialization;
using JiraToRea.App.Serialization;

namespace JiraToRea.App.Models;

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
