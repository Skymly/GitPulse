using System.Net;
using GitPulse.Tests.TestHelpers;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class CheckRunDetailViewModelTests
{
    private const string CheckRunJson = """
        {
          "id": 4,
          "name": "build",
          "status": "completed",
          "conclusion": "failure",
          "html_url": "https://github.com/owner/repo/runs/4",
          "output": {
            "title": "Build failed",
            "summary": "1 error",
            "text": "csc: error CS0001",
            "annotations_count": 2
          }
        }
        """;

    [Fact]
    public void Initialize_SetsRepoFullName()
    {
        using var vm = new CheckRunDetailViewModel(
            new FakeGitHubClientFactory(new MockHttpHandler()), new FakeBrowserLauncher());
        vm.Initialize("Skymly", "GitPulse", 4);

        Assert.Equal("Skymly/GitPulse", vm.RepoFullName.Value);
    }

    [Fact]
    public async Task Load_WithoutId_DoesNothing()
    {
        var called = false;
        var handler = new MockHttpHandler()
            .When("/check-runs/4", _ =>
            {
                called = true;
                return new MockResponse(CheckRunJson);
            });
        using var vm = new CheckRunDetailViewModel(
            new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(called);
        Assert.Null(vm.CheckRun.Value);
    }

    [Fact]
    public async Task Load_WithoutToken_SetsErrorWithoutRequest()
    {
        var called = false;
        var handler = new MockHttpHandler()
            .When("/check-runs/4", _ =>
            {
                called = true;
                return new MockResponse(CheckRunJson);
            });
        using var vm = new CheckRunDetailViewModel(
            new FakeGitHubClientFactory(handler, token: null), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 4);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Contains("No token", vm.ErrorMessage.Value);
        Assert.False(called);
    }

    [Fact]
    public async Task Load_PopulatesOutputAndDoesNotSendPageQuery()
    {
        HttpRequestMessage? seen = null;
        var handler = new MockHttpHandler()
            .When("/check-runs/4", req =>
            {
                seen = req;
                return new MockResponse(CheckRunJson);
            });
        using var vm = new CheckRunDetailViewModel(
            new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 4);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal("build", vm.Name.Value);
        Assert.Equal("completed", vm.Status.Value);
        Assert.Equal("failure", vm.Conclusion.Value);
        Assert.Equal("Build failed", vm.OutputTitle.Value);
        Assert.Equal("1 error", vm.OutputSummary.Value);
        Assert.Equal("csc: error CS0001", vm.OutputText.Value);
        Assert.NotNull(seen);
        Assert.Equal("/repos/owner/repo/check-runs/4", seen!.RequestUri!.AbsolutePath);
        Assert.DoesNotContain("page", seen.RequestUri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("per_page", seen.RequestUri.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Load_NotFound_StaysOnPage()
    {
        var handler = new MockHttpHandler()
            .When("/check-runs/4", HttpStatusCode.NotFound);
        using var vm = new CheckRunDetailViewModel(
            new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 4);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Contains("Load failed", vm.ErrorMessage.Value);
        Assert.Null(vm.CheckRun.Value);
    }

    [Fact]
    public async Task OpenInBrowser_RecordsHtmlUrl()
    {
        var launcher = new FakeBrowserLauncher();
        var handler = new MockHttpHandler()
            .When("/check-runs/4", CheckRunJson);
        using var vm = new CheckRunDetailViewModel(
            new FakeGitHubClientFactory(handler), launcher);
        vm.Initialize("owner", "repo", 4);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.OpenInBrowserCommand.ExecuteAsync(vm.HtmlUrl.Value);

        Assert.Equal("https://github.com/owner/repo/runs/4", launcher.OpenedUrls.Single());
    }

    private const string AnnotationsJson = """
        [
          {
            "path": "src/Program.cs",
            "start_line": 10,
            "end_line": 12,
            "annotation_level": "failure",
            "message": "null ref",
            "title": "CS0001"
          }
        ]
        """;

    [Fact]
    public async Task Load_ListsAnnotations()
    {
        var handler = new MockHttpHandler()
            .When("/check-runs/4", CheckRunJson)
            .When("/check-runs/4/annotations", AnnotationsJson);
        using var vm = new CheckRunDetailViewModel(
            new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 4);

        await vm.LoadCommand.ExecuteAsync(null);

        var item = Assert.Single(vm.Annotations);
        Assert.Equal("src/Program.cs", item.Path);
        Assert.Equal(10, item.StartLine);
        Assert.Equal("failure", item.AnnotationLevel);
        Assert.Equal("null ref", item.Message);
        Assert.Empty(vm.ErrorMessage.Value);
    }

    [Fact]
    public async Task Load_WhenAnnotationsMissing_KeepsCheckRun()
    {
        using var vm = new CheckRunDetailViewModel(
            new FakeGitHubClientFactory(new MockHttpHandler()
                .When("/check-runs/4", CheckRunJson)),
            new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 4);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("build", vm.Name.Value);
        Assert.Empty(vm.Annotations);
        Assert.Empty(vm.ErrorMessage.Value);
    }

    [Fact]
    public async Task Rerequest_WhenAllowed_StaysQuiet()
    {
        HttpRequestMessage? post = null;
        var handler = new MockHttpHandler()
            .When("/check-runs/4", CheckRunJson)
            .When("/check-runs/4/rerequest", req =>
            {
                post = req;
                return new MockResponse("", StatusCode: HttpStatusCode.Created, AttachRequest: true);
            });
        using var vm = new CheckRunDetailViewModel(
            new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 4);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.RerequestCommand.ExecuteAsync(null);

        Assert.NotNull(post);
        Assert.Equal(HttpMethod.Post, post!.Method);
        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal("build", vm.Name.Value);
        Assert.False(vm.IsRerequesting.Value);
    }

    [Fact]
    public async Task Rerequest_WithoutToken_SetsErrorWithoutRequest()
    {
        var called = false;
        var handler = new MockHttpHandler()
            .When("/check-runs/4/rerequest", _ =>
            {
                called = true;
                return new MockResponse("", StatusCode: HttpStatusCode.Created, AttachRequest: true);
            });
        using var vm = new CheckRunDetailViewModel(
            new FakeGitHubClientFactory(handler, token: null), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 4);

        await vm.RerequestCommand.ExecuteAsync(null);

        Assert.Contains("No token", vm.ErrorMessage.Value);
        Assert.False(called);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "Not allowed")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "cannot rerequest")]
    [InlineData(HttpStatusCode.InternalServerError, "Rerequest failed")]
    public async Task Rerequest_HttpFailure_StaysOnPage(
        HttpStatusCode statusCode,
        string expectedMessage)
    {
        var handler = new MockHttpHandler()
            .When("/check-runs/4", CheckRunJson)
            .When("/check-runs/4/rerequest", _ =>
                new MockResponse("{}", StatusCode: statusCode, AttachRequest: true));
        using var vm = new CheckRunDetailViewModel(
            new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 4);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.RerequestCommand.ExecuteAsync(null);

        Assert.Contains(expectedMessage, vm.ErrorMessage.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("build", vm.Name.Value);
        Assert.False(vm.IsRerequesting.Value);
    }
}
