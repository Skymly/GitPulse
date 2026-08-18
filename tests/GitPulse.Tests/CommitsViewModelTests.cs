using System.Net;
using GitPulse.Tests.TestHelpers;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class CommitsViewModelTests
{
    private static string CommitsJson(params (string sha, string message, string author)[] commits)
    {
        var items = commits.Select(c =>
            $"{{\"sha\":\"{c.sha}\",\"html_url\":\"https://github.com/o/r/commit/{c.sha}\"," +
            $"\"commit\":{{\"message\":\"{c.message}\",\"author\":{{\"name\":\"{c.author}\"," +
            $"\"date\":\"2026-08-18T00:00:00Z\"}}}}}}");
        return $"[{string.Join(",", items)}]";
    }

    private const string LinkHasNext =
        "<https://api.github.com/repos/owner/repo/commits?page=2>; rel=\"next\"";

    [Fact]
    public void Initialize_SetsOwnerRepoAndFullName()
    {
        using var vm = new CommitsViewModel(
            new FakeGitHubClientFactory(new MockHttpHandler()), new FakeBrowserLauncher());
        vm.Initialize("Skymly", "GitPulse");

        Assert.Equal("Skymly", vm.Owner.Value);
        Assert.Equal("GitPulse", vm.RepoName.Value);
        Assert.Equal("Skymly/GitPulse", vm.RepoFullName.Value);
    }

    [Fact]
    public async Task Load_WithoutOwnerRepo_DoesNothing()
    {
        using var vm = new CommitsViewModel(
            new FakeGitHubClientFactory(new MockHttpHandler()), new FakeBrowserLauncher());

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.Commits);
        Assert.Empty(vm.ErrorMessage.Value);
    }

    [Fact]
    public async Task Load_WithoutToken_SetsErrorMessage()
    {
        using var vm = new CommitsViewModel(
            new FakeGitHubClientFactory(new MockHttpHandler(), token: null),
            new FakeBrowserLauncher());
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Contains("No token", vm.ErrorMessage.Value);
        Assert.Empty(vm.Commits);
    }

    [Fact]
    public async Task Load_WithToken_PopulatesCommitsAndCanLoadMore()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/commits",
                CommitsJson(("abc1234deadbeef", "Fix bugs\\nmore", "Ada")),
                LinkHasNext);
        using var vm = new CommitsViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Single(vm.Commits);
        Assert.Equal("abc1234deadbeef", vm.Commits[0].Sha);
        Assert.StartsWith("Fix bugs", vm.Commits[0].Commit!.Message);
        Assert.True(vm.CanLoadMore.Value);
    }

    [Fact]
    public async Task Load_EmptyList_IsQuiet()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/commits", "[]");
        using var vm = new CommitsViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Empty(vm.Commits);
        Assert.False(vm.CanLoadMore.Value);
    }

    [Fact]
    public async Task Load_NotFound_ShowsErrorAndKeepsPage()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/commits", HttpStatusCode.NotFound);
        using var vm = new CommitsViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.Empty(vm.Commits);
    }

    [Fact]
    public async Task LoadMore_AppendsNextPage()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/commits", req =>
            {
                var page = req.RequestUri?.Query ?? "";
                if (page.Contains("page=2"))
                    return new MockResponse(CommitsJson(("bbb2222", "Second", "Bob")));
                return new MockResponse(CommitsJson(("aaa1111", "First", "Ada")), LinkHasNext);
            });
        using var vm = new CommitsViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);
        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Commits.Count);
        Assert.Equal("bbb2222", vm.Commits[1].Sha);
        Assert.False(vm.CanLoadMore.Value);
    }

    [Fact]
    public async Task OpenInBrowser_RecordsHtmlUrl()
    {
        var browser = new FakeBrowserLauncher();
        using var vm = new CommitsViewModel(
            new FakeGitHubClientFactory(new MockHttpHandler()), browser);

        await vm.OpenInBrowserCommand.ExecuteAsync("https://github.com/o/r/commit/abc");

        Assert.Equal("https://github.com/o/r/commit/abc", browser.OpenedUrls.Single());
    }
}
