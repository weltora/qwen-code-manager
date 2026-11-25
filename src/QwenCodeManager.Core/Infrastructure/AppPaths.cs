namespace QwenCodeManager.Core.Infrastructure;

/// <summary>All well-known directories used by the manager. Nothing writes outside these roots.</summary>
public sealed class AppPaths
{
    public AppPaths(string? localAppDataOverride = null, string? qwenHomeOverride = null)
    {
        var localAppData = localAppDataOverride
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Root = Path.Combine(localAppData, "QwenCodeManager");
        Config = Path.Combine(Root, "config");
        Projects = Path.Combine(Root, "projects");
        Covers = Path.Combine(Root, "covers");
        Cache = Path.Combine(Root, "cache");
        Logs = Path.Combine(Root, "logs");

        SettingsFile = Path.Combine(Config, "settings.json");
        ProjectsFile = Path.Combine(Config, "projects.json");
        LogFile = Path.Combine(Logs, "manager.log");

        QwenHome = qwenHomeOverride
            ?? Environment.GetEnvironmentVariable("QWEN_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".qwen");
        QwenProjectsDir = Path.Combine(QwenHome, "projects");
        QwenSessionsDir = Path.Combine(QwenHome, "sessions");
    }

    public string Root { get; }
    public string Config { get; }
    public string Projects { get; }
    public string Covers { get; }
    public string Cache { get; }
    public string Logs { get; }
    public string SettingsFile { get; }
    public string ProjectsFile { get; }
    public string LogFile { get; }
    public string QwenHome { get; }
    public string QwenProjectsDir { get; }
    public string QwenSessionsDir { get; }

    public string ProjectDir(string projectId) => Path.Combine(Projects, projectId);
    public string ProjectMetadata(string projectId) => Path.Combine(ProjectDir(projectId), "metadata.json");
    public string CoverFile(string projectId, string extension) => Path.Combine(Covers, projectId + extension);

    public void EnsureCreated()
    {
        foreach (var dir in new[] { Root, Config, Projects, Covers, Cache, Logs })
        {
            Directory.CreateDirectory(dir);
        }
    }

    /// <summary>
    /// Converts an absolute project path to the folder name Qwen Code uses under
    /// %QWEN_HOME%\projects (e.g. C:\Users\X\Y -&gt; c--users-x-y).
    /// Verified against real folders of Qwen Code 0.28.x.
    /// </summary>
    public static string ToQwenProjectSlug(string absolutePath)
    {
        var normalized = absolutePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var chars = normalized.ToLowerInvariant()
            .Select(c => c is ':' or '\\' or '/' ? '-' : c)
            .ToArray();
        return new string(chars);
    }

    public string QwenChatsDirFor(string projectPath) =>
        Path.Combine(QwenProjectsDir, ToQwenProjectSlug(projectPath), "chats");
}
