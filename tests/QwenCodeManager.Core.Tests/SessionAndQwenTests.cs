using System.Text.Json;
using QwenCodeManager.Core.Infrastructure;
using QwenCodeManager.Core.Models;
using QwenCodeManager.Core.Services;

namespace QwenCodeManager.Core.Tests;

public class SessionServiceTests
{
    private static void WriteSession(AppPaths paths, string projectPath, string sessionId, double startedAt, string? title = null, bool withTranscript = true)
    {
        var chats = paths.QwenChatsDirFor(projectPath);
        Directory.CreateDirectory(chats);

        var runtime = new
        {
            schema_version = 1,
            pid = 1234,
            session_id = sessionId,
            work_dir = projectPath,
            started_at = startedAt,
            qwen_version = "0.28.4",
        };
        File.WriteAllText(Path.Combine(chats, sessionId + ".runtime.json"), JsonSerializer.Serialize(runtime));

        if (withTranscript)
        {
            var lines = new[]
            {
                JsonSerializer.Serialize(new { role = "user", content = "Fix the reader shell" }),
                JsonSerializer.Serialize(new { role = "assistant", content = "Working on it" }),
            };
            File.WriteAllLines(Path.Combine(chats, sessionId + ".jsonl"), lines);
        }

        if (title is not null)
        {
            File.WriteAllText(
                Path.Combine(chats, sessionId + ".title.json"),
                JsonSerializer.Serialize(new { version = 1, displayName = title, source = "prompt", updatedAt = "2026-11-24T12:00:00.000Z" }));
        }
    }

    [Fact]
    public void DiscoversSessionsNewestFirst()
    {
        using var temp = new TempRoot();
        var project = temp.NewDir("project");
        var older = Guid.NewGuid().ToString();
        var newer = Guid.NewGuid().ToString();

        WriteSession(temp.Paths, project, older, 1795000000, "Old work");
        WriteSession(temp.Paths, project, newer, 1795500000, "New work");

        var service = new SessionService(temp.Paths, NullLogger.Instance);
        var summary = service.GetSummary(project);

        Assert.Equal(2, summary.Total);
        Assert.True(summary.CanContinue);
        Assert.Equal(newer, summary.Last!.SessionId);
        Assert.Equal("New work", summary.Last.Title);
        Assert.Equal("Fix the reader shell", summary.Last.FirstPrompt);
        Assert.Equal("0.28.4", summary.Last.QwenVersion);
    }

    [Fact]
    public void NoQwenDataMeansNoSessionsNotError()
    {
        using var temp = new TempRoot();
        var project = temp.NewDir("fresh");

        var summary = new SessionService(temp.Paths, NullLogger.Instance).GetSummary(project);

        Assert.Equal(0, summary.Total);
        Assert.Null(summary.Last);
        Assert.False(summary.CanContinue);
    }

    [Fact]
    public void CorruptRuntimeFileIsSkipped()
    {
        using var temp = new TempRoot();
        var project = temp.NewDir("proj");
        var chats = temp.Paths.QwenChatsDirFor(project);
        Directory.CreateDirectory(chats);
        var id = Guid.NewGuid().ToString();
        File.WriteAllText(Path.Combine(chats, id + ".runtime.json"), "{ broken");

        var summary = new SessionService(temp.Paths, NullLogger.Instance).GetSummary(project);

        // Session still discovered via file name; corrupt runtime yields null fields, no crash.
        Assert.Equal(1, summary.Total);
        Assert.Null(summary.Last!.QwenVersion);
        Assert.False(summary.CanContinue); // no transcript
    }

    [Fact]
    public void RuntimeOnlySessionCannotContinue()
    {
        using var temp = new TempRoot();
        var project = temp.NewDir("proj");
        WriteSession(temp.Paths, project, Guid.NewGuid().ToString(), 1795500000, withTranscript: false);

        var summary = new SessionService(temp.Paths, NullLogger.Instance).GetSummary(project);
        Assert.Equal(1, summary.Total);
        Assert.False(summary.CanContinue);
    }
}

public class QwenServiceTests
{
    [Fact]
    public void CmdInvocationQuotesBatchPathAndArgs()
    {
        var invocation = QwenService.BuildCmdInvocation(@"C:\Users\Some User\AppData\Roaming\npm\qwen.cmd", "-c");
        Assert.Equal("\"\"C:\\Users\\Some User\\AppData\\Roaming\\npm\\qwen.cmd\" -c\"", invocation);
    }

    [Fact]
    public void NewSessionHasNoContinueFlag()
    {
        var invocation = QwenService.BuildCmdInvocation(@"C:\npm\qwen.cmd", string.Empty);
        Assert.Equal("\"C:\\npm\\qwen.cmd\"", invocation);
    }

    [Fact]
    public async Task MissingProjectDirectoryFailsGracefully()
    {
        using var temp = new TempRoot();
        var service = new QwenService(temp.Paths, new FakeProcessRunner(), NullLogger.Instance)
        {
            CurrentExecutableOverride = @"C:\npm\qwen.cmd",
        };

        var result = await service.LaunchAsync(new ProjectEntry { Name = "X", Path = @"Z:\missing\dir" }, LaunchMode.Continue);

        Assert.False(result.Ok);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task MissingExecutableFailsWithActionableError()
    {
        using var temp = new TempRoot();
        var project = temp.NewDir("proj");
        var service = new QwenService(temp.Paths, new FakeProcessRunner(), NullLogger.Instance)
        {
            CurrentExecutableOverride = @"Z:\nowhere\qwen.cmd",
        };

        var result = await service.LaunchAsync(new ProjectEntry { Name = "X", Path = project }, LaunchMode.NewSession);

        Assert.False(result.Ok);
        Assert.Contains("Settings", result.Error);
    }

    [Fact]
    public async Task ContinueLaunchesWithContinueFlagInProjectDirectory()
    {
        using var temp = new TempRoot();
        var project = temp.NewDir("my proj");
        var exe = Path.Combine(temp.Root, "qwen.cmd");
        await File.WriteAllTextAsync(exe, "@echo off");

        var runner = new FakeProcessRunner();
        var service = new QwenService(temp.Paths, runner, NullLogger.Instance)
        {
            CurrentExecutableOverride = exe,
        };

        var result = await service.LaunchAsync(new ProjectEntry { Name = "X", Path = project }, LaunchMode.Continue);

        Assert.True(result.Ok);
        var launch = Assert.Single(runner.Detached);
        Assert.Contains("-c", launch.Args);
        Assert.Equal(project, launch.Cwd);
    }

    [Fact]
    public async Task ResumeUsesSessionId()
    {
        using var temp = new TempRoot();
        var project = temp.NewDir("proj");
        var exe = Path.Combine(temp.Root, "qwen.cmd");
        await File.WriteAllTextAsync(exe, "@echo off");

        var runner = new FakeProcessRunner();
        var service = new QwenService(temp.Paths, runner, NullLogger.Instance)
        {
            CurrentExecutableOverride = exe,
        };

        var sessionId = Guid.NewGuid().ToString();
        var result = await service.LaunchAsync(new ProjectEntry { Name = "X", Path = project }, LaunchMode.Continue, sessionId);

        Assert.True(result.Ok);
        Assert.Contains($"-r {sessionId}", runner.Detached[0].Args);
    }

    [Fact]
    public async Task DetectFindsVersionViaCmdShim()
    {
        using var temp = new TempRoot();
        var exe = Path.Combine(temp.Root, "qwen.cmd");
        await File.WriteAllTextAsync(exe, "@echo off");

        var runner = new FakeProcessRunner
        {
            Handler = (file, args) => file == "cmd.exe" && args.Contains("--version")
                ? new ProcessResult(0, "0.28.4\n", "")
                : new ProcessResult(1, "", "unexpected"),
        };

        var service = new QwenService(temp.Paths, runner, NullLogger.Instance)
        {
            CurrentExecutableOverride = exe,
        };
        var info = await service.DetectAsync();

        Assert.True(info.Found);
        Assert.Equal("0.28.4", info.Version);
        Assert.Equal(exe, info.ExecutablePath);
        Assert.Contains("/d /s /c", runner.Runs[0].Args);
    }

    [Fact]
    public async Task DetectReportsMissingQwenWithoutThrowing()
    {
        using var temp = new TempRoot();
        var service = new QwenService(temp.Paths, new FakeProcessRunner(), NullLogger.Instance)
        {
            CurrentExecutableOverride = @"Z:\nowhere\qwen.cmd",
        };

        var info = await service.DetectAsync();
        Assert.False(info.Found);
        Assert.NotNull(info.Error);
    }
}
