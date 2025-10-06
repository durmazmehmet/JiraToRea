using System;

namespace JiraToRea.App.Models;

public sealed class WorklogEntryViewModel
{
    public string IssueKey { get; set; } = string.Empty;

    public string IssueSummary { get; set; } = string.Empty;

    public string Task { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public double EffortHours { get; set; }

    public string Comment { get; set; } = string.Empty;

    public string? JiraWorklogId { get; set; }
}
