using System.Text.Json;

namespace QwenCodeManager.Core.Infrastructure;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);

    /// <summary>Reads and parses a JSON file; returns default(T) instead of throwing when the file is missing or corrupt.</summary>
    public static async Task<T?> ReadFileAsync<T>(string path, ILogger? logger = null)
    {
        try
        {
            if (!File.Exists(path))
            {
                return default;
            }

            await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return await JsonSerializer.DeserializeAsync<T>(stream, Options).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.Log(LogLevel.Warning, $"JSON read failed for '{path}': {ex.Message}");
            return default;
        }
    }

    /// <summary>Writes JSON atomically (temp file + replace) so a crash cannot truncate the store.</summary>
    public static async Task WriteFileAsync<T>(string path, T value, ILogger? logger = null)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = path + ".tmp";
        await using (var stream = File.Create(temp))
        {
            await JsonSerializer.SerializeAsync(stream, value, Options).ConfigureAwait(false);
        }

        try
        {
            if (File.Exists(path))
            {
                File.Replace(temp, path, null);
            }
            else
            {
                File.Move(temp, path);
            }
        }
        catch (Exception ex)
        {
            logger?.Log(LogLevel.Warning, $"Atomic replace failed for '{path}' ({ex.Message}); falling back to direct write.");
            File.Copy(temp, path, overwrite: true);
            File.Delete(temp);
        }
    }
}
