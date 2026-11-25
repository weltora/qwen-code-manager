using QwenCodeManager.Core.Infrastructure;
using QwenCodeManager.Core.Models;

namespace QwenCodeManager.Core.Services;

/// <summary>
/// Loads and saves %LOCALAPPDATA%\QwenCodeManager\config\settings.json.
/// A corrupt or missing file yields defaults instead of failing startup.
/// </summary>
public sealed class SettingsService(AppPaths paths, ILogger logger)
{
    public AppSettings Settings { get; private set; } = new();

    public async Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        var loaded = await Json.ReadFileAsync<AppSettings>(paths.SettingsFile, logger).ConfigureAwait(false);
        Settings = loaded ?? new AppSettings();
        if (loaded is null && File.Exists(paths.SettingsFile))
        {
            logger.Log(LogLevel.Warning, "settings.json unreadable, falling back to defaults (file left untouched).");
        }

        await SaveAsync(ct).ConfigureAwait(false);
        return Settings;
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        try
        {
            await Json.WriteFileAsync(paths.SettingsFile, Settings, logger).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, $"Failed to save settings: {ex.Message}");
        }
    }

    public Task UpdateAsync(Action<AppSettings> mutate, CancellationToken ct = default)
    {
        mutate(Settings);
        return SaveAsync(ct);
    }

    /// <summary>Restores factory defaults; existing file is backed up, not deleted.</summary>
    public async Task ResetAsync(CancellationToken ct = default)
    {
        try
        {
            if (File.Exists(paths.SettingsFile))
            {
                File.Copy(paths.SettingsFile, paths.SettingsFile + $".backup-{DateTime.Now:yyyyMMddHHmmss}", overwrite: true);
            }
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Warning, $"Could not back up settings before reset: {ex.Message}");
        }

        Settings = new AppSettings();
        await SaveAsync(ct).ConfigureAwait(false);
        logger.Log(LogLevel.Info, "Settings reset to defaults.");
    }
}
