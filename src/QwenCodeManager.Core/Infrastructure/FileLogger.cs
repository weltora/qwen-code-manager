namespace QwenCodeManager.Core.Infrastructure;

public interface ILogger
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
    void Log(LogLevel level, string message);
}

public enum LogLevel
{
    Info,
    Warning,
    Error,
}

/// <summary>Append-only text log at %LOCALAPPDATA%\QwenCodeManager\logs\manager.log.</summary>
public sealed class FileLogger : ILogger
{
    private readonly string _logFile;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private const long MaxBytes = 2 * 1024 * 1024;

    public FileLogger(AppPaths paths)
    {
        _logFile = paths.LogFile;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logFile)!);
        }
        catch
        {
            // Logging must never break the app.
        }
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}: {ex.GetType().Name}: {ex.Message}");
    public void Log(LogLevel level, string message) =>
        Write(level switch
        {
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            _ => "INFO",
        }, message);

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {Sanitize(message)}{Environment.NewLine}";
        _ = Task.Run(async () =>
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var info = new FileInfo(_logFile);
                if (info.Exists && info.Length > MaxBytes)
                {
                    // Keep the newest half when the log grows too large.
                    var text = await File.ReadAllTextAsync(_logFile).ConfigureAwait(false);
                    text = text[(text.Length / 2)..];
                    await File.WriteAllTextAsync(_logFile, text).ConfigureAwait(false);
                }
                await File.AppendAllTextAsync(_logFile, line).ConfigureAwait(false);
            }
            catch
            {
                // Swallow: logging failures are not user-visible errors.
            }
            finally
            {
                _gate.Release();
            }
        });
    }

    private static string Sanitize(string message)
    {
        // Cheap defense: never let obvious secret material reach the log.
        var idx = message.IndexOf("sk-", StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? message[..idx] + "[redacted]" : message;
    }
}

public sealed class NullLogger : ILogger
{
    public static readonly NullLogger Instance = new();
    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message, Exception? ex = null) { }
    public void Log(LogLevel level, string message) { }
}
