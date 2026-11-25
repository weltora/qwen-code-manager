using QwenCodeManager.Core.Models;
using QwenCodeManager.Core.Services;

namespace QwenCodeManager.Core.Tests;

public class GitServiceTests
{
    [Fact]
    public void PorcelainCountsModifiedStagedUntracked()
    {
        var status = new GitStatus();
        GitService.ParsePorcelain(
            "## main...origin/main [ahead 2, behind 1]\n" +
            " M file1.cs\n" +
            "M  file2.cs\n" +
            "MM file3.cs\n" +
            "?? new.txt\n" +
            "?? other.txt\n",
            status);

        Assert.Equal("main", status.Branch);
        Assert.Equal(2, status.Ahead);
        Assert.Equal(1, status.Behind);
        Assert.Equal(2, status.Modified);   // file1 (worktree) + file3 (both)
        Assert.Equal(2, status.Staged);     // file2 + file3
        Assert.Equal(2, status.Untracked);
        Assert.False(status.IsClean);
    }

    [Fact]
    public void CleanRepoHasNoChanges()
    {
        var status = new GitStatus { IsRepository = true };
        GitService.ParsePorcelain("## main...origin/main\n", status);
        Assert.True(status.IsClean);
        Assert.Equal("main", status.Branch);
    }

    [Theory]
    [InlineData("https://github.com/user/repo.git", "https://github.com/user/repo")]
    [InlineData("https://github.com/user/repo", "https://github.com/user/repo")]
    [InlineData("git@github.com:user/repo.git", "https://github.com/user/repo")]
    [InlineData("ssh://git@github.com/user/repo.git", "https://github.com/user/repo")]
    [InlineData("https://gitlab.com/user/repo.git", null)]
    [InlineData("not a url", null)]
    public void NormalizesGitHubUrls(string remote, string? expected)
    {
        Assert.Equal(expected, GitService.NormalizeGitHubUrl(remote));
    }

    [Fact]
    public async Task MissingDirectoryYieldsNoRepository()
    {
        var git = new GitService(new FakeProcessRunner(), NullLogger.Instance);
        var status = await git.GetStatusAsync(@"Z:\definitely\missing");
        Assert.False(status.IsRepository);
    }

    [Fact]
    public async Task GitFailureYieldsNoRepository()
    {
        var runner = new FakeProcessRunner
        {
            Handler = (_, _) => new ProcessResult(-1, "", "git not found"),
        };
        using var temp = new TempRoot();
        var dir = temp.NewDir("proj");

        var git = new GitService(runner, NullLogger.Instance);
        var status = await git.GetStatusAsync(dir);

        Assert.False(status.IsRepository);
    }

    [Fact]
    public async Task ReadsBranchAndRemote()
    {
        var runner = new FakeProcessRunner
        {
            Handler = (_, args) => args switch
            {
                "rev-parse --is-inside-work-tree" => new ProcessResult(0, "true\n", ""),
                "status --porcelain=v1 --branch" => new ProcessResult(0, "## dev\n M a.cs\n", ""),
                "remote get-url origin" => new ProcessResult(0, "git@github.com:me/app.git\n", ""),
                _ => new ProcessResult(1, "", "unexpected"),
            },
        };
        using var temp = new TempRoot();
        var dir = temp.NewDir("repo");

        var status = await new GitService(runner, NullLogger.Instance).GetStatusAsync(dir);

        Assert.True(status.IsRepository);
        Assert.Equal("dev", status.Branch);
        Assert.Equal(1, status.Modified);
        Assert.Equal("https://github.com/me/app", status.GitHubUrl);
    }
}
