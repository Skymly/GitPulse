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

    private const string LinkHasNext =
        "<https://api.github.com/repos/owner/repo/pulls?page=2>; rel=\"next\", " +
        "<https://api.github.com/repos/owner/repo/pulls?page=5>; rel=\"last\"";

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
    public async Task Load_WithToken_PopulatesPullRequestsAndCanLoadMore()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/pulls",
                PrsJson(("open", false, false), ("closed", false, true), ("open", true, false)),
                LinkHasNext);
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestsViewModel(factory);
        vm.Initialize("owner", "repo");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal(3, vm.PullRequests.Count);
        Assert.True(vm.CanLoadMore.Value);
        Assert.True(vm.PullRequests[2].Draft);
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
