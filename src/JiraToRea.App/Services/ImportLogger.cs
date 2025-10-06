using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JiraToRea.App.Models;

namespace JiraToRea.App.Services;

public sealed class ImportLogger
{
    private readonly string _logFilePath;
    private readonly object _syncRoot = new();

    public ImportLogger()
    {
        var baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JiraToRea",
            "Logs");

        Directory.CreateDirectory(baseDirectory);
        _logFilePath = Path.Combine(baseDirectory, "import.log");
    }

    public void LogImport(string userId, string projectId, IReadOnlyCollection<WorklogEntryViewModel> entries, bool success, string? errorMessage = null)
    {
        try
        {
            var logEntry = BuildLogEntry(userId, projectId, entries, success, errorMessage);

            lock (_syncRoot)
            {
                File.AppendAllText(_logFilePath, logEntry, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging failures should not interrupt the application flow.
        }
    }

    private static string BuildLogEntry(string userId, string projectId, IReadOnlyCollection<WorklogEntryViewModel> entries, bool success, string? errorMessage)
    {
        var builder = new StringBuilder();
        builder.AppendLine(new string('-', 80));
        builder.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"Result: {(success ? "Success" : "Failure")}");
        builder.AppendLine($"User ID: {userId}");
        builder.AppendLine($"Project ID: {projectId}");
        builder.AppendLine($"Entry Count: {entries.Count}");

        var index = 1;
        foreach (var entry in entries)
        {
            builder.AppendLine($"#{index} | Issue: {entry.IssueKey} | Task: {entry.Task} | Start: {entry.StartDate:yyyy-MM-dd HH:mm} | End: {entry.EndDate:yyyy-MM-dd HH:mm} | Effort: {entry.EffortHours:0.##} | Comment: {entry.Comment}");
            index++;
        }

        if (!success && !string.IsNullOrWhiteSpace(errorMessage))
        {
            builder.AppendLine($"Error: {errorMessage}");
        }

        builder.AppendLine();
        return builder.ToString();
    }
}
