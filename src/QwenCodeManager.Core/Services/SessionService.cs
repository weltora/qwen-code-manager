using System.Text.Json;
using QwenCodeManager.Core.Infrastructure;
using QwenCodeManager.Core.Models;

namespace QwenCodeManager.Core.Services;

/// <summary>
/// Read-only discovery of Qwen Code sessions from
/// %QWEN_HOME%\projects\&lt;slug&gt;\chats\&lt;session-id&gt;.{jsonl,runtime.json,title.json}.
/// Layout verified against Qwen Code 0.28.4 on this machine. Never writes to Qwen data.
/// </summary>
public sealed class SessionService(AppPaths paths, ILogger logger)
{
    public QwenSessionSummary GetSummary(string projectPath)
    {
        var sessions = ListSessions(projectPath);
        return new QwenSessionSummary
        {
            Last = sessions.FirstOrDefault(),
            Total = sessions.Count,
            CanContinue = sessions.Any(s => s.HasTranscript),
        };
    }

    /// <summary>All sessions for the project, newest activity first.</summary>
    public List<QwenSession> ListSessions(string projectPath)
    {
        var chatsDir = paths.ResolveChatsDir(projectPath, GetRuntimeWorkDir);
        var result = new List<QwenSession>();
        if (!Directory.Exists(chatsDir))
        {
            return result;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(chatsDir, "*.runtime.json");
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Warning, $"Cannot enumerate sessions in '{chatsDir}': {ex.Message}");
            return result;
        }

        foreach (var runtimeFile in files)
        {
            try
            {
                var session = ReadSession(chatsDir, runtimeFile, projectPath);
                if (session is not null)
                {
                    result.Add(session);
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Warning, $"Skipping unreadable session '{runtimeFile}': {ex.Message}");
            }
        }

        return result
            .OrderByDescending(s => s.LastActivityUtc ?? s.StartedAtUtc ?? DateTime.MinValue)
            .ToList();
    }

    /// <summary>Resolved chats directory for a project (slug rule + work_dir fallback).</summary>
    public string ResolveChatsDir(string projectPath) =>
        paths.ResolveChatsDir(projectPath, GetRuntimeWorkDir);

    /// <summary>Reads just work_dir from a runtime.json for the slug fallback; silent on errors.</summary>
    private static string? GetRuntimeWorkDir(string runtimeFile)
    {
        try
        {
            using var stream = File.Open(runtimeFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var runtime = JsonSerializer.Deserialize<RuntimeInfo>(stream, Json.Options);
            return runtime?.WorkDir;
        }
        catch
        {
            return null;
        }
    }

    private QwenSession? ReadSession(string chatsDir, string runtimeFile, string projectPath)
    {
        var name = Path.GetFileName(runtimeFile);
        const string suffix = ".runtime.json";
        if (!name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var sessionId = name[..^suffix.Length];
        if (!Guid.TryParse(sessionId, out _))
        {
            return null;
        }

        var runtime = Json.ReadFileAsync<RuntimeInfo>(runtimeFile).GetAwaiter().GetResult();
        var jsonlPath = Path.Combine(chatsDir, sessionId + ".jsonl");
        var jsonl = SafeFileInfo(jsonlPath);
        var titlePath = Path.Combine(chatsDir, sessionId + ".title.json");
        var title = File.Exists(titlePath)
            ? Json.ReadFileAsync<TitleInfo>(titlePath).GetAwaiter().GetResult()
            : null;

        DateTime? startedUtc = null;
        if (runtime?.StartedAt is { } started && started > 0)
        {
            startedUtc = DateTimeOffset.FromUnixTimeSeconds((long)started).UtcDateTime;
        }

        DateTime? lastActivityUtc = null;
        if (jsonl is not null)
        {
            // Transcript write time is the freshest activity signal.
            lastActivityUtc = jsonl.LastWriteTimeUtc;
        }
        else if (title?.UpdatedAt is { } updated && DateTime.TryParse(updated, out var parsedTitle))
        {
            lastActivityUtc = parsedTitle.ToUniversalTime();
        }
        else
        {
            lastActivityUtc = startedUtc;
        }

        string? firstPrompt = null;
        if (jsonl is not null)
        {
            firstPrompt = TryReadFirstPrompt(jsonlPath);
        }

        return new QwenSession
        {
            SessionId = sessionId,
            Title = CleanTitle(title?.DisplayName),
            FirstPrompt = firstPrompt,
            StartedAtUtc = startedUtc ?? jsonl?.CreationTimeUtc,
            LastActivityUtc = lastActivityUtc,
            WorkDir = runtime?.WorkDir ?? projectPath,
            QwenVersion = runtime?.QwenVersion,
            Pid = runtime?.Pid,
            JsonlBytes = jsonl?.Length ?? 0,
            HasTranscript = jsonl is not null,
            IsRunning = IsProcessAlive(runtime?.Pid),
        };
    }

    private static bool IsProcessAlive(int? pid)
    {
        if (pid is not { } value || value <= 0)
        {
            return false;
        }

        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(value);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static string? CleanTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var single = title.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return single.Length > 160 ? single[..157] + "..." : single;
    }

    /// <summary>Reads only the first few JSONL records to find the first user prompt; never loads the whole transcript.</summary>
    private string? TryReadFirstPrompt(string jsonlPath)
    {
        try
        {
            using var reader = new StreamReader(jsonlPath);
            for (var i = 0; i < 30; i++)
            {
                var line = reader.ReadLine();
                if (line is null)
                {
                    break;
                }

                if (line.Length == 0)
                {
                    continue;
                }

                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var role = GetString(root, "role") ?? GetStringNested(root, "message", "role");
                if (!string.Equals(role, "user", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var content = GetString(root, "content")
                    ?? ExtractContent(GetProperty(GetProperty(root, "message"), "content"));
                if (!string.IsNullOrWhiteSpace(content))
                {
                    var single = content.Replace('\n', ' ').Trim();
                    return single.Length > 200 ? single[..197] + "..." : single;
                }
            }
        }
        catch
        {
            // Transcript format may evolve; summary simply shows no prompt.
        }

        return null;
    }

    private static JsonElement? GetProperty(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value
            : null;
    }

    private static JsonElement? GetProperty(JsonElement? element, string name) =>
        element is { } e ? GetProperty(e, name) : null;

    private static string? GetString(JsonElement element, string name)
    {
        var value = GetProperty(element, name);
        return value?.ValueKind == JsonValueKind.String ? value.Value.GetString() : null;
    }

    private static string? GetStringNested(JsonElement element, string outer, string inner)
    {
        var nested = GetProperty(element, outer);
        return nested is { } n ? GetString(n, inner) : null;
    }

    private static string? ExtractContent(JsonElement? content)
    {
        if (content is not { } c)
        {
            return null;
        }

        if (c.ValueKind == JsonValueKind.String)
        {
            return c.GetString();
        }

        if (c.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in c.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    return item.GetString();
                }

                var text = GetString(item, "text");
                if (text is not null)
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static FileInfo? SafeFileInfo(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? info : null;
        }
        catch
        {
            return null;
        }
    }

    // Real runtime.json uses snake_case (verified against Qwen Code 0.28.4).
    private sealed class RuntimeInfo
    {
        [System.Text.Json.Serialization.JsonPropertyName("work_dir")]
        public string? WorkDir { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("started_at")]
        public double? StartedAt { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("qwen_version")]
        public string? QwenVersion { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pid")]
        public int? Pid { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("session_id")]
        public string? SessionId { get; set; }
    }

    private sealed class TitleInfo
    {
        public string? DisplayName { get; set; }
        public string? Source { get; set; }
        public string? UpdatedAt { get; set; }
    }
}
