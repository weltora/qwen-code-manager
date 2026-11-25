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
    public void SlugMatchesRealQwenLayout(string path, string expected)
    {
        Assert.Equal(expected, AppPaths.ToQwenProjectSlug(path));
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
