using GitPulse.Core.Models;
using GitPulse.Tests.TestHelpers;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class CreateIssueViewModelTests
{
    private static string IssueJson(int number, string title) =>
        GitHubJson.Issue(number, body: "", title: title);

    [Fact]
    public void Initialize_SetsOwnerAndRepo()
    {
        var vm = new CreateIssueViewModel(new FakeGitHubClientFactory(new MockHttpHandler()));
        vm.Initialize("Skymly", "GitPulse");
        // No public Owner/Repo properties to assert, but Create should work.
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WithEmptyTitle_DoesNothing()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreateIssueViewModel(factory);
        vm.Initialize("owner", "repo");

        vm.TitleInput.Value = "   ";
        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Null(vm.CreatedIssueNumber.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WithValidTitle_SetsCreatedIssueNumber()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", req =>
            {
                if (req.Method == HttpMethod.Post)
                    return new MockResponse(IssueJson(99, "My new issue"));
                return new MockResponse("[]");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreateIssueViewModel(factory);
        vm.Initialize("owner", "repo");

        vm.TitleInput.Value = "My new issue";
        vm.BodyInput.Value = "This is the body";
        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal(99, vm.CreatedIssueNumber.Value);
        Assert.Equal(0, factory.LiveClients);
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WithoutToken_SetsErrorMessage()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler, token: null);
        var vm = new CreateIssueViewModel(factory);
        vm.Initialize("owner", "repo");

        vm.TitleInput.Value = "Test";
        await vm.CreateCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.Null(vm.CreatedIssueNumber.Value);
        Assert.Equal(0, factory.LiveClients);
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WithLabels_SetsCreatedIssueNumber()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", req =>
            {
                if (req.Method == HttpMethod.Post)
                    return new MockResponse(IssueJson(77, "With labels"));
                return new MockResponse("[]");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreateIssueViewModel(factory);
        vm.Initialize("owner", "repo");

        vm.TitleInput.Value = "With labels";
        vm.LabelsInput.Value = "bug, help wanted";
        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal(77, vm.CreatedIssueNumber.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WithBodyButNoLabels_SetsCreatedIssueNumber()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", req =>
            {
                if (req.Method == HttpMethod.Post)
                    return new MockResponse(IssueJson(88, "With body"));
                return new MockResponse("[]");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreateIssueViewModel(factory);
        vm.Initialize("owner", "repo");

        vm.TitleInput.Value = "With body";
        vm.BodyInput.Value = "This is a detailed body";
        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal(88, vm.CreatedIssueNumber.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WithNotFoundResponse_SetsErrorMessage()
    {
        var handler = new MockHttpHandler(); // No routes → 404
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreateIssueViewModel(factory);
        vm.Initialize("owner", "repo");

        vm.TitleInput.Value = "Test";
        await vm.CreateCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.Contains("Create failed", vm.ErrorMessage.Value);
        Assert.Null(vm.CreatedIssueNumber.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WhenTimeoutAndIssueExists_SetsCreatedIssueNumber()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", req =>
            {
                if (req.Method == HttpMethod.Post)
                    throw new OperationCanceledException();
                return new MockResponse($"[{IssueJson(99, "My new issue")}]");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreateIssueViewModel(factory);
        vm.Initialize("owner", "repo");
        vm.TitleInput.Value = "My new issue";

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal(99, vm.CreatedIssueNumber.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Create_WhenTimeoutAndNoMatch_SetsMaybeSubmitted()
    {
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", req =>
            {
                if (req.Method == HttpMethod.Post)
                    throw new OperationCanceledException();
                return new MockResponse("[]");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new CreateIssueViewModel(factory);
        vm.Initialize("owner", "repo");
        vm.TitleInput.Value = "My new issue";

        await vm.CreateCommand.ExecuteAsync(null);

        Assert.Contains("may have been submitted", vm.ErrorMessage.Value, StringComparison.Ordinal);
        Assert.Null(vm.CreatedIssueNumber.Value);
        vm.Dispose();
    }
}
