using System.Diagnostics;
using QwenCodeManager.Core.Infrastructure;
using QwenCodeManager.Core.Models;
using QwenCodeManager.Core.Services;

namespace QwenCodeManager.App.Services;

/// <summary>Composition root: builds the UI-independent core services once.</summary>
public sealed class AppServices
{
    public required AppPaths Paths { get; init; }
    public required FileLogger Logger { get; init; }
    public required ProcessRunner ProcessRunner { get; init; }
    public required SettingsService Settings { get; init; }
    public required GitService Git { get; init; }
    public required SessionService Sessions { get; init; }
    public required QwenService Qwen { get; init; }
    public required ProjectService Projects { get; init; }
    public required CheckpointService Checkpoints { get; init; }
    public required CoverService Covers { get; init; }

    public QwenCliInfo QwenInfo { get; set; } = new();

    public static AppServices Create()
    {
        // QCM_DATA_DIR isolates all manager storage (used for demos/screenshots/CI); unset in normal runs.
        var paths = new AppPaths(localAppDataOverride: Environment.GetEnvironmentVariable("QCM_DATA_DIR"));
        paths.EnsureCreated();
        var logger = new FileLogger(paths);
        var runner = new ProcessRunner(logger);
        var settings = new SettingsService(paths, logger);
        var git = new GitService(runner, logger);
        var sessions = new SessionService(paths, logger);
        var qwen = new QwenService(paths, runner, logger);
        return new AppServices
        {
            Paths = paths,
            Logger = logger,
            ProcessRunner = runner,
            Settings = settings,
            Git = git,
            Sessions = sessions,
            Qwen = qwen,
            Projects = new ProjectService(paths, settings, git, sessions, logger),
            Checkpoints = new CheckpointService(logger),
            Covers = new CoverService(paths, logger),
        };
    }

    public async Task InitializeAsync()
    {
        await Settings.LoadAsync().ConfigureAwait(true);
        Qwen.CurrentExecutableOverride = Settings.Settings.QwenExecutable;
        await Projects.LoadAsync().ConfigureAwait(true);
        QwenInfo = await Qwen.DetectAsync().ConfigureAwait(true);
    }

    // ---- Shell actions (all safe, user-initiated) ----

    public void OpenFolder(ProjectEntry project)
    {
        if (!Directory.Exists(project.Path))
        {
            Logger.Warn($"Open folder skipped, path missing: {project.Path}");
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{project.Path}\"") { UseShellExecute = true });
    }

    public void OpenTerminal(ProjectEntry project)
    {
        if (!Directory.Exists(project.Path))
        {
            return;
        }

        var wt = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Microsoft\WindowsApps\wt.exe");
        if (File.Exists(wt))
        {
            ProcessRunner.StartDetached(wt, $"-d \"{project.Path}\"", project.Path);
        }
        else
        {
            ProcessRunner.StartDetached("cmd.exe", $"/k cd /d \"{project.Path}\"", project.Path);
        }
    }

    public void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to open URL '{url}'", ex);
        }
    }

    public void OpenInEditor(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to open '{path}'", ex);
        }
    }
}
