using GitPulse.Tests.TestHelpers;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class PullRequestsViewModelTests
{
    private static string PrsJson(params (string state, bool draft, bool merged)[] prs)
    {
        var items = prs.Select((p, i) =>
            $"{{\"number\":{i + 100},\"title\":\"PR {i + 1}\",\"state\":\"{p.state}\"," +
            $"\"draft\":{p.draft.ToString().ToLower()}," +
            $"\"merged\":{p.merged.ToString().ToLower()}," +
            $"\"headRef\":\"feature-{i + 1}\",\"baseRef\":\"main\"," +
            $"\"user\":{{\"login\":\"bob\"}}}}");
        return $"[{string.Join(",", items)}]";
    }

    [Fact]
    public void Initialize_SetsOwnerRepoAndFullName()
    {
        var vm = new PullRequestsViewModel(new FakeGitHubClientFactory(new MockHttpHandler()));

        vm.Initialize("Skymly", "Observables");

        Assert.Equal("Skymly", vm.Owner.Value);
        Assert.Equal("Observables", vm.RepoName.Value);
        Assert.Equal("Skymly/Observables", vm.RepoFullName.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithToken_PopulatesPullRequests()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/pulls",
                PrsJson(("open", false, false), ("closed", false, true), ("open", true, false)));
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestsViewModel(factory);
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        // Default filter is "open" → 2 open PRs (including the draft).
        Assert.Equal(2, vm.PullRequests.Count);
        Assert.Equal("PR 1", vm.PullRequests[0].Title);
        Assert.True(vm.PullRequests[1].Draft);
        vm.Dispose();
    }

    [Fact]
    public async Task StateFilter_Closed_ShowsOnlyClosed()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/pulls",
                PrsJson(("open", false, false), ("closed", false, true)));
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestsViewModel(factory);
        vm.Initialize("owner", "repo");
        await vm.LoadCommand.ExecuteAsync(null);

        vm.StateFilter.Value = "closed";

        Assert.Single(vm.PullRequests);
        Assert.True(vm.PullRequests[0].Merged);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithoutToken_SetsErrorMessage()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler, token: null);
        var vm = new PullRequestsViewModel(factory);
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.Empty(vm.PullRequests);
        vm.Dispose();
    }
}
