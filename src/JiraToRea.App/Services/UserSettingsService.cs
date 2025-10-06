using System;
using System.IO;
using System.Text.Json;
using JiraToRea.App.Models;

namespace JiraToRea.App.Services;

public sealed class UserSettingsService
{
    private readonly string _settingsFilePath;

    public UserSettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "JiraToRea");
        Directory.CreateDirectory(folder);
        _settingsFilePath = Path.Combine(folder, "settings.json");
    }

    public UserSettings Load()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var settings = JsonSerializer.Deserialize<UserSettings>(json);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
        }
        catch
        {
            // ignored - fallback to default settings
        }

        return new UserSettings();
    }

    public void Save(UserSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings);
            File.WriteAllText(_settingsFilePath, json);
        }
        catch
        {
            // ignored - best effort persistence
        }
    }
}
