using QwenCodeManager.Core.Infrastructure;
using QwenCodeManager.Core.Models;

namespace QwenCodeManager.Core.Services;

/// <summary>
/// Owns projects.json (metadata only — never touches project files) and the live
/// per-project state (git, sessions, detection).
/// </summary>
public sealed class ProjectService(
    AppPaths paths,
    SettingsService settings,
    GitService git,
    SessionService sessions,
    ILogger logger)
{
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public List<ProjectEntry> Projects { get; private set; } = new();
    public event Action? ProjectsChanged;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var file = await Json.ReadFileAsync<ProjectsFile>(paths.ProjectsFile, logger).ConfigureAwait(false);
        if (file is null && File.Exists(paths.ProjectsFile))
        {
            logger.Log(LogLevel.Error, "projects.json is corrupt; keeping a backup and starting with an empty list.");
            TryBackup(paths.ProjectsFile);
        }

        Projects = file?.Projects ?? new List<ProjectEntry>();
        ProjectsChanged?.Invoke();
    }

    public ProjectEntry? Find(string id) =>
        Projects.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    public ProjectEntry? FindByPath(string path) =>
        Projects.FirstOrDefault(p => string.Equals(
            p.Path.TrimEnd('\\', '/'), path.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase));

    /// <summary>Adds a directory as a project. Detection is read-only; only manager storage is written.</summary>
    public async Task<ProjectEntry> AddAsync(string directory, CancellationToken ct = default)
    {
        var full = Path.GetFullPath(directory);
        if (!Directory.Exists(full))
        {
            throw new DirectoryNotFoundException($"Directory not found: {full}");
        }

        var existing = FindByPath(full);
        if (existing is not null)
        {
            return existing;
        }

        var detection = ProjectDetector.Detect(full);
        var gitStatus = await git.GetStatusAsync(full, ct).ConfigureAwait(false);

        var entry = new ProjectEntry
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Name = detection.Name,
            Path = full,
            GitHubUrl = gitStatus.GitHubUrl,
            DefaultLaunchMode = settings.Settings.DefaultLaunchMode,
            AddedAtUtc = DateTime.UtcNow,
        };

        Projects.Add(entry);
        await SaveAsync(ct).ConfigureAwait(false);
        logger.Log(LogLevel.Info, $"Project added: '{entry.Name}' ({full}).");
        ProjectsChanged?.Invoke();
        return entry;
    }

    /// <summary>Removes the project from the manager only. Project files are never deleted.</summary>
    public async Task RemoveAsync(string id, CancellationToken ct = default)
    {
        var entry = Find(id);
        if (entry is null)
        {
            return;
        }

        Projects.Remove(entry);
        await SaveAsync(ct).ConfigureAwait(false);
        logger.Log(LogLevel.Info, $"Project removed from manager: '{entry.Name}' (files untouched).");
        ProjectsChanged?.Invoke();
    }

    public async Task UpdateAsync(ProjectEntry entry, CancellationToken ct = default)
    {
        var index = Projects.FindIndex(p => p.Id == entry.Id);
        if (index >= 0)
        {
            Projects[index] = entry;
        }

        await SaveAsync(ct).ConfigureAwait(false);
        ProjectsChanged?.Invoke();
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        await _saveGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await Json.WriteFileAsync(paths.ProjectsFile, new ProjectsFile { Projects = Projects }, logger).ConfigureAwait(false);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    /// <summary>Scans live state for one project: existence, detection, git, sessions.</summary>
    public async Task<ProjectState> RefreshStateAsync(ProjectEntry entry, bool qwenAvailable, CancellationToken ct = default)
    {
        var state = new ProjectState { ScannedAtUtc = DateTime.UtcNow };
        try
        {
            state.PathExists = Directory.Exists(entry.Path);
            if (!state.PathExists)
            {
                state.Status = ProjectStatus.MissingPath;
                return state;
            }

            var detection = ProjectDetector.Detect(entry.Path);
            state.Type = detection.Type;
            state.DetectedName = detection.Name;

            state.Git = await git.GetStatusAsync(entry.Path, ct).ConfigureAwait(false);
            state.DetectedGitHubUrl = state.Git?.GitHubUrl;

            var summary = sessions.GetSummary(entry.Path);
            state.LastSession = summary;
            state.SessionCount = summary.Total;

            state.Status = ComputeStatus(state, qwenAvailable);
        }
        catch (Exception ex)
        {
            logger.Error($"State scan failed for '{entry.Name}'", ex);
            state.Status = ProjectStatus.Error;
        }

        return state;
    }

    internal static ProjectStatus ComputeStatus(ProjectState state, bool qwenAvailable)
    {
        if (!state.PathExists)
        {
            return ProjectStatus.MissingPath;
        }

        if (!qwenAvailable)
        {
            return ProjectStatus.QwenUnavailable;
        }

        if (state.Git is not { IsRepository: true })
        {
            return ProjectStatus.NoGit;
        }

        if (!state.Git.IsClean || state.Git.Ahead > 0 || state.Git.Behind > 0)
        {
            return ProjectStatus.GitModified;
        }

        return ProjectStatus.Ready;
    }

    private void TryBackup(string file)
    {
        try
        {
            File.Copy(file, file + $".corrupt-{DateTime.Now:yyyyMMddHHmmss}", overwrite: true);
        }
        catch
        {
            // best effort
        }
    }
}
