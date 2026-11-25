namespace QwenCodeManager.Core.Models;

/// <summary>Global settings persisted at %LOCALAPPDATA%\QwenCodeManager\config\settings.json.</summary>
public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    // General
    public string? QwenExecutable { get; set; }
    public LaunchMode DefaultLaunchMode { get; set; } = LaunchMode.Continue;

    // Appearance
    public string Theme { get; set; } = "Dark";
    public string AccentColor { get; set; } = "Teal";

    // Projects
    public string? DefaultProjectsDirectory { get; set; }
    public bool AutoScanProjects { get; set; }
    public bool ShowHiddenProjects { get; set; }

    // Network
    public bool GitHubCoverLookup { get; set; } = true;
    public int CoverCacheDays { get; set; } = 14;

    public bool OnboardingCompleted { get; set; }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
