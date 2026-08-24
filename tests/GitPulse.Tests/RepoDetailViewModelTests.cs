using System.Net;
using System.Text;
using GitPulse.Core.Models;
using GitPulse.Tests.TestHelpers;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class RepoDetailViewModelTests
{
    private const string Owner = "owner";
    private const string Repo = "repo";

    /// <summary>Repo JSON with standard fields.</summary>
    private const string RepoJson =
        "{\"id\":1,\"name\":\"repo\",\"full_name\":\"owner/repo\"," +
        "\"description\":\"A test repo\",\"html_url\":\"https://github.com/owner/repo\"," +
        "\"private\":false,\"default_branch\":\"main\"," +
        "\"stargazers_count\":42,\"forks_count\":3,\"open_issues_count\":5," +
        "\"updated_at\":\"2026-01-01T00:00:00Z\",\"clone_url\":\"https://github.com/owner/repo.git\",\"ssh_url\":\"git@github.com:owner/repo.git\",\"language\":\"C#\",\"license\":{\"key\":\"mit\",\"name\":\"MIT License\",\"spdx_id\":\"MIT\"},\"topics\":[\"maui\",\"github\"],\"homepage\":\"https://example.com\"}";

    /// <summary>Encode a markdown string as base64 for README content.</summary>
    private static string ReadmeJson(string markdown)
    {
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(markdown));
        return $"{{\"name\":\"README.md\",\"path\":\"README.md\",\"sha\":\"abc123\"," +
               $"\"size\":100,\"content\":\"{base64}\",\"encoding\":\"base64\"," +
               $"\"html_url\":\"https://github.com/owner/repo/blob/main/README.md\"}}";
    }

    private const string BranchesJson =
        "[{\"name\":\"main\",\"commit\":{\"sha\":\"abc123def456\",\"url\":\"https://api.github.com/repos/owner/repo/commits/abc123def456\"},\"protected\":false}," +
        "{\"name\":\"develop\",\"commit\":{\"sha\":\"789012abcdef\",\"url\":\"https://api.github.com/repos/owner/repo/commits/789012abcdef\"},\"protected\":true}]";

    private const string ReleasesJson =
        "[{\"id\":1,\"tag_name\":\"v1.0.0\",\"name\":\"Release 1.0\",\"body\":\"## What's new\\n\\nFirst release.\"," +
        "\"html_url\":\"https://github.com/owner/repo/releases/tag/v1.0.0\"," +
        "\"draft\":false,\"prerelease\":false," +
        "\"created_at\":\"2026-01-01T00:00:00Z\",\"published_at\":\"2026-01-01T00:00:00Z\"," +
        "\"author\":{\"login\":\"owner\"}}]";

    [Fact]
    public void Initialize_SetsOwnerRepoAndFullName()
    {
        var vm = new RepoDetailViewModel(
            new FakeGitHubClientFactory(new MockHttpHandler()),
            new FakeBrowserLauncher());

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
        var vm = new RepoDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize(Owner, Repo);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.Null(vm.Repo.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithToken_PopulatesRepoAndReadme()
    {
        var handler = new MockHttpHandler()
            .When($"/repos/{Owner}/{Repo}", RepoJson)
            .When($"/repos/{Owner}/{Repo}/readme", ReadmeJson("# Test README\n\nHello world."));
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new RepoDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize(Owner, Repo);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.NotNull(vm.Repo.Value);
        Assert.Equal("repo", vm.Repo.Value!.Name);
        Assert.Equal(42, vm.Repo.Value.StargazersCount);
        Assert.Equal("https://github.com/owner/repo.git", vm.Repo.Value.CloneUrl);
        Assert.Equal("C#", vm.Repo.Value.Language);
        Assert.Equal("MIT", vm.Repo.Value.License!.SpdxId);
        Assert.Equal(["maui", "github"], vm.Repo.Value.Topics);
        Assert.Equal("https://example.com", vm.Repo.Value.Homepage);
        Assert.Equal("git@github.com:owner/repo.git", vm.Repo.Value.SshUrl);
        Assert.True(vm.HasReadme.Value);
        Assert.Contains("Test README", vm.ReadmeMarkdown.Value);
        Assert.Contains("Hello world.", vm.ReadmeMarkdown.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithReadmeNotFound_SetsHasReadmeFalse()
    {
        // Only mock the repo endpoint — README will 404.
        var handler = new MockHttpHandler()
            .When($"/repos/{Owner}/{Repo}", RepoJson);
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new RepoDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize(Owner, Repo);

        await vm.LoadCommand.ExecuteAsync(null);

        // Repo should still load successfully.
        Assert.NotNull(vm.Repo.Value);
        // README should be absent but no error.
        Assert.False(vm.HasReadme.Value);
        Assert.Empty(vm.ReadmeMarkdown.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithEmptyOwner_DoesNothing()
    {
        var handler = new MockHttpHandler()
            .When($"/repos/{Owner}/{Repo}", RepoJson);
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new RepoDetailViewModel(factory, new FakeBrowserLauncher());
        // Don't call Initialize.

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Null(vm.Repo.Value);
        Assert.False(vm.IsLoading.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithApiError_SetsErrorMessage()
    {
        // No routes → 404 for everything.
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new RepoDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize(Owner, Repo);

        await vm.LoadCommand.ExecuteAsync(null);

        // The repo endpoint 404s (not just README), so the whole load fails.
        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.Contains("Load failed", vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task LoadBranches_PopulatesBranchesCollection()
    {
        var handler = new MockHttpHandler()
            .When($"/repos/{Owner}/{Repo}/branches", BranchesJson);
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new RepoDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize(Owner, Repo);

        await vm.LoadBranchesCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Branches.Count);
        Assert.Equal("main", vm.Branches[0].Name);
        Assert.False(vm.Branches[0].Protected);
        Assert.Equal("develop", vm.Branches[1].Name);
        Assert.True(vm.Branches[1].Protected);
        vm.Dispose();
    }

    [Fact]
    public async Task LoadBranches_LoadsOnlyOnce()
    {
        var callCount = 0;
        var handler = new MockHttpHandler()
            .When($"/repos/{Owner}/{Repo}/branches", req =>
            {
                callCount++;
                return new MockResponse(BranchesJson);
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new RepoDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize(Owner, Repo);

        await vm.LoadBranchesCommand.ExecuteAsync(null);
        await vm.LoadBranchesCommand.ExecuteAsync(null); // Should be a no-op.

        Assert.Equal(1, callCount);
        vm.Dispose();
    }

    [Fact]
    public async Task LoadReleases_PopulatesReleasesCollection()
    {
        var handler = new MockHttpHandler()
            .When($"/repos/{Owner}/{Repo}/releases", ReleasesJson);
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new RepoDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize(Owner, Repo);

        await vm.LoadReleasesCommand.ExecuteAsync(null);

        Assert.Single(vm.Releases);
        Assert.Equal("v1.0.0", vm.Releases[0].TagName);
        Assert.Equal("Release 1.0", vm.Releases[0].Name);
        Assert.Contains("First release", vm.Releases[0].Body);
        Assert.False(vm.Releases[0].Draft);
        Assert.False(vm.Releases[0].Prerelease);
        vm.Dispose();
    }

    [Fact]
    public async Task LoadReleases_LoadsOnlyOnce()
    {
        var callCount = 0;
        var handler = new MockHttpHandler()
            .When($"/repos/{Owner}/{Repo}/releases", req =>
            {
                callCount++;
                return new MockResponse(ReleasesJson);
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new RepoDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize(Owner, Repo);

        await vm.LoadReleasesCommand.ExecuteAsync(null);
        await vm.LoadReleasesCommand.ExecuteAsync(null); // Should be a no-op.

        Assert.Equal(1, callCount);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WhenStarred_SetsIsStarred()
    {
        var handler = new MockHttpHandler()
            .When($"/repos/{Owner}/{Repo}", RepoJson)
            .When($"/repos/{Owner}/{Repo}/readme", ReadmeJson("# hi"))
            .When($"/user/starred/{Owner}/{Repo}", _ =>
                new MockResponse("", StatusCode: HttpStatusCode.NoContent, AttachRequest: true));
        var vm = new RepoDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize(Owner, Repo);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.IsStarred.Value);
        Assert.Equal(42, vm.StarCount.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WhenNotStarred_SetsIsStarredFalse()
    {
        var handler = new MockHttpHandler()
            .When($"/repos/{Owner}/{Repo}", RepoJson)
            .When($"/repos/{Owner}/{Repo}/readme", ReadmeJson("# hi"))
            .When($"/user/starred/{Owner}/{Repo}", _ =>
                new MockResponse("", StatusCode: HttpStatusCode.NotFound, AttachRequest: true));
        var vm = new RepoDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize(Owner, Repo);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.IsStarred.Value);
        Assert.Empty(vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task ToggleStar_StarsAndIncrementsCount()
    {
        HttpRequestMessage? put = null;
        var handler = new MockHttpHandler()
            .When($"/repos/{Owner}/{Repo}", RepoJson)
            .When($"/repos/{Owner}/{Repo}/readme", ReadmeJson("# hi"))
            .When($"/user/starred/{Owner}/{Repo}", req =>
            {
                if (req.Method == HttpMethod.Put)
                {
                    put = req;
                    return new MockResponse("", StatusCode: HttpStatusCode.NoContent, AttachRequest: true);
                }

                return new MockResponse("", StatusCode: HttpStatusCode.NotFound, AttachRequest: true);
            });
        var vm = new RepoDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize(Owner, Repo);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.ToggleStarCommand.ExecuteAsync(null);

        Assert.NotNull(put);
        Assert.True(vm.IsStarred.Value);
        Assert.Equal(43, vm.StarCount.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task ToggleStar_UnstarsAndDecrementsCount()
    {
        HttpRequestMessage? delete = null;
        var handler = new MockHttpHandler()
            .When($"/repos/{Owner}/{Repo}", RepoJson)
            .When($"/repos/{Owner}/{Repo}/readme", ReadmeJson("# hi"))
            .When($"/user/starred/{Owner}/{Repo}", req =>
            {
                if (req.Method == HttpMethod.Delete)
                {
                    delete = req;
                    return new MockResponse("", StatusCode: HttpStatusCode.NoContent, AttachRequest: true);
                }

                return new MockResponse("", StatusCode: HttpStatusCode.NoContent, AttachRequest: true);
            });
        var vm = new RepoDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize(Owner, Repo);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.ToggleStarCommand.ExecuteAsync(null);

        Assert.NotNull(delete);
        Assert.False(vm.IsStarred.Value);
        Assert.Equal(41, vm.StarCount.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task ToggleStar_Forbidden_StaysOnPage()
    {
        var handler = new MockHttpHandler()
            .When($"/repos/{Owner}/{Repo}", RepoJson)
            .When($"/repos/{Owner}/{Repo}/readme", ReadmeJson("# hi"))
            .When($"/user/starred/{Owner}/{Repo}", req =>
            {
                if (req.Method == HttpMethod.Put)
                    return new MockResponse("{}", StatusCode: HttpStatusCode.Forbidden, AttachRequest: true);
                return new MockResponse("", StatusCode: HttpStatusCode.NotFound, AttachRequest: true);
            });
        var vm = new RepoDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize(Owner, Repo);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.ToggleStarCommand.ExecuteAsync(null);

        Assert.Contains("Not allowed", vm.ErrorMessage.Value);
        Assert.False(vm.IsStarred.Value);
        Assert.NotNull(vm.Repo.Value);
        vm.Dispose();
    }
    [Fact]
    public async Task Load_WhenWatching_SetsIsWatching()
    {
        var handler = WatchingHandler(watching: true);
        var vm = new RepoDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize(Owner, Repo);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.IsWatching.Value);
        Assert.Empty(vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WhenNotWatching_SetsIsWatchingFalse()
    {
        var handler = WatchingHandler(watching: false);
        var vm = new RepoDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize(Owner, Repo);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.IsWatching.Value);
        Assert.Empty(vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WhenIgnoring_SetsIsWatchingFalse()
    {
        var handler = new MockHttpHandler()
            .When($"/repos/{Owner}/{Repo}", RepoJson)
            .When($"/repos/{Owner}/{Repo}/readme", ReadmeJson("# hi"))
            .When($"/repos/{Owner}/{Repo}/subscription", _ =>
                new MockResponse(
                    "{\"subscribed\":false,\"ignored\":true}",
                    StatusCode: HttpStatusCode.OK,
                    AttachRequest: true));
        var vm = new RepoDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize(Owner, Repo);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.IsWatching.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task ToggleWatch_WatchesRepo()
    {
        HttpRequestMessage? put = null;
        var handler = new MockHttpHandler()
            .When($"/repos/{Owner}/{Repo}", RepoJson)
            .When($"/repos/{Owner}/{Repo}/readme", ReadmeJson("# hi"))
            .When($"/repos/{Owner}/{Repo}/subscription", req =>
            {
                if (req.Method == HttpMethod.Put)
                {
                    put = req;
                    return new MockResponse(
                        "{\"subscribed\":true,\"ignored\":false}",
                        StatusCode: HttpStatusCode.OK,
                        AttachRequest: true);
                }

                return new MockResponse("", StatusCode: HttpStatusCode.NotFound, AttachRequest: true);
            });
        var vm = new RepoDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize(Owner, Repo);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.ToggleWatchCommand.ExecuteAsync(null);

        Assert.NotNull(put);
        Assert.True(vm.IsWatching.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task ToggleWatch_UnwatchesRepo()
    {
        HttpRequestMessage? delete = null;
        var handler = new MockHttpHandler()
            .When($"/repos/{Owner}/{Repo}", RepoJson)
            .When($"/repos/{Owner}/{Repo}/readme", ReadmeJson("# hi"))
            .When($"/repos/{Owner}/{Repo}/subscription", req =>
            {
                if (req.Method == HttpMethod.Delete)
                {
                    delete = req;
                    return new MockResponse("", StatusCode: HttpStatusCode.NoContent, AttachRequest: true);
                }

                return new MockResponse(
                    "{\"subscribed\":true,\"ignored\":false}",
                    StatusCode: HttpStatusCode.OK,
                    AttachRequest: true);
            });
        var vm = new RepoDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize(Owner, Repo);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.ToggleWatchCommand.ExecuteAsync(null);

        Assert.NotNull(delete);
        Assert.False(vm.IsWatching.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task ToggleWatch_Forbidden_StaysOnPage()
    {
        var handler = new MockHttpHandler()
            .When($"/repos/{Owner}/{Repo}", RepoJson)
            .When($"/repos/{Owner}/{Repo}/readme", ReadmeJson("# hi"))
            .When($"/repos/{Owner}/{Repo}/subscription", req =>
            {
                if (req.Method == HttpMethod.Put)
                    return new MockResponse("{}", StatusCode: HttpStatusCode.Forbidden, AttachRequest: true);
                return new MockResponse("", StatusCode: HttpStatusCode.NotFound, AttachRequest: true);
            });
        var vm = new RepoDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize(Owner, Repo);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.ToggleWatchCommand.ExecuteAsync(null);

        Assert.Contains("Not allowed", vm.ErrorMessage.Value);
        Assert.False(vm.IsWatching.Value);
        Assert.NotNull(vm.Repo.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Fork_WhenAllowed_SetsForkedFullName()
    {
        HttpRequestMessage? post = null;
        var handler = new MockHttpHandler()
            .When($"/repos/{Owner}/{Repo}", RepoJson)
            .When($"/repos/{Owner}/{Repo}/readme", ReadmeJson("# hi"))
            .When($"/repos/{Owner}/{Repo}/forks", req =>
            {
                post = req;
                return new MockResponse(
                    "{\"id\":9,\"name\":\"repo\",\"full_name\":\"me/repo\"}",
                    StatusCode: HttpStatusCode.Accepted,
                    AttachRequest: true);
            });
        var vm = new RepoDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize(Owner, Repo);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.ForkCommand.ExecuteAsync(null);

        Assert.NotNull(post);
        Assert.Equal(HttpMethod.Post, post!.Method);
        Assert.Equal("me/repo", vm.ForkedFullName.Value);
        Assert.Empty(vm.ErrorMessage.Value);
        Assert.False(vm.IsForking.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Fork_WithoutToken_SetsErrorWithoutRequest()
    {
        var called = false;
        var handler = new MockHttpHandler()
            .When($"/repos/{Owner}/{Repo}/forks", _ =>
            {
                called = true;
                return new MockResponse("{}", StatusCode: HttpStatusCode.Accepted, AttachRequest: true);
            });
        var vm = new RepoDetailViewModel(
            new FakeGitHubClientFactory(handler, token: null), new FakeBrowserLauncher());
        vm.Initialize(Owner, Repo);

        await vm.ForkCommand.ExecuteAsync(null);

        Assert.Contains("No token", vm.ErrorMessage.Value);
        Assert.False(called);
        Assert.Empty(vm.ForkedFullName.Value);
        vm.Dispose();
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "Not allowed")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "could not fork")]
    public async Task Fork_HttpFailure_StaysOnPage(HttpStatusCode statusCode, string expected)
    {
        var handler = new MockHttpHandler()
            .When($"/repos/{Owner}/{Repo}", RepoJson)
            .When($"/repos/{Owner}/{Repo}/readme", ReadmeJson("# hi"))
            .When($"/repos/{Owner}/{Repo}/forks", _ =>
                new MockResponse("{}", StatusCode: statusCode, AttachRequest: true));
        var vm = new RepoDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize(Owner, Repo);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.ForkCommand.ExecuteAsync(null);

        Assert.Contains(expected, vm.ErrorMessage.Value, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(vm.Repo.Value);
        Assert.Empty(vm.ForkedFullName.Value);
        vm.Dispose();
    }

    private static MockHttpHandler WatchingHandler(bool watching)
    {
        return new MockHttpHandler()
            .When($"/repos/{Owner}/{Repo}", RepoJson)
            .When($"/repos/{Owner}/{Repo}/readme", ReadmeJson("# hi"))
            .When($"/repos/{Owner}/{Repo}/subscription", _ => watching
                ? new MockResponse(
                    "{\"subscribed\":true,\"ignored\":false}",
                    StatusCode: HttpStatusCode.OK,
                    AttachRequest: true)
                : new MockResponse("", StatusCode: HttpStatusCode.NotFound, AttachRequest: true));
    }
}
