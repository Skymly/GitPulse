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
            .When("/repos/owner/repo/issues", IssuesJson("open", "closed", "open"));
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssuesViewModel(factory);
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        // Default filter is "open" → only the 2 open issues appear.
        Assert.Equal(2, vm.Issues.Count);
        Assert.Equal("Issue 1", vm.Issues[0].Title);
        Assert.Equal("Issue 3", vm.Issues[1].Title);
        vm.Dispose();
    }

    [Fact]
    public async Task StateFilter_All_ShowsEveryIssue()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", IssuesJson("open", "closed", "open"));
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssuesViewModel(factory);
        vm.Initialize("owner", "repo");
        await vm.LoadCommand.ExecuteAsync(null);

        // Switch to "all" — reactive subscription re-filters.
        vm.StateFilter.Value = "all";

        Assert.Equal(3, vm.Issues.Count);
        vm.Dispose();
    }

    [Fact]
    public async Task StateFilter_Closed_ShowsOnlyClosed()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", IssuesJson("open", "closed", "open"));
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssuesViewModel(factory);
        vm.Initialize("owner", "repo");
        await vm.LoadCommand.ExecuteAsync(null);

        vm.StateFilter.Value = "closed";

        Assert.Single(vm.Issues);
        Assert.Equal("closed", vm.Issues[0].State);
        vm.Dispose();
    }
}
