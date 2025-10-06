namespace JiraToRea.App.Models;

public sealed class UserSettings
{
    public string ReaUsername { get; set; } = string.Empty;

    public string ReaPassword { get; set; } = string.Empty;

    public string JiraEmail { get; set; } = string.Empty;

    public string JiraToken { get; set; } = string.Empty;
}
