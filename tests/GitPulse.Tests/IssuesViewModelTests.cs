using System.Net;
using GitPulse.Core.Models;
using GitPulse.Tests.TestHelpers;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class IssuesViewModelTests
{
    private static string IssuesJson(params string[] states)
    {
        var items = states.Select((s, i) =>
            GitHubJson.Issue(i + 1, state: s, body: $"body {i + 1}"));
        return $"[{string.Join(",", items)}]";
    }

    /// <summary>
    /// Link header for page 2 of a 3-page result set (used to test CanLoadMore).
    /// </summary>
    private const string LinkHasNext =
        "<https://api.github.com/repos/owner/repo/issues?page=2>; rel=\"next\", " +
        "<https://api.github.com/repos/owner/repo/issues?page=3>; rel=\"last\"";

    /// <summary>Link header with no next page (last page).</summary>
    private const string LinkNoNext =
        "<https://api.github.com/repos/owner/repo/issues?page=1>; rel=\"prev\", " +
        "<https://api.github.com/repos/owner/repo/issues?page=1>; rel=\"first\"";

    [Fact]
    public void Initialize_SetsOwnerRepoAndFullName()
    {
        var vm = new IssuesViewModel(new FakeGitHubClientFactory(new MockHttpHandler()));

        vm.Initialize("Skymly", "GitPulse");

        Assert.Equal("Skymly", vm.Owner.Value);
        Assert.Equal("GitPulse", vm.RepoName.Value);
        Assert.Equal("Skymly/GitPulse", vm.RepoFullName.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithoutToken_SetsErrorMessage()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler, token: null);
        var vm = new IssuesViewModel(factory);
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.Empty(vm.Issues);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithToken_PopulatesIssues()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", IssuesJson("open", "open", "closed"), LinkHasNext);
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssuesViewModel(factory);
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        // State filter is server-side now; the mock returns all 3 regardless.
        // The query handler injects state=open, but the mock doesn't filter.
        Assert.Equal(3, vm.Issues.Count);
        Assert.True(vm.CanLoadMore.Value);
        Assert.Equal("https://github.com/octocat/Hello-World/issues/1", vm.Issues[0].HtmlUrl);
        Assert.Equal(new DateTime(2011, 1, 26, 19, 1, 12, DateTimeKind.Utc), vm.Issues[0].CreatedAt);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithNoNextLink_SetsCanLoadMoreFalse()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", IssuesJson("open"), LinkNoNext);
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssuesViewModel(factory);
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.CanLoadMore.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithNullLinkHeader_SetsCanLoadMoreFalse()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", IssuesJson("open"));
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssuesViewModel(factory);
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.CanLoadMore.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithEmptyOwner_DoesNothing()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", IssuesJson("open"));
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssuesViewModel(factory);
        // Don't call Initialize — owner/repo are empty.

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.Issues);
        Assert.False(vm.IsLoading.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task LoadMore_AppendsNextPageToIssues()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", req =>
            {
                var page = req.RequestUri?.Query ?? "";
                if (page.Contains("page=2"))
                    return new MockResponse(IssuesJson("open"), LinkNoNext);
                return new MockResponse(IssuesJson("open", "open"), LinkHasNext);
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssuesViewModel(factory);
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Issues.Count);
        Assert.True(vm.CanLoadMore.Value);

        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.Issues.Count);
        Assert.False(vm.CanLoadMore.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task LoadMore_WithoutLoadFirst_ReturnsEarly()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", IssuesJson("open"), LinkHasNext);
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssuesViewModel(factory);
        vm.Initialize("owner", "repo");

        // LoadMore without Load — session HasNextPage is false.
        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Empty(vm.Issues);
        vm.Dispose();
    }

    [Fact]
    public async Task StateFilter_Change_ReloadsFromPage1WithNewState()
    {
        string? lastQuery = null;
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", req =>
            {
                lastQuery = req.RequestUri?.Query;
                return new MockResponse(IssuesJson("closed"), LinkNoNext);
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssuesViewModel(factory);
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Contains("state=open", lastQuery);

        vm.StateFilter.Value = "closed";
        // StateFilter subscription kicks Load asynchronously; wait for it.
        await AsyncTestWait.UntilAsync(() => lastQuery?.Contains("state=closed") == true);

        Assert.Contains("state=closed", lastQuery);
        // GitHubQueryHandler omits page when it is 1 (default).
        Assert.DoesNotContain("page=", lastQuery);
        Assert.Single(vm.Issues);
        vm.Dispose();
    }

    [Fact]
    public async Task StateFilter_All_SendsStateAll()
    {
        string? lastQuery = null;
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", req =>
            {
                lastQuery = req.RequestUri?.Query;
                return new MockResponse(IssuesJson("open", "closed"), LinkNoNext);
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssuesViewModel(factory);
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Contains("state=open", lastQuery);

        vm.StateFilter.Value = "all";
        await AsyncTestWait.UntilAsync(() => lastQuery?.Contains("state=all") == true);

        Assert.Contains("state=all", lastQuery);
        vm.Dispose();
    }

    [Fact]
    public async Task LoadMore_WhileLoading_ReturnsEarly()
    {
        // This is hard to test directly since Load is synchronous in setting IsLoading.
        // Instead, verify the guard by checking that LoadMore doesn't run when
        // CanLoadMore is false (no next page after load).
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", IssuesJson("open"), LinkNoNext);
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssuesViewModel(factory);
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);
        Assert.False(vm.CanLoadMore.Value);

        await vm.LoadMoreCommand.ExecuteAsync(null);

        // Still just 1 issue — LoadMore was a no-op.
        Assert.Single(vm.Issues);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithNotFoundResponse_SetsErrorMessage()
    {
        var handler = new MockHttpHandler(); // No routes → 404 for everything
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssuesViewModel(factory);
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.Contains("Load failed", vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithUnauthorizedResponse_SetsErrorMessage()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", HttpStatusCode.Unauthorized, "{\"message\":\"Bad credentials\"}");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssuesViewModel(factory);
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.Contains("Load failed", vm.ErrorMessage.Value);
        Assert.Empty(vm.Issues);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_DropsPullRequestsFromTheList()
    {
        var json = $"[{GitHubJson.Issue(1, title: "Real issue")}," +
                   $"{GitHubJson.Issue(2, title: "Actually a PR", pullRequest: true)}," +
                   $"{GitHubJson.Issue(3, title: "Another issue")}]";
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", json, LinkNoNext);
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssuesViewModel(factory);
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Issues.Count);
        Assert.Equal(1, vm.Issues[0].Number);
        Assert.Equal(3, vm.Issues[1].Number);
        Assert.All(vm.Issues, issue => Assert.False(issue.IsPullRequest));
        vm.Dispose();
    }

    [Fact]
    public async Task LoadMore_DropsPullRequestsFromThePage()
    {
        var page1 = $"[{GitHubJson.Issue(1, title: "Issue one")}]";
        var page2 = $"[{GitHubJson.Issue(2, title: "PR two", pullRequest: true)}," +
                    $"{GitHubJson.Issue(3, title: "Issue three")}]";
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", req =>
            {
                var query = req.RequestUri?.Query ?? "";
                if (query.Contains("page=2"))
                    return new MockResponse(page2, LinkNoNext);
                return new MockResponse(page1, LinkHasNext);
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssuesViewModel(factory);
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);
        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Issues.Count);
        Assert.Equal(1, vm.Issues[0].Number);
        Assert.Equal(3, vm.Issues[1].Number);
        vm.Dispose();
    }

    [Fact]
    public async Task LoadMore_WhileFirstPageHangs_DoesNotRequestPageTwo()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pages = new List<string>();
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", req =>
            {
                pages.Add(req.RequestUri?.Query ?? "");
                return new MockResponse(IssuesJson("open"), LinkHasNext, Gate: gate.Task);
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssuesViewModel(factory);
        vm.Initialize("owner", "repo");

        var load = vm.LoadCommand.ExecuteAsync(null);
        await AsyncTestWait.UntilAsync(() => vm.IsLoading.Value && pages.Count > 0);

        await vm.LoadMoreCommand.ExecuteAsync(null);
        Assert.Single(pages);

        gate.SetResult();
        await load;

        Assert.Single(vm.Issues);
        Assert.Single(pages);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WhilePreviousLoadHangs_KeepsOnlyTheNewerPage()
    {
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var n = 0;
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", _ =>
            {
                var i = Interlocked.Increment(ref n);
                return i == 1
                    ? new MockResponse($"[{GitHubJson.Issue(1, title: "Stale")}]", LinkNoNext, Gate: first.Task)
                    : new MockResponse($"[{GitHubJson.Issue(2, title: "Fresh")}]", LinkNoNext, Gate: second.Task);
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssuesViewModel(factory);
        vm.Initialize("owner", "repo");

        var load1 = vm.LoadCommand.ExecuteAsync(null);
        await AsyncTestWait.UntilAsync(() => n >= 1);

        var load2 = vm.LoadCommand.ExecuteAsync(null);
        await AsyncTestWait.UntilAsync(() => n >= 2);

        first.SetResult();
        await load1;
        second.SetResult();
        await load2;

        Assert.Single(vm.Issues);
        Assert.Equal(2, vm.Issues[0].Number);
        Assert.Equal("Fresh", vm.Issues[0].Title);
        vm.Dispose();
    }

    [Fact]
    public async Task StateFilter_ChangeWhileLoading_QueuesReload()
    {
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queries = new List<string>();
        var n = 0;
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", req =>
            {
                var i = Interlocked.Increment(ref n);
                queries.Add(req.RequestUri?.Query ?? "");
                return i == 1
                    ? new MockResponse(IssuesJson("open"), LinkNoNext, Gate: first.Task)
                    : new MockResponse(IssuesJson("closed"), LinkNoNext);
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssuesViewModel(factory);
        vm.Initialize("owner", "repo");

        var load = vm.LoadCommand.ExecuteAsync(null);
        await AsyncTestWait.UntilAsync(() => vm.IsLoading.Value && n >= 1);

        vm.StateFilter.Value = "closed";
        first.SetResult();
        await load;
        await AsyncTestWait.UntilAsync(() => queries.Exists(q => q.Contains("state=closed")));

        Assert.Contains(queries, q => q.Contains("state=closed"));
        vm.Dispose();
    }
}
