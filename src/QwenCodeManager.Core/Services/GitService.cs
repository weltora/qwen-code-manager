using QwenCodeManager.Core.Infrastructure;
using QwenCodeManager.Core.Models;

namespace QwenCodeManager.Core.Services;

/// <summary>
/// Read-only Git status via the git CLI. Never runs mutating git commands.
/// </summary>
public sealed class GitService(IProcessRunner runner, ILogger logger)
{
    private static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(10);

    public async Task<GitStatus> GetStatusAsync(string projectPath, CancellationToken ct = default)
    {
        var status = new GitStatus();
        if (!Directory.Exists(projectPath))
        {
            return status;
        }

        var inside = await RunGitAsync(projectPath, "rev-parse --is-inside-work-tree", ct).ConfigureAwait(false);
        if (!inside.Success || inside.StdOut.Trim() != "true")
        {
            return status;
        }

        status.IsRepository = true;

        var porcelain = await RunGitAsync(projectPath, "status --porcelain=v1 --branch", ct).ConfigureAwait(false);
        if (porcelain.Success)
        {
            ParsePorcelain(porcelain.StdOut, status);
        }

        var remote = await RunGitAsync(projectPath, "remote get-url origin", ct).ConfigureAwait(false);
        if (remote.Success && remote.StdOut.Trim() is { Length: > 0 } url)
        {
            status.RemoteUrl = url;
            status.GitHubUrl = NormalizeGitHubUrl(url);
        }

        return status;
    }

    internal static void ParsePorcelain(string output, GitStatus status)
    {
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("## "))
            {
                ParseBranchHeader(line[3..], status);
                continue;
            }

            if (line.Length < 3)
            {
                continue;
            }

            var x = line[0];
            var y = line[1];
            if (x == '?' && y == '?')
            {
                status.Untracked++;
                continue;
            }

            if (x is not (' ' or '?'))
            {
                status.Staged++;
            }

            if (y is not (' ' or '?'))
            {
                status.Modified++;
            }
        }
    }

    private static void ParseBranchHeader(string header, GitStatus status)
    {
        // Examples: "main", "main...origin/main [ahead 2, behind 1]", "No commits yet on main"
        var bracket = header.IndexOf('[');
        if (bracket >= 0)
        {
            var inside = header[(bracket + 1)..].TrimEnd(']');
            foreach (var part in inside.Split(','))
            {
                var trimmed = part.Trim();
                if (trimmed.StartsWith("ahead ") && int.TryParse(trimmed[6..], out var ahead))
                {
                    status.Ahead = ahead;
                }
                else if (trimmed.StartsWith("behind ") && int.TryParse(trimmed[7..], out var behind))
                {
                    status.Behind = behind;
                }
            }

            header = header[..bracket].Trim();
        }

        var dots = header.IndexOf("...", StringComparison.Ordinal);
        status.Branch = dots >= 0 ? header[..dots] : header;
    }

    /// <summary>https://github.com/user/repo.git and git@github.com:user/repo.git -&gt; https://github.com/user/repo</summary>
    public static string? NormalizeGitHubUrl(string remoteUrl)
    {
        var url = remoteUrl.Trim();

        if (url.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://github.com/" + url["git@github.com:".Length..];
        }
        else if (url.StartsWith("ssh://git@github.com/", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://github.com/" + url["ssh://git@github.com/".Length..];
        }
        else if (!url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)
                 && !url.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            url = url[..^4];
        }

        url = url.TrimEnd('/');
        var uri = Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? parsed : null;
        if (uri is null || uri.Segments.Length < 3)
        {
            return null;
        }

        return url;
    }

    private async Task<ProcessResult> RunGitAsync(string cwd, string args, CancellationToken ct)
    {
        try
        {
            return await runner.RunAsync("git", args, cwd, GitTimeout, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Warning, $"git {args} failed in '{cwd}': {ex.Message}");
            return new ProcessResult(-1, string.Empty, ex.Message);
        }
    }
}
