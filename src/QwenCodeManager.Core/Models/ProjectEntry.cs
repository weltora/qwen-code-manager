using System.Text.Json.Serialization;

namespace QwenCodeManager.Core.Models;

/// <summary>Persisted project metadata (projects.json / projects/&lt;id&gt;/metadata.json).</summary>
public sealed class ProjectEntry
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverPath { get; set; }
    public string? GitHubUrl { get; set; }
    public string? QwenExecutable { get; set; }
    public LaunchMode DefaultLaunchMode { get; set; } = LaunchMode.Continue;
    public string? CheckpointPath { get; set; }
    public bool Hidden { get; set; }
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastOpenedUtc { get; set; }
}

public sealed class ProjectsFile
{
    public int Version { get; set; } = 1;
    public List<ProjectEntry> Projects { get; set; } = new();
}

/// <summary>Live, non-persisted state shown on a project card.</summary>
public sealed class ProjectState
{
    public bool PathExists { get; set; }
    public ProjectType Type { get; set; } = ProjectType.Other;
    public ProjectStatus Status { get; set; } = ProjectStatus.Unknown;
    public GitStatus? Git { get; set; }
    public QwenSessionSummary? LastSession { get; set; }
    public int SessionCount { get; set; }
    public string? DetectedName { get; set; }
    public string? DetectedGitHubUrl { get; set; }
    public DateTime ScannedAtUtc { get; set; }
}

public enum ProjectStatus
{
    Unknown,
    Ready,
    Working,
    NeedsAttention,
    GitModified,
    NoGit,
    QwenUnavailable,
    MissingPath,
    Error,
}

public sealed class GitStatus
{
    public bool IsRepository { get; set; }
    public string? Branch { get; set; }
    public int Modified { get; set; }
    public int Staged { get; set; }
    public int Untracked { get; set; }
    public int Ahead { get; set; }
    public int Behind { get; set; }
    public string? RemoteUrl { get; set; }
    public string? GitHubUrl { get; set; }

    [JsonIgnore]
    public bool IsClean => IsRepository && Modified == 0 && Staged == 0 && Untracked == 0;
}

/// <summary>One Qwen session discovered under %QWEN_HOME%\projects\&lt;slug&gt;\chats.</summary>
public sealed class QwenSession
{
    public string SessionId { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? FirstPrompt { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? LastActivityUtc { get; set; }
    public string WorkDir { get; set; } = string.Empty;
    public string? QwenVersion { get; set; }
    public int? Pid { get; set; }
    public long JsonlBytes { get; set; }
    public bool HasTranscript { get; set; }

    /// <summary>True when the runtime pid is still a live process (heuristic, pid reuse possible).</summary>
    public bool IsRunning { get; set; }
}

public sealed class QwenSessionSummary
{
    public QwenSession? Last { get; set; }
    public int Total { get; set; }
    public bool CanContinue { get; set; }
}

public sealed class QwenCliInfo
{
    public bool Found { get; set; }
    public string? ExecutablePath { get; set; }
    public string? Version { get; set; }
    public string? Error { get; set; }
    public string? QwenHome { get; set; }
}
