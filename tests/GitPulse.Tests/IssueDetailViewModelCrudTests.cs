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

    [Fact]
    public async Task Load_WithToken_PopulatesIssueAndComments()
    {
        var handler = new MockHttpHandler()
            .When("/issues/42", IssueJson(42, "open", "Bug description"))
            .When("/issues/42/comments",
                $"[{CommentJson(1, "First")},{CommentJson(2, "Second")}]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssueDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.NotNull(vm.Issue.Value);
        Assert.Equal(42, vm.Issue.Value!.Number);
        Assert.Equal("#42 Issue 42", vm.Title.Value);
        Assert.Equal(2, vm.Comments.Count);
        Assert.Equal("First", vm.Comments[0].Body);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithoutToken_SetsErrorMessage()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler, token: null);
        var vm = new IssueDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.Null(vm.Issue.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_PopulatesLabelsFromIssuePayload()
    {
        var issueJsonWithLabels =
            $"{{\"number\":42,\"title\":\"Issue 42\",\"state\":\"open\"," +
            $"\"body\":\"body\",\"user\":{{\"login\":\"alice\"}}," +
            $"\"labels\":[{{\"name\":\"bug\",\"color\":\"ff0000\"}}," +
            $"{{\"name\":\"help\",\"color\":\"00ff00\"}}]}}";
        var handler = new MockHttpHandler()
            .When("/issues/42", issueJsonWithLabels)
            .When("/issues/42/comments", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssueDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Labels.Count);
        Assert.Equal("bug", vm.Labels[0].Name);
        Assert.Equal("bug, help", vm.LabelInput.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task ToggleState_ChangesClosedToOpen()
    {
        var handler = new MockHttpHandler()
            .When("/issues/42", req =>
            {
                if (req.Method == HttpMethod.Patch)
                    return new MockResponse(IssueJson(42, state: "open"));
                return new MockResponse(IssueJson(42, state: "closed"));
            })
            .When("/issues/42/comments", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssueDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("closed", vm.Issue.Value!.State);
        await vm.ToggleStateCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal("open", vm.Issue.Value!.State);
        vm.Dispose();
    }

    [Fact]
    public async Task SaveLabels_ReplacesLabelsCollection()
    {
        var labelsJson =
            "[{\"name\":\"bug\",\"color\":\"ff0000\"}," +
            "{\"name\":\"wontfix\",\"color\":\"000000\"}]";
        var handler = new MockHttpHandler()
            .When("/issues/42", IssueJson(42))
            .When("/issues/42/labels", req =>
            {
                if (req.Method == HttpMethod.Put)
                    return new MockResponse(labelsJson);
                return new MockResponse("[]");
            })
            .When("/issues/42/comments", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssueDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.LabelInput.Value = "bug, wontfix";
        await vm.SaveLabelsCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal(2, vm.Labels.Count);
        Assert.Equal("bug", vm.Labels[0].Name);
        Assert.Equal("wontfix", vm.Labels[1].Name);
        vm.Dispose();
    }

    [Fact]
    public async Task SaveLabels_WithEmptyInput_ClearsLabels()
    {
        var handler = new MockHttpHandler()
            .When("/issues/42", IssueJson(42))
            .When("/issues/42/labels", req =>
            {
                if (req.Method == HttpMethod.Put)
                    return new MockResponse("[]");
                return new MockResponse("[]");
            })
            .When("/issues/42/comments", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new IssueDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.LabelInput.Value = "";
        await vm.SaveLabelsCommand.ExecuteAsync(null);

        Assert.Empty(vm.Labels);
        vm.Dispose();
    }
}
