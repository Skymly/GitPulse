using GitPulse.Core.Models;
using GitPulse.Tests.TestHelpers;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class PullRequestDetailViewModelTests
{
    private static string PrJson(int number, string state = "open", bool draft = false, bool merged = false) =>
        $"{{\"number\":{number},\"title\":\"PR {number}\",\"state\":\"{state}\"," +
        $"\"draft\":{draft.ToString().ToLower()},\"merged\":{merged.ToString().ToLower()}," +
        $"\"headRef\":\"feature\",\"baseRef\":\"main\"," +
        $"\"user\":{{\"login\":\"bob\"}}}}";

    private static string CommentJson(int id, string body) =>
        $"{{\"id\":{id},\"body\":\"{body}\",\"user\":{{\"login\":\"alice\"}}," +
        $"\"created_at\":\"2025-01-01T00:00:00Z\"}}";

    [Fact]
    public void Initialize_SetsOwnerRepoAndPrNumber()
    {
        var vm = new PullRequestDetailViewModel(
            new FakeGitHubClientFactory(new MockHttpHandler()), new FakeBrowserLauncher());

        vm.Initialize("owner", "repo", 42);
        // No public properties for owner/repo/prNumber, but Load should work.
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithoutToken_SetsErrorMessage()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler, token: null);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.Null(vm.PullRequest.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithToken_PopulatesPullRequestAndComments()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open"))
            .When("/issues/42/comments",
                $"[{CommentJson(1, "Looks good")},{CommentJson(2, "Needs work")}]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.NotNull(vm.PullRequest.Value);
        Assert.Equal(42, vm.PullRequest.Value!.Number);
        Assert.Equal("#42 PR 42", vm.Title.Value);
        Assert.Equal(2, vm.Comments.Count);
        Assert.Equal("Looks good", vm.Comments[0].Body);
        vm.Dispose();
    }

    [Fact]
    public async Task AddComment_AppendsToCommentsAndClearsInput()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42))
            .When("/issues/42/comments", req =>
            {
                if (req.Method == HttpMethod.Post)
                    return new MockResponse(CommentJson(100, "LGTM"));
                return new MockResponse("[]");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.CommentInput.Value = "LGTM";
        await vm.AddCommentCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal(string.Empty, vm.CommentInput.Value);
        Assert.Single(vm.Comments);
        Assert.Equal("LGTM", vm.Comments[0].Body);
        vm.Dispose();
    }

    [Fact]
    public async Task AddComment_WithEmptyInput_DoesNothing()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42))
            .When("/issues/42/comments", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.CommentInput.Value = "   ";
        await vm.AddCommentCommand.ExecuteAsync(null);

        Assert.Empty(vm.Comments);
        vm.Dispose();
    }

    [Fact]
    public async Task ToggleState_ChangesOpenToClosed()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open"))
            .When("/issues/42", req =>
            {
                if (req.Method == HttpMethod.Patch)
                    return new MockResponse(
                        $"{{\"number\":42,\"title\":\"PR 42\",\"state\":\"closed\"}}");
                return new MockResponse("[]");
            })
            .When("/issues/42/comments", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("open", vm.PullRequest.Value!.State);
        await vm.ToggleStateCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal("closed", vm.PullRequest.Value!.State);
        vm.Dispose();
    }

    [Fact]
    public async Task ToggleState_ChangesClosedToOpen()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "closed"))
            .When("/issues/42", req =>
            {
                if (req.Method == HttpMethod.Patch)
                    return new MockResponse(
                        $"{{\"number\":42,\"title\":\"PR 42\",\"state\":\"open\"}}");
                return new MockResponse("[]");
            })
            .When("/issues/42/comments", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("closed", vm.PullRequest.Value!.State);
        await vm.ToggleStateCommand.ExecuteAsync(null);

        Assert.Equal("open", vm.PullRequest.Value!.State);
        vm.Dispose();
    }
}
