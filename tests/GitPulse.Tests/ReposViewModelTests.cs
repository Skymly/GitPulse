using GitPulse.Core.Models;
using GitPulse.Tests.TestHelpers;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class ReposViewModelTests
{
    private static string ReposJson(params (string name, string? desc)[] repos)
    {
        var items = repos.Select((r, i) =>
            $"{{\"id\":{i + 1},\"name\":\"{r.name}\"," +
            $"\"full_name\":\"owner/{r.name}\"," +
            $"\"description\":{JsonString(r.desc)}," +
            $"\"html_url\":\"https://github.com/owner/{r.name}\"," +
            $"\"private\":false,\"stargazers_count\":{i}," +
            $"\"forks_count\":0,\"open_issues_count\":0}}");
        return $"[{string.Join(",", items)}]";
    }

    private static string JsonString(string? s) =>
        s is null ? "null" : $"\"{s}\"";

    private const string LinkHasNext =
        "<https://api.github.com/user/repos?page=2>; rel=\"next\", " +
        "<https://api.github.com/user/repos?page=3>; rel=\"last\"";

    private const string LinkNoNext =
        "<https://api.github.com/user/repos?page=1>; rel=\"prev\", " +
        "<https://api.github.com/user/repos?page=1>; rel=\"first\"";

    [Fact]
    public async Task Load_WithoutToken_SetsErrorMessageAndNotAuthenticated()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler, token: null);
        var vm = new ReposViewModel(factory);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.False(vm.IsAuthenticated.Value);
        Assert.Empty(vm.Repos);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithToken_PopulatesReposAndCanLoadMore()
    {
        var handler = new MockHttpHandler()
            .When("/user/repos",
                ReposJson(("gitpulse", "A GitHub client"), ("observables", "Reactive bridges")),
                LinkHasNext);
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new ReposViewModel(factory);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.True(vm.IsAuthenticated.Value);
        Assert.Equal(2, vm.Repos.Count);
        Assert.True(vm.CanLoadMore.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithNoNextLink_SetsCanLoadMoreFalse()
    {
        var handler = new MockHttpHandler()
            .When("/user/repos", ReposJson(("repo1", null)), LinkNoNext);
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new ReposViewModel(factory);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.CanLoadMore.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithNullLinkHeader_SetsCanLoadMoreFalse()
    {
        var handler = new MockHttpHandler()
            .When("/user/repos", ReposJson(("repo1", null)));
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new ReposViewModel(factory);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.CanLoadMore.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task LoadMore_WithoutLoadFirst_ReturnsEarly()
    {
        var handler = new MockHttpHandler()
            .When("/user/repos", ReposJson(("repo1", null)), LinkHasNext);
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new ReposViewModel(factory);

        // LoadMore without Load — _hasNextPage is false, so nothing happens.
        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Empty(vm.Repos);
        vm.Dispose();
    }

    [Fact]
    public async Task LoadMore_AppendsNextPageToRepos()
    {
        var handler = new MockHttpHandler()
            .When("/user/repos", req =>
            {
                // Page 1 has next, page 2 does not.
                var page = req.RequestUri?.Query ?? "";
                if (page.Contains("page=2"))
                    return new MockResponse(ReposJson(("repo3", null)), LinkNoNext);
                return new MockResponse(ReposJson(("repo1", null), ("repo2", null)), LinkHasNext);
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new ReposViewModel(factory);

        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Repos.Count);

        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.Repos.Count);
        Assert.False(vm.CanLoadMore.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task SearchText_FiltersReposByName()
    {
        var handler = new MockHttpHandler()
            .When("/user/repos",
                ReposJson(("gitpulse", "GitHub client"), ("observables", "Reactive"), ("other", null)),
                LinkNoNext);
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new ReposViewModel(factory);

        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(3, vm.Repos.Count);

        vm.SearchText.Value = "obs";

        Assert.Single(vm.Repos);
        Assert.Equal("observables", vm.Repos[0].Name);
        vm.Dispose();
    }

    [Fact]
    public async Task SearchText_FiltersReposByDescription()
    {
        var handler = new MockHttpHandler()
            .When("/user/repos",
                ReposJson(("gitpulse", "A GitHub client"), ("obs", "Reactive bridges")),
                LinkNoNext);
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new ReposViewModel(factory);

        await vm.LoadCommand.ExecuteAsync(null);

        vm.SearchText.Value = "reactive";

        Assert.Single(vm.Repos);
        Assert.Equal("obs", vm.Repos[0].Name);
        vm.Dispose();
    }

    [Fact]
    public async Task SearchText_EmptyString_RestoresAllRepos()
    {
        var handler = new MockHttpHandler()
            .When("/user/repos",
                ReposJson(("gitpulse", "GitHub client"), ("obs", "Reactive")),
                LinkNoNext);
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new ReposViewModel(factory);

        await vm.LoadCommand.ExecuteAsync(null);
        vm.SearchText.Value = "git";
        Assert.Single(vm.Repos);

        vm.SearchText.Value = "";

        Assert.Equal(2, vm.Repos.Count);
        vm.Dispose();
    }
}
