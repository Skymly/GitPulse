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
            $"{{\"number\":{i + 1},\"title\":\"Issue {i + 1}\",\"state\":\"{s}\"," +
            $"\"body\":\"body {i + 1}\",\"user\":{{\"login\":\"alice\"}}}}");
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
}
