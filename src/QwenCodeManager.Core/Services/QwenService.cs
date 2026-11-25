using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using QwenCodeManager.Core.Infrastructure;
using QwenCodeManager.Core.Models;

namespace QwenCodeManager.Core.Services;

/// <summary>
/// Manages Qwen Code CLI detection and launching (Continue / New / Resume).
/// Executing .cmd shims safely: batch files must run through cmd.exe /d /s /c
/// (a bare CreateProcess on "qwen" picks the extensionless POSIX sh shim first).
/// </summary>
public sealed class QwenService(AppPaths paths, IProcessRunner runner, ILogger logger)
{
    public static readonly string[] KnownNpmLocations =
    [
        @"%APPDATA%\npm\qwen.cmd",
        @"%USERPROFILE%\AppData\Roaming\npm\qwen.cmd",
    ];

    /// <summary>Locates qwen.cmd/qwen.exe. Never writes or modifies anything.</summary>
    public async Task<QwenCliInfo> DetectAsync(CancellationToken ct = default)
    {
        var info = new QwenCliInfo { QwenHome = paths.QwenHome };

        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(CurrentExecutableOverride))
        {
            candidates.Add(CurrentExecutableOverride);
        }

        foreach (var raw in KnownNpmLocations)
        {
            var expanded = Environment.ExpandEnvironmentVariables(raw);
            if (!candidates.Contains(expanded, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(expanded);
            }
        }

        var fromPath = LocateOnPath();
        if (fromPath is not null && !candidates.Contains(fromPath, StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add(fromPath);
        }

        foreach (var candidate in candidates)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate))
                {
                    continue;
                }

                var args = QuotesIfNeeded(candidate) + " --version";
                var result = await runner.RunAsync(
                    "cmd.exe",
                    $"/d /s /c {args}",
                    Path.GetTempPath(),
                    TimeSpan.FromSeconds(20),
                    ct).ConfigureAwait(false);

                if (result.StdOut.Trim() is { Length: > 0 } version)
                {
                    info.Found = true;
                    info.ExecutablePath = candidate;
                    info.Version = version.Split('\n')[0].Trim();
                    break;
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Warning, $"Qwen detection failed for '{candidate}': {ex.Message}");
            }
        }

        if (!info.Found)
        {
            info.Error = "Qwen Code not found. Set the path to qwen.cmd in Settings.";
        }

        logger.Log(LogLevel.Info, info.Found
            ? $"Qwen CLI found: '{info.ExecutablePath}' version {info.Version}, QWEN_HOME='{paths.QwenHome}'"
            : "Qwen CLI not found.");
        return info;
    }

    /// <summary>Explicit user-configured path; when set it is the single source of truth.</summary>
    public string? CurrentExecutableOverride { get; set; }

    /// <summary>Project override wins, then the global one.</summary>
    public string? ResolveExecutable(ProjectEntry? project = null)
    {
        var perProject = project?.QwenExecutable;
        if (!string.IsNullOrWhiteSpace(perProject))
        {
            return perProject;
        }

        if (!string.IsNullOrWhiteSpace(CurrentExecutableOverride))
        {
            return CurrentExecutableOverride;
        }

        return LocateOnPath() ?? ResolveKnownNpm();
    }

    /// <summary>Walks PATH honoring PATHEXT, skipping the extensionless POSIX sh shim.</summary>
    public static string? LocateOnPath()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
        {
            return null;
        }

        var extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var dir in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Directory.Exists(dir))
            {
                continue;
            }

            foreach (var ext in extensions)
            {
                if (ext.Equals(".PS1", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var full = Path.Combine(dir, "qwen" + ext.ToLowerInvariant());
                    if (File.Exists(full))
                    {
                        return full;
                    }
                }
                catch
                {
                    // PATH may contain invalid entries.
                }
            }
        }

        return null;
    }

    public Task<ProcessLauncherResult> LaunchAsync(ProjectEntry project, LaunchMode mode, string? sessionId = null, CancellationToken ct = default)
    {
        if (!Directory.Exists(project.Path))
        {
            return Task.FromResult(ProcessLauncherResult.Failure($"Project directory not found:\n{project.Path}"));
        }

        var exe = ResolveExecutable(project);
        if (exe is null || !File.Exists(exe))
        {
            return Task.FromResult(ProcessLauncherResult.Failure("Qwen Code not found. Set the path to qwen.cmd in Settings."));
        }

        // Verified against qwen --help 0.28.4: -c continues the latest session,
        // -r <id> resumes a specific one, no flag starts a new session.
        string launchArgs;
        if (mode == LaunchMode.Continue)
        {
            launchArgs = sessionId is { Length: > 0 }
                ? $"-r {sessionId}"
                : "-c";
        }
        else
        {
            launchArgs = string.Empty;
        }

        // Windows Terminal, if present, gives the user resize/split/scroll for free.
        var wt = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Microsoft\WindowsApps\wt.exe");

        try
        {
            int pid;
            if (File.Exists(wt))
            {
                // /k keeps the shell open after Qwen exits so errors stay readable.
                var wtArgs = $"-d {QuotesIfNeeded(project.Path)} cmd.exe /d /k {BuildCmdInvocation(exe, launchArgs)}";
                pid = runner.StartDetached(wt, wtArgs, project.Path);
            }
            else
            {
                var cmdArgs = $"/d /k {BuildCmdInvocation(exe, launchArgs)}";
                pid = runner.StartDetached("cmd.exe", cmdArgs, project.Path);
            }

            if (pid < 0)
            {
                return Task.FromResult(ProcessLauncherResult.Failure("Failed to start Qwen Code process."));
            }

            logger.Log(LogLevel.Info, $"Launched Qwen ({mode}) for '{project.Name}' in '{project.Path}' (pid {pid}).");
            return Task.FromResult(ProcessLauncherResult.Success(pid));
        }
        catch (Exception ex)
        {
            logger.Error($"Qwen launch failed for '{project.Name}': {ex.Message}", ex);
            return Task.FromResult(ProcessLauncherResult.Failure("Failed to start Qwen Code:\n" + ex.Message));
        }
    }

    private static string? ResolveKnownNpm()
    {
        foreach (var raw in KnownNpmLocations)
        {
            var expanded = Environment.ExpandEnvironmentVariables(raw);
            if (File.Exists(expanded))
            {
                return expanded;
            }
        }

        return null;
    }

    /// <summary>
    /// Builds the cmd.exe command-line tail that runs a batch/exe with args.
    /// Outer quotes are required by cmd's quote-stripping rule when the batch path is quoted.
    /// </summary>
    internal static string BuildCmdInvocation(string exe, string launchArgs)
    {
        var call = QuotesIfNeeded(exe) + (launchArgs.Length > 0 ? " " + launchArgs : string.Empty);
        return "\"" + call + "\"";
    }

    internal static string QuotesIfNeeded(string value)
    {
        return value.Contains(' ') && !value.StartsWith('"')
            ? "\"" + value + "\""
            : value;
    }
}

public sealed record ProcessLauncherResult(bool Ok, string? Error, int Pid)
{
    public static ProcessLauncherResult Success(int pid) => new(true, null, pid);
    public static ProcessLauncherResult Failure(string error) => new(false, error, -1);
}
