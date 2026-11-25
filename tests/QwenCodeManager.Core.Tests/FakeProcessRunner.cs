using QwenCodeManager.Core.Infrastructure;

namespace QwenCodeManager.Core.Tests;

/// <summary>Canned process responses; records invocations. Never starts real processes.</summary>
public sealed class FakeProcessRunner : IProcessRunner
{
    public Func<string, string, ProcessResult> Handler { get; set; } = (_, _) => new ProcessResult(-1, "", "not configured");
    public List<(string File, string Args, string? Cwd)> Runs { get; } = new();
    public List<(string File, string Args, string? Cwd)> Detached { get; } = new();

    public Task<ProcessResult> RunAsync(string fileName, string arguments, string? workingDirectory = null, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        Runs.Add((fileName, arguments, workingDirectory));
        return Task.FromResult(Handler(fileName, arguments));
    }

    public int StartDetached(string fileName, string arguments, string? workingDirectory = null)
    {
        Detached.Add((fileName, arguments, workingDirectory));
        return 4242;
    }
}
