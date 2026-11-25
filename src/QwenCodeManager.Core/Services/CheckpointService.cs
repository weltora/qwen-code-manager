using QwenCodeManager.Core.Infrastructure;
using QwenCodeManager.Core.Models;

namespace QwenCodeManager.Core.Services;

/// <summary>
/// Project checkpoint: a plain markdown file the user owns. The manager creates the
/// .qwen-manager folder and a template on explicit request only — never invents content.
/// </summary>
public sealed class CheckpointService(ILogger logger)
{
    public const string DefaultRelativePath = ".qwen-manager/checkpoint.md";

    public string ResolvePath(ProjectEntry project)
    {
        if (!string.IsNullOrWhiteSpace(project.CheckpointPath))
        {
            return Path.IsPathRooted(project.CheckpointPath)
                ? project.CheckpointPath
                : Path.Combine(project.Path, project.CheckpointPath);
        }

        return Path.Combine(project.Path, DefaultRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    public bool Exists(ProjectEntry project) => File.Exists(ResolvePath(project));

    /// <summary>Creates the template if missing. Returns the file path.</summary>
    public async Task<string> CreateIfMissingAsync(ProjectEntry project, CancellationToken ct = default)
    {
        var path = ResolvePath(project);
        if (!File.Exists(path))
        {
            if (!Directory.Exists(project.Path))
            {
                throw new DirectoryNotFoundException($"Project directory not found: {project.Path}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var template = $"""
                # Current checkpoint

                Project:
                {project.Name}

                Last updated:
                {DateTime.Now:yyyy-MM-dd}

                Current task:
                -

                Completed:
                -

                Next:
                -

                Notes:
                -
                """;
            await File.WriteAllTextAsync(path, template, ct).ConfigureAwait(false);
            logger.Log(LogLevel.Info, $"Checkpoint created for '{project.Name}': {path}");
        }

        return path;
    }

    public async Task<string?> ReadAsync(ProjectEntry project, CancellationToken ct = default)
    {
        var path = ResolvePath(project);
        if (!File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
    }

    public async Task WriteAsync(ProjectEntry project, string content, CancellationToken ct = default)
    {
        var path = ResolvePath(project);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content, ct).ConfigureAwait(false);
    }
}
