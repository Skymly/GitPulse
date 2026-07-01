using GitPulse.Core.Models;
using GitPulse.Tests.TestHelpers;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class IssueDetailViewModelCrudTests
{
    private static string IssueJson(int number, string state = "open", string? body = "body") =>
        $"{{\"number\":{number},\"title\":\"Issue {number}\",\"state\":\"{state}\"," +
        $"\"body\":\"{body ?? ""}\",\"user\":{{\"login\":\"alice\"}}," +
        $"\"labels\":[]}}";

    private static string CommentJson(int id, string body) =>
        $"{{\"id\":{id},\"body\":\"{body}\",\"user\":{{\"login\":\"bob\"}}," +
        $"\"created_at\":\"2025-01-01T00:00:00Z\"}}";

    /// <summary>
    /// POST /repos/.../issues/{number}/comments returns the created comment.
    /// The ViewModel should append it to the Comments collection.
    /// </summary>
    [Fact]
    public async Task AddComment_AppendsToCommentsAndClearsInput()
    {
        // Load returns issue + empty comments; POST returns the new comment.
        var handler = new MockHttpHandler()
            .When("/issues/42/comments", req =>
            {
                // POST (AddComment) vs GET (Load) — distinguish by method.
                if (req.Method == HttpMethod.Post)
                    return new MockResponse(CommentJson(100, "Nice work!"));
                return new MockResponse("[]");
            })
            .When("/issues/42", IssueJson(42));
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssueDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.CommentInput.Value = "Nice work!";
        await vm.AddCommentCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal(string.Empty, vm.CommentInput.Value);
        Assert.Single(vm.Comments);
        Assert.Equal("Nice work!", vm.Comments[0].Body);
        Assert.Equal("bob", vm.Comments[0].User!.Login);
        vm.Dispose();
    }

    /// <summary>
    /// PATCH /repos/.../issues/{number} with state=closed toggles the issue.
    /// The ViewModel should update Issue.Value.State.
    /// </summary>
    [Fact]
    public async Task ToggleState_ChangesOpenToClosed()
    {
        var handler = new MockHttpHandler()
            .When("/issues/42", req =>
            {
                if (req.Method == HttpMethod.Patch)
                    return new MockResponse(IssueJson(42, state: "closed"));
                return new MockResponse(IssueJson(42, state: "open"));
            })
            .When("/issues/42/comments", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssueDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("open", vm.Issue.Value!.State);
        await vm.ToggleStateCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal("closed", vm.Issue.Value!.State);
        vm.Dispose();
    }

    /// <summary>
    /// Empty comment input does not trigger a POST.
    /// </summary>
    [Fact]
    public async Task AddComment_WithEmptyInput_DoesNothing()
    {
        var handler = new MockHttpHandler()
            .When("/issues/42", IssueJson(42))
            .When("/issues/42/comments", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssueDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.CommentInput.Value = "   ";
        await vm.AddCommentCommand.ExecuteAsync(null);

        Assert.Empty(vm.Comments);
        vm.Dispose();
    }
}
