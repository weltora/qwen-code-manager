using QwenCodeManager.Core.Infrastructure;
using QwenCodeManager.Core.Models;

namespace QwenCodeManager.Core.Services;

/// <summary>
/// Cover resolution, offline-first: user cover → project image → cached GitHub social
/// preview (network optional) → procedural placeholder. Images are cached under covers\.
/// All work happens on a background thread (RefreshAsync), never on the UI thread.
/// </summary>
public sealed class CoverService(AppPaths paths, ILogger logger)
{
    private static readonly string[] ProjectImageNames =
    [
        "cover", "banner", "logo", "icon", "preview", "screenshot",
    ];

    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp"];

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
        DefaultRequestHeaders = { { "User-Agent", "QwenCodeManager" } },
    };

    public Task<string?> ResolveAsync(ProjectEntry entry, bool allowNetwork, CancellationToken ct = default) =>
        Task.Run(() => Resolve(entry, allowNetwork, ct), ct);

    public string? Resolve(ProjectEntry entry, bool allowNetwork, CancellationToken ct = default)
    {
        try
        {
            // 1. Explicit user cover
            if (!string.IsNullOrWhiteSpace(entry.CoverPath) && File.Exists(entry.CoverPath))
            {
                return entry.CoverPath;
            }

            // 2. Cached cover (placeholder, imported image or downloaded preview)
            var cached = paths.CoverFile(entry.Id, ".png");
            if (File.Exists(cached))
            {
                return cached;
            }

            // 3. Image inside the project (root only)
            if (Directory.Exists(entry.Path))
            {
                var found = FindProjectImage(entry.Path);
                if (found is not null)
                {
                    var imported = Import(found, paths.CoverFile(entry.Id, Path.GetExtension(found).ToLowerInvariant()));
                    if (imported is not null)
                    {
                        return imported;
                    }
                }
            }

            // 4. GitHub social preview (network, cached; skipped entirely offline)
            if (allowNetwork && entry.GitHubUrl is { Length: > 0 } github)
            {
                var downloaded = TryDownloadGitHubCover(github, paths.CoverFile(entry.Id, ".jpg"), ct).GetAwaiter().GetResult();
                if (downloaded is not null)
                {
                    return downloaded;
                }
            }

            // 5. Procedural placeholder
            CoverPlaceholder.Generate(entry.Name, cached);
            return cached;
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Warning, $"Cover resolution failed for '{entry.Name}': {ex.Message}");
            return null;
        }
    }

    /// <summary>Forces re-download/re-import for a project (used by Refresh after cache clear).</summary>
    public void Invalidate(string projectId)
    {
        foreach (var ext in new[] { ".png", ".jpg" })
        {
            try
            {
                var file = paths.CoverFile(projectId, ext);
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Warning, $"Cover invalidate failed for '{projectId}': {ex.Message}");
            }
        }
    }

    private static string? FindProjectImage(string dir)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                var name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ImageExtensions.Contains(ext) && ProjectImageNames.Contains(name))
                {
                    return file;
                }
            }
        }
        catch
        {
            // unreadable
        }

        return null;
    }

    private string? Import(string source, string target)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
            return target;
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Warning, $"Cover import failed: {ex.Message}");
            return null;
        }
    }

    private async Task<string?> TryDownloadGitHubCover(string githubUrl, string targetPath, CancellationToken ct)
    {
        try
        {
            // opengraph image redirector: works with owner/repo without an API token.
            var uri = new Uri(githubUrl);
            if (uri.Host != "github.com" || uri.Segments.Length < 3)
            {
                return null;
            }

            var ownerRepo = uri.AbsolutePath.Trim('/');
            var bytes = await Http.GetByteArrayAsync($"https://opengraph.githubassets.com/1/{ownerRepo}", ct).ConfigureAwait(false);
            if (bytes.Length == 0)
            {
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await File.WriteAllBytesAsync(targetPath, bytes, ct).ConfigureAwait(false);
            logger.Log(LogLevel.Info, $"GitHub cover cached for '{githubUrl}'.");
            return targetPath;
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Info, $"GitHub cover lookup skipped for '{githubUrl}': {ex.Message}");
            return null;
        }
    }
}
