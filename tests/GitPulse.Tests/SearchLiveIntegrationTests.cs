using GitPulse.Services;
using GitPulse.Tests.TestHelpers;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

/// <summary>
/// Live GitHub Search checks against the real API.
/// Skipped unless <c>GITPULSE_TEST_PAT</c> is set. Not a substitute for Windows UI
/// navigation acceptance; covers API-level M9 gates from RestApi.md.
/// </summary>
/// <remarks>
/// PowerShell:
/// <code>
/// $env:GITPULSE_TEST_PAT = (Get-Content 'path\to\pat.txt' -Raw).Trim()
/// dotnet test tests/GitPulse.Tests --filter Category=Integration --configuration Release
/// Remove-Item Env:GITPULSE_TEST_PAT
/// </code>
/// </remarks>
public sealed class SearchLiveIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Token_AuthenticatesAgainstGitHubApi()
    {
        LiveGitHubTestGate.SkipIfUnavailable();

        using var client = await CreateFactory().CreateClientAsync(TestContext.Current.CancellationToken);
        using var response = await client.GetAsync("user", TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode, $"Expected 2xx from /user, got {(int)response.StatusCode}.");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SearchRepositories_ReturnsResultsAndTotalCount()
    {
        LiveGitHubTestGate.SkipIfUnavailable();
        using var vm = CreateViewModel();
        vm.SelectedType.Value = SearchType.Repositories;
        vm.Query.Value = "dotnet maui";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.True(string.IsNullOrEmpty(vm.ErrorMessage.Value), vm.ErrorMessage.Value);
        Assert.True(vm.HasSearched.Value);
        Assert.NotEmpty(vm.Repositories);
        Assert.True(vm.TotalCount.Value >= vm.Repositories.Count);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SearchIssues_ReturnsIssueItemsOnly()
    {
        LiveGitHubTestGate.SkipIfUnavailable();
        using var vm = CreateViewModel();
        vm.SelectedType.Value = SearchType.Issues;
        vm.Query.Value = "label:bug";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.True(string.IsNullOrEmpty(vm.ErrorMessage.Value), vm.ErrorMessage.Value);
        Assert.NotEmpty(vm.Issues);
        Assert.All(vm.Issues, item => Assert.Null(item.PullRequest));
        Assert.All(vm.Issues, item => Assert.False(string.IsNullOrWhiteSpace(item.RepositoryUrl)));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SearchPullRequests_ReturnsPullRequestItemsOnly()
    {
        LiveGitHubTestGate.SkipIfUnavailable();
        using var vm = CreateViewModel();
        vm.SelectedType.Value = SearchType.PullRequests;
        vm.Query.Value = "is:open";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.True(string.IsNullOrEmpty(vm.ErrorMessage.Value), vm.ErrorMessage.Value);
        Assert.NotEmpty(vm.PullRequests);
        Assert.All(vm.PullRequests, item => Assert.NotNull(item.PullRequest));
        Assert.All(vm.PullRequests, item => Assert.False(string.IsNullOrWhiteSpace(item.RepositoryUrl)));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SearchCode_ReturnsPathShaAndRepository()
    {
        LiveGitHubTestGate.SkipIfUnavailable();
        using var vm = CreateViewModel();
        vm.SelectedType.Value = SearchType.Code;
        vm.Query.Value = "UseR3 language:C#";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.True(string.IsNullOrEmpty(vm.ErrorMessage.Value), vm.ErrorMessage.Value);
        Assert.NotEmpty(vm.CodeResults);
        Assert.All(
            vm.CodeResults,
            item =>
            {
                Assert.False(string.IsNullOrWhiteSpace(item.Path));
                Assert.False(string.IsNullOrWhiteSpace(item.Sha));
                Assert.False(string.IsNullOrWhiteSpace(item.Repository.FullName));
            });
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Search_WithHashAndSpaces_DoesNotFailEncoding()
    {
        LiveGitHubTestGate.SkipIfUnavailable();
        using var vm = CreateViewModel();
        vm.SelectedType.Value = SearchType.Repositories;
        vm.Query.Value = "C# maui";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.True(string.IsNullOrEmpty(vm.ErrorMessage.Value), vm.ErrorMessage.Value);
        Assert.True(vm.HasSearched.Value);
        Assert.True(vm.TotalCount.Value >= 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Search_WithNoMatches_SetsEmptyState()
    {
        LiveGitHubTestGate.SkipIfUnavailable();
        using var vm = CreateViewModel();
        vm.SelectedType.Value = SearchType.Repositories;
        vm.Query.Value = "zzzxqnotexist999gitpulse";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.True(string.IsNullOrEmpty(vm.ErrorMessage.Value), vm.ErrorMessage.Value);
        Assert.True(vm.IsEmpty.Value);
        Assert.Empty(vm.Repositories);
        Assert.Equal(0, vm.TotalCount.Value);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Search_WithInvalidQuery_ShowsSyntaxError()
    {
        LiveGitHubTestGate.SkipIfUnavailable();
        using var vm = CreateViewModel();
        vm.SelectedType.Value = SearchType.Repositories;
        vm.Query.Value = "repo:";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Contains("rejected the search query", vm.ErrorMessage.Value);
        Assert.Empty(vm.Repositories);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SwitchingType_DoesNotAutoSearch()
    {
        LiveGitHubTestGate.SkipIfUnavailable();
        using var vm = CreateViewModel();
        vm.SelectedType.Value = SearchType.Repositories;
        vm.Query.Value = "dotnet";
        await vm.SearchCommand.ExecuteAsync(null);
        Assert.NotEmpty(vm.Repositories);

        var repoCount = vm.Repositories.Count;
        vm.SelectedType.Value = SearchType.Issues;
        await Task.Delay(300, TestContext.Current.CancellationToken);

        Assert.Equal(repoCount, vm.Repositories.Count);
        Assert.Empty(vm.Issues);
        Assert.True(string.IsNullOrEmpty(vm.ErrorMessage.Value), vm.ErrorMessage.Value);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task LoadMore_AppendsNextPageWhenAvailable()
    {
        LiveGitHubTestGate.SkipIfUnavailable();
        using var vm = CreateViewModel();
        vm.SelectedType.Value = SearchType.Repositories;
        // Broad query so GitHub returns more than one page (30/page).
        vm.Query.Value = "javascript";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.True(string.IsNullOrEmpty(vm.ErrorMessage.Value), vm.ErrorMessage.Value);
        Assert.SkipWhen(!vm.CanLoadMore.Value, "GitHub did not return a next Link for this query.");

        var firstPageCount = vm.Repositories.Count;
        await vm.LoadMoreCommand.ExecuteAsync(null);

        Assert.True(string.IsNullOrEmpty(vm.ErrorMessage.Value), vm.ErrorMessage.Value);
        Assert.True(vm.Repositories.Count > firstPageCount);
    }

    private static SearchViewModel CreateViewModel()
        => new(CreateFactory());

    private static GitHubClientFactory CreateFactory()
    {
        LiveGitHubTestGate.SkipIfUnavailable();
        return new GitHubClientFactory(new StaticCredentialStore(LiveGitHubTestGate.Token));
    }
}
