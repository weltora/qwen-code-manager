using QwenCodeManager.Core.Infrastructure;
using QwenCodeManager.Core.Models;
using QwenCodeManager.Core.Services;

namespace QwenCodeManager.Core.Tests;

public class ProjectServiceTests
{
    private static ProjectService Create(TempRoot temp, FakeProcessRunner? runner = null)
    {
        runner ??= new FakeProcessRunner();
        var settings = new SettingsService(temp.Paths, NullLogger.Instance);
        var git = new GitService(runner, NullLogger.Instance);
        var sessions = new SessionService(temp.Paths, NullLogger.Instance);
        return new ProjectService(temp.Paths, settings, git, sessions, NullLogger.Instance);
    }

    [Fact]
    public async Task AddDetectsNodeProjectAndPersists()
    {
        using var temp = new TempRoot();
        var dir = temp.NewDir("webapp");
        await File.WriteAllTextAsync(Path.Combine(dir, "package.json"), """{"name":"cool-app","dependencies":{"express":"^4"}}""");

        var service = Create(temp);
        var entry = await service.AddAsync(dir);

        Assert.Equal("cool-app", entry.Name);

        var reloaded = Create(temp);
        await reloaded.LoadAsync();
        var found = Assert.Single(reloaded.Projects);
        Assert.Equal(entry.Id, found.Id);
    }

    [Fact]
    public async Task AddIsIdempotentForSamePath()
    {
        using var temp = new TempRoot();
        var dir = temp.NewDir("app");
        var service = Create(temp);

        var first = await service.AddAsync(dir);
        var second = await service.AddAsync(dir);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(service.Projects);
    }

    [Fact]
    public async Task AddMissingDirectoryThrows()
    {
        using var temp = new TempRoot();
        var service = Create(temp);
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => service.AddAsync(@"Z:\missing"));
    }

    [Fact]
    public async Task RemoveKeepsProjectFiles()
    {
        using var temp = new TempRoot();
        var dir = temp.NewDir("keepme");
        var marker = Path.Combine(dir, "source.txt");
        await File.WriteAllTextAsync(marker, "user work");

        var service = Create(temp);
        var entry = await service.AddAsync(dir);
        await service.RemoveAsync(entry.Id);

        Assert.Empty(service.Projects);
        Assert.True(File.Exists(marker));
    }

    [Fact]
    public async Task CorruptProjectsFileStartsEmptyWithBackup()
    {
        using var temp = new TempRoot();
        await File.WriteAllTextAsync(temp.Paths.ProjectsFile, "NOT JSON");

        var service = Create(temp);
        await service.LoadAsync();

        Assert.Empty(service.Projects);
        Assert.Contains(Directory.GetFiles(temp.Paths.Config), f => f.Contains(".corrupt-"));
    }

    [Fact]
    public async Task RemovedPathReportsMissingPathStatus()
    {
        using var temp = new TempRoot();
        var dir = temp.NewDir("gone");
        var service = Create(temp);
        var entry = await service.AddAsync(dir);
        Directory.Delete(dir, true);

        var state = await service.RefreshStateAsync(entry, qwenAvailable: true);
        Assert.Equal(ProjectStatus.MissingPath, state.Status);
    }

    [Fact]
    public void StatusPriorityWorks()
    {
        Assert.Equal(ProjectStatus.MissingPath,
            ProjectService.ComputeStatus(new ProjectState { PathExists = false }, true));
        Assert.Equal(ProjectStatus.QwenUnavailable,
            ProjectService.ComputeStatus(new ProjectState { PathExists = true }, false));
        Assert.Equal(ProjectStatus.NoGit,
            ProjectService.ComputeStatus(new ProjectState { PathExists = true, Git = new GitStatus() }, true));
        Assert.Equal(ProjectStatus.GitModified,
            ProjectService.ComputeStatus(new ProjectState { PathExists = true, Git = new GitStatus { IsRepository = true, Modified = 1 } }, true));
        Assert.Equal(ProjectStatus.Ready,
            ProjectService.ComputeStatus(new ProjectState { PathExists = true, Git = new GitStatus { IsRepository = true } }, true));
    }
}

public class ProjectDetectorTests
{
    [Fact]
    public void GradleRootProjectNameWins()
    {
        using var temp = new TempRoot();
        var dir = temp.NewDir("gradle-app");
        File.WriteAllText(Path.Combine(dir, "settings.gradle.kts"), """rootProject.name = "NoveLA" """);
        File.WriteAllText(Path.Combine(dir, "package.json"), """{"name":"ignored"}""");

        Assert.Equal("NoveLA", ProjectDetector.Detect(dir).Name);
    }

    [Fact]
    public void AndroidDetectedFromManifest()
    {
        using var temp = new TempRoot();
        var dir = temp.NewDir("android");
        File.WriteAllText(Path.Combine(dir, "settings.gradle.kts"), """rootProject.name = "Reader" """);
        var manifestDir = Path.Combine(dir, "app", "src", "main");
        Directory.CreateDirectory(manifestDir);
        File.WriteAllText(Path.Combine(manifestDir, "AndroidManifest.xml"), "<manifest/>");

        Assert.Equal(ProjectType.Android, ProjectDetector.DetectType(dir));
    }

    [Fact]
    public void WebDetectedFromReactDependency()
    {
        using var temp = new TempRoot();
        var dir = temp.NewDir("web");
        File.WriteAllText(Path.Combine(dir, "package.json"), """{"name":"site","dependencies":{"react":"^18"}}""");

        Assert.Equal(ProjectType.Web, ProjectDetector.DetectType(dir));
    }

    [Fact]
    public void PlainNodeIsNodeJs()
    {
        using var temp = new TempRoot();
        var dir = temp.NewDir("node");
        File.WriteAllText(Path.Combine(dir, "package.json"), """{"name":"cli-tool","dependencies":{"express":"^4"}}""");

        Assert.Equal(ProjectType.NodeJs, ProjectDetector.DetectType(dir));
    }

    [Fact]
    public void UnknownFallsBackToOtherAndFolderName()
    {
        using var temp = new TempRoot();
        var dir = temp.NewDir("mystery-box");

        var detection = ProjectDetector.Detect(dir);
        Assert.Equal(ProjectType.Other, detection.Type);
        Assert.Equal("mystery-box", detection.Name);
    }

    [Fact]
    public void MalformedPackageJsonDoesNotThrow()
    {
        using var temp = new TempRoot();
        var dir = temp.NewDir("broken");
        File.WriteAllText(Path.Combine(dir, "package.json"), "{invalid");

        var detection = ProjectDetector.Detect(dir);
        Assert.Equal("broken", detection.Name);
    }
}

public class SettingsAndCheckpointTests
{
    [Fact]
    public async Task SettingsRoundTripAndReset()
    {
        using var temp = new TempRoot();
        var service = new SettingsService(temp.Paths, NullLogger.Instance);
        await service.LoadAsync();

        await service.UpdateAsync(s => s.QwenExecutable = @"C:\npm\qwen.cmd");

        var second = new SettingsService(temp.Paths, NullLogger.Instance);
        await second.LoadAsync();
        Assert.Equal(@"C:\npm\qwen.cmd", second.Settings.QwenExecutable);

        await second.ResetAsync();
        Assert.Null(second.Settings.QwenExecutable);
        Assert.Contains(Directory.GetFiles(temp.Paths.Config), f => f.Contains(".backup-"));
    }

    [Fact]
    public async Task CorruptSettingsFallsBackToDefaults()
    {
        using var temp = new TempRoot();
        await File.WriteAllTextAsync(temp.Paths.SettingsFile, "garbage");

        var service = new SettingsService(temp.Paths, NullLogger.Instance);
        await service.LoadAsync();

        Assert.Equal("Dark", service.Settings.Theme);
    }

    [Fact]
    public async Task CheckpointCreatedOnlyOnRequestAndReadable()
    {
        using var temp = new TempRoot();
        var dir = temp.NewDir("proj");
        var entry = new ProjectEntry { Name = "Demo", Path = dir };
        var service = new CheckpointService(NullLogger.Instance);

        Assert.False(service.Exists(entry));
        var path = await service.CreateIfMissingAsync(entry);

        Assert.True(File.Exists(path));
        Assert.Equal(Path.Combine(dir, ".qwen-manager", "checkpoint.md"), path);
        var content = await service.ReadAsync(entry);
        Assert.Contains("Current task:", content);
        Assert.Contains("Demo", content);

        await service.WriteAsync(entry, "# updated by user");
        Assert.Equal("# updated by user", await service.ReadAsync(entry));
    }

    [Fact]
    public async Task CheckpointCreateDoesNotOverwriteExisting()
    {
        using var temp = new TempRoot();
        var dir = temp.NewDir("proj");
        var entry = new ProjectEntry { Name = "Demo", Path = dir };
        var service = new CheckpointService(NullLogger.Instance);

        var path = await service.CreateIfMissingAsync(entry);
        await File.WriteAllTextAsync(path, "user content");
        await service.CreateIfMissingAsync(entry);

        Assert.Equal("user content", await File.ReadAllTextAsync(path));
    }
}

public class CoverTests
{
    [Fact]
    public void PlaceholderIsGeneratedDeterministically()
    {
        using var temp = new TempRoot();
        var target = Path.Combine(temp.Root, "cover.png");
        CoverPlaceholder.Generate("NoveLA", target);
        Assert.True(File.Exists(target));
        Assert.True(new FileInfo(target).Length > 0);
    }

    [Fact]
    public async Task OfflineCoverFallsBackToPlaceholder()
    {
        using var temp = new TempRoot();
        var dir = temp.NewDir("proj");
        var entry = new ProjectEntry { Id = "abc123", Name = "Offline App", Path = dir, GitHubUrl = "https://github.com/x/y" };

        var service = new CoverService(temp.Paths, NullLogger.Instance);
        var cover = await service.ResolveAsync(entry, allowNetwork: false);

        Assert.NotNull(cover);
        Assert.True(File.Exists(cover));
        Assert.StartsWith(temp.Paths.Covers, cover);
    }

    [Fact]
    public async Task UserCoverWins()
    {
        using var temp = new TempRoot();
        var dir = temp.NewDir("proj");
        var userCover = Path.Combine(dir, "my-cover.png");
        CoverPlaceholder.Generate("Custom", userCover);
        var entry = new ProjectEntry { Id = "abc123", Name = "App", Path = dir, CoverPath = userCover };

        var service = new CoverService(temp.Paths, NullLogger.Instance);
        var cover = await service.ResolveAsync(entry, allowNetwork: false);

        Assert.Equal(userCover, cover);
    }

    [Fact]
    public async Task ProjectImageIsImported()
    {
        using var temp = new TempRoot();
        var dir = temp.NewDir("proj");
        var logo = Path.Combine(dir, "logo.png");
        CoverPlaceholder.Generate("Logo", logo);
        var entry = new ProjectEntry { Id = "abc123", Name = "App", Path = dir };

        var service = new CoverService(temp.Paths, NullLogger.Instance);
        var cover = await service.ResolveAsync(entry, allowNetwork: false);

        Assert.NotNull(cover);
        Assert.StartsWith(temp.Paths.Covers, cover);
    }
}
