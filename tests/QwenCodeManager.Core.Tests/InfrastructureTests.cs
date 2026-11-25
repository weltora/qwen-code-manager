using QwenCodeManager.Core.Infrastructure;
using QwenCodeManager.Core.Models;

namespace QwenCodeManager.Core.Tests;

public sealed class TempRoot : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "qcm-tests-" + Guid.NewGuid().ToString("N")[..8]);
    public AppPaths Paths { get; }

    public TempRoot()
    {
        Directory.CreateDirectory(Root);
        Paths = new AppPaths(
            localAppDataOverride: Path.Combine(Root, "localappdata"),
            qwenHomeOverride: Path.Combine(Root, "qwenhome"));
        Paths.EnsureCreated();
    }

    public string NewDir(string name)
    {
        var dir = Path.Combine(Root, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch { }
    }
}

public class AppPathsTests
{
    [Theory]
    [InlineData(@"C:\Users\WELTORA\Desktop\QwenCodeManager", "c--users-weltora-desktop-qwencodemanager")]
    [InlineData(@"C:\Users\WELTORA\Desktop\NoveLA-default", "c--users-weltora-desktop-novela-default")]
    [InlineData(@"D:\Projects\Cyberpunk\", "d--projects-cyberpunk")]
    // Real-world cases verified against Qwen 0.28.x storage: '_' and '.' also become '-'
    [InlineData(@"C:\Users\WELTORA\Desktop\Blackwall_Unleashed_0.2.2_RAM_RU", "c--users-weltora-desktop-blackwall-unleashed-0-2-2-ram-ru")]
    // Spaces become '-', consecutive dashes are NOT collapsed (" - " -> "---")
    [InlineData(@"C:\Users\X\Desktop\Blackwall Unleashed - Canto Mk.6 39002 0.2.2", "c--users-x-desktop-blackwall-unleashed---canto-mk-6-39002-0-2-2")]
    public void SlugMatchesRealQwenLayout(string path, string expected)
    {
        Assert.Equal(expected, AppPaths.ToQwenProjectSlug(path));
    }

    [Fact]
    public void ResolveChatsDirFallsBackToWorkDirScan()
    {
        using var temp = new TempRoot();
        var project = temp.NewDir("Some_Weird.Name v2");
        // Simulate a Qwen layout whose slug does not match our computed rule.
        var oddChats = Path.Combine(temp.Paths.QwenProjectsDir, "totally-different-slug", "chats");
        Directory.CreateDirectory(oddChats);
        var id = Guid.NewGuid().ToString();
        File.WriteAllText(
            Path.Combine(oddChats, id + ".runtime.json"),
            System.Text.Json.JsonSerializer.Serialize(new { work_dir = project, started_at = 1795500000.0, qwen_version = "0.28.4" }));

        var service = new SessionService(temp.Paths, NullLogger.Instance);
        var summary = service.GetSummary(project);

        Assert.Equal(1, summary.Total);
        Assert.Equal(oddChats, service.ResolveChatsDir(project));
    }

    [Fact]
    public void SlugRuleMatchesEveryRealQwenProjectFolder()
    {
        // Real-data smoke test: verifies the rule against actual QWEN_HOME storage when present.
        var qwenHome = Environment.GetEnvironmentVariable("QWEN_HOME");
        var projectsDir = qwenHome is null ? null : Path.Combine(qwenHome, "projects");
        if (projectsDir is null || !Directory.Exists(projectsDir))
        {
            return; // no Qwen installation on this machine
        }

        var failures = new List<string>();
        foreach (var projectDir in Directory.EnumerateDirectories(projectsDir))
        {
            var chats = Path.Combine(projectDir, "chats");
            if (!Directory.Exists(chats))
            {
                continue;
            }

            var runtime = Directory.EnumerateFiles(chats, "*.runtime.json").FirstOrDefault();
            if (runtime is null)
            {
                continue;
            }

            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(runtime));
            if (!doc.RootElement.TryGetProperty("work_dir", out var wdEl))
            {
                continue;
            }

            var workDir = wdEl.GetString()!;
            var expected = Path.GetFileName(projectDir);
            var computed = AppPaths.ToQwenProjectSlug(workDir);
            if (computed != expected)
            {
                failures.Add($"{workDir}: expected '{expected}', computed '{computed}'");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void ChatsDirUsesSlug()
    {
        var paths = new AppPaths(@"C:\temp\lad", @"D:\QwenHome");
        Assert.Equal(
            @"D:\QwenHome\projects\c--projects-app\chats",
            paths.QwenChatsDirFor(@"C:\Projects\App"));
    }
}

public class JsonStoreTests
{
    [Fact]
    public async Task CorruptJsonReturnsDefaultInsteadOfThrowing()
    {
        using var temp = new TempRoot();
        var file = Path.Combine(temp.Root, "broken.json");
        await File.WriteAllTextAsync(file, "{ this is not json !!!");

        var result = await Json.ReadFileAsync<AppSettings>(file);
        Assert.Null(result);
    }

    [Fact]
    public async Task MissingFileReturnsDefault()
    {
        using var temp = new TempRoot();
        var result = await Json.ReadFileAsync<AppSettings>(Path.Combine(temp.Root, "nope.json"));
        Assert.Null(result);
    }

    [Fact]
    public async Task RoundTripPreservesValues()
    {
        using var temp = new TempRoot();
        var file = Path.Combine(temp.Root, "settings.json");
        var settings = new AppSettings { QwenExecutable = @"C:\npm\qwen.cmd", Theme = "Dark", CoverCacheDays = 3 };

        await Json.WriteFileAsync(file, settings);
        var loaded = await Json.ReadFileAsync<AppSettings>(file);

        Assert.NotNull(loaded);
        Assert.Equal(settings.QwenExecutable, loaded!.QwenExecutable);
        Assert.Equal(3, loaded.CoverCacheDays);
    }
}
