using System.Net;
using GitPulse.Tests.TestHelpers;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class CommitDetailViewModelTests
{
    private const string Sha = "abc1234deadbeef";

    private const string CommitJson = """
        {
          "sha": "abc1234deadbeef",
          "html_url": "https://github.com/owner/repo/commit/abc1234deadbeef",
          "commit": {
            "message": "Fix bugs\n\nmore detail",
            "author": { "name": "Ada", "date": "2026-08-18T00:00:00Z" }
          },
          "stats": { "additions": 5, "deletions": 2, "total": 7 },
          "files": [
            {
              "sha": "blob1",
              "filename": "src/Program.cs",
              "status": "modified",
              "additions": 5,
              "deletions": 2,
              "changes": 7,
              "blob_url": "https://github.com/owner/repo/blob/src/Program.cs",
              "raw_url": "https://github.com/owner/repo/raw/src/Program.cs",
              "contents_url": "https://api.github.com/repos/owner/repo/contents/src/Program.cs",
              "patch": "@@ -1 +1 @@\n-a\n+b"
            },
            {
              "sha": "blob2",
              "filename": "logo.png",
              "status": "added",
              "additions": 0,
              "deletions": 0,
              "changes": 0,
              "blob_url": "https://github.com/owner/repo/blob/logo.png",
              "raw_url": "https://github.com/owner/repo/raw/logo.png",
              "contents_url": "https://api.github.com/repos/owner/repo/contents/logo.png",
              "patch": null
            }
          ]
        }
        """;

    private const string EmptyFilesJson = """
        {
          "sha": "abc1234deadbeef",
          "html_url": "https://github.com/owner/repo/commit/abc1234deadbeef",
          "commit": {
            "message": "Empty change",
            "author": { "name": "Ada", "date": "2026-08-18T00:00:00Z" }
          },
          "stats": { "additions": 0, "deletions": 0, "total": 0 },
          "files": []
        }
        """;

    [Fact]
    public void Initialize_SetsOwnerRepoAndSha()
    {
        using var vm = new CommitDetailViewModel(
            new FakeGitHubClientFactory(new MockHttpHandler()), new FakeBrowserLauncher());
        vm.Initialize("Skymly", "GitPulse", Sha);

        Assert.Equal("Skymly/GitPulse", vm.RepoFullName.Value);
        Assert.Equal(Sha, vm.Sha.Value);
    }

    [Fact]
    public async Task Load_WithoutOwnerRepoSha_DoesNothing()
    {
        using var vm = new CommitDetailViewModel(
            new FakeGitHubClientFactory(new MockHttpHandler()), new FakeBrowserLauncher());

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.Files);
        Assert.Empty(vm.ErrorMessage.Value);
    }

    [Fact]
    public async Task Load_MissingSha_DoesNotCallApi()
    {
        var called = false;
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/commits", _ =>
            {
                called = true;
                return new MockResponse("{}", StatusCode: HttpStatusCode.InternalServerError);
            });
        using var vm = new CommitDetailViewModel(
            new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", "");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(called);
        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Empty(vm.Files);
    }

    [Fact]
    public async Task Load_WithoutToken_SetsErrorMessage()
    {
        using var vm = new CommitDetailViewModel(
            new FakeGitHubClientFactory(new MockHttpHandler(), token: null),
            new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", Sha);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Contains("No token", vm.ErrorMessage.Value);
        Assert.Contains("Settings", vm.ErrorMessage.Value);
        Assert.Empty(vm.Files);
        Assert.Null(vm.Commit.Value);
    }

    [Fact]
    public async Task Load_PopulatesMessageStatsAndFiles()
    {
        var handler = new MockHttpHandler()
            .When($"/repos/owner/repo/commits/{Sha}", CommitJson);
        using var vm = new CommitDetailViewModel(
            new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", Sha);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal("Fix bugs\n\nmore detail", vm.Message.Value);
        Assert.Equal(Sha, vm.Sha.Value);
        Assert.Equal("Ada", vm.AuthorName.Value);
        Assert.Equal(5, vm.Additions.Value);
        Assert.Equal(2, vm.Deletions.Value);
        Assert.Equal(7, vm.Total.Value);
        Assert.Equal(2, vm.Files.Count);
        Assert.Equal("src/Program.cs", vm.Files[0].Filename);
        Assert.False(string.IsNullOrEmpty(vm.Files[0].Patch));
        Assert.Equal("https://github.com/owner/repo/commit/abc1234deadbeef", vm.HtmlUrl.Value);
    }

    [Fact]
    public async Task Load_FileWithNullPatch_DoesNotFailPage()
    {
        var handler = new MockHttpHandler()
            .When($"/repos/owner/repo/commits/{Sha}", CommitJson);
        using var vm = new CommitDetailViewModel(
            new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", Sha);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal(2, vm.Files.Count);
        Assert.Equal("logo.png", vm.Files[1].Filename);
        Assert.Null(vm.Files[1].Patch);
    }

    [Fact]
    public async Task Load_EmptyFiles_IsQuiet()
    {
        var handler = new MockHttpHandler()
            .When($"/repos/owner/repo/commits/{Sha}", EmptyFilesJson);
        using var vm = new CommitDetailViewModel(
            new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", Sha);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Empty(vm.Files);
        Assert.Equal(0, vm.Additions.Value);
        Assert.Equal(0, vm.Deletions.Value);
        Assert.Equal(0, vm.Total.Value);
        Assert.Equal("Empty change", vm.Message.Value);
    }

    [Fact]
    public async Task Load_NotFound_ShowsErrorAndKeepsIsolation()
    {
        var handler = new MockHttpHandler()
            .When($"/repos/owner/repo/commits/{Sha}", HttpStatusCode.NotFound);
        using var vm = new CommitDetailViewModel(
            new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", Sha);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.Empty(vm.Files);
        Assert.Null(vm.Commit.Value);
    }

    [Fact]
    public async Task Load_RequestsCommitShaWithoutPageQuery()
    {
        HttpRequestMessage? seen = null;
        var handler = new MockHttpHandler()
            .When($"/repos/owner/repo/commits/{Sha}", req =>
            {
                seen = req;
                return new MockResponse(CommitJson);
            });
        using var vm = new CommitDetailViewModel(
            new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", Sha);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.NotNull(seen);
        Assert.Equal($"/repos/owner/repo/commits/{Sha}", seen!.RequestUri!.AbsolutePath);
        Assert.DoesNotContain("page", seen.RequestUri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("per_page", seen.RequestUri.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpenInBrowser_RecordsHtmlUrl()
    {
        var browser = new FakeBrowserLauncher();
        using var vm = new CommitDetailViewModel(
            new FakeGitHubClientFactory(new MockHttpHandler()), browser);

        await vm.OpenInBrowserCommand.ExecuteAsync("https://github.com/o/r/commit/abc");

        Assert.Equal("https://github.com/o/r/commit/abc", browser.OpenedUrls.Single());
    }
}
