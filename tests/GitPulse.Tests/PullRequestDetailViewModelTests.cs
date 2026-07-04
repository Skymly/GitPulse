using GitPulse.Core.Models;
using GitPulse.Tests.TestHelpers;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class PullRequestDetailViewModelTests
{
    private static string PrJson(int number, string state = "open", bool draft = false, bool merged = false,
        bool? mergeable = null, string? mergeableState = null, int commits = 0, int additions = 0, int deletions = 0, int changedFiles = 0) =>
        $"{{\"number\":{number},\"title\":\"PR {number}\",\"state\":\"{state}\"," +
        $"\"draft\":{draft.ToString().ToLower()},\"merged\":{merged.ToString().ToLower()}," +
        $"\"headRef\":\"feature\",\"baseRef\":\"main\"," +
        (mergeable.HasValue ? $"\"mergeable\":{mergeable.Value.ToString().ToLower()}," : "") +
        (mergeableState is not null ? $"\"mergeable_state\":\"{mergeableState}\"," : "") +
        $"\"commits\":{commits},\"additions\":{additions},\"deletions\":{deletions},\"changed_files\":{changedFiles}," +
        $"\"user\":{{\"login\":\"bob\"}}}}";

    private static string MergeJson(string sha, bool merged = true) =>
        $"{{\"sha\":\"{sha}\",\"merged\":{merged.ToString().ToLower()}," +
        $"\"message\":\"Pull Request successfully merged\"}}";

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

    // ── M6: Merge tests ──────────────────────────────────────────

    [Fact]
    public async Task Load_MergeablePR_SetsCanMergeTrue()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", mergeable: true, mergeableState: "clean", commits: 3, additions: 50, deletions: 10, changedFiles: 5))
            .When("/issues/42/comments", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.CanMerge.Value);
        Assert.Equal("Mergeable", vm.MergeStatus.Value);
        Assert.False(vm.IsMerged.Value);
        Assert.Equal(3, vm.PullRequest.Value!.Commits);
        Assert.Equal(50, vm.PullRequest.Value!.Additions);
        Assert.Equal(10, vm.PullRequest.Value!.Deletions);
        Assert.Equal(5, vm.PullRequest.Value!.ChangedFiles);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_ConflictingPR_SetsCanMergeFalse()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", mergeable: false, mergeableState: "dirty"))
            .When("/issues/42/comments", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.CanMerge.Value);
        Assert.Contains("Conflicts", vm.MergeStatus.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_DraftPR_SetsCanMergeFalse()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", draft: true, mergeable: true))
            .When("/issues/42/comments", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.CanMerge.Value);
        Assert.Contains("Draft", vm.MergeStatus.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_MergedPR_SetsIsMergedTrue()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "closed", merged: true))
            .When("/issues/42/comments", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.IsMerged.Value);
        Assert.False(vm.CanMerge.Value);
        Assert.Equal("Merged", vm.MergeStatus.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_PendingMergeable_SetsCheckingStatus()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", mergeable: null))
            .When("/issues/42/comments", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.CanMerge.Value);
        Assert.Contains("Checking", vm.MergeStatus.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Merge_WithMergeablePR_UpdatesStateToMerged()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", mergeable: true, mergeableState: "clean"))
            .When("/pulls/42/merge", req =>
            {
                if (req.Method == HttpMethod.Put)
                    return new MockResponse(MergeJson("abc123sha", merged: true));
                return new MockResponse(PrJson(42, "open", mergeable: true));
            })
            .When("/issues/42/comments", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.True(vm.CanMerge.Value);

        await vm.MergeCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.True(vm.IsMerged.Value);
        Assert.False(vm.CanMerge.Value);
        Assert.True(vm.PullRequest.Value!.Merged);
        Assert.Equal("abc123sha", vm.PullRequest.Value!.MergeCommitSha);
        Assert.Equal("closed", vm.PullRequest.Value!.State);
        vm.Dispose();
    }

    [Fact]
    public async Task Merge_WithSquashMethod_SendsSquashInRequest()
    {
        string? capturedMethod = null;
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", mergeable: true, mergeableState: "clean"))
            .When("/pulls/42/merge", req =>
            {
                if (req.Method == HttpMethod.Put)
                {
                    // Read the request body to capture the merge method.
                    var bodyTask = req.Content?.ReadAsStringAsync();
                    capturedMethod = bodyTask?.Result ?? "";
                    return new MockResponse(MergeJson("squashsha", merged: true));
                }
                return new MockResponse(PrJson(42, "open", mergeable: true));
            })
            .When("/issues/42/comments", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.MergeMethod.Value = "squash";
        await vm.MergeCommand.ExecuteAsync(null);

        Assert.NotNull(capturedMethod);
        Assert.NotEmpty(capturedMethod);
        Assert.Contains("\"merge_method\":\"squash\"", capturedMethod);
        Assert.True(vm.IsMerged.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Merge_WithNonMergeablePR_DoesNothing()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", mergeable: false, mergeableState: "dirty"))
            .When("/pulls/42/merge", _ => new MockResponse(MergeJson("should-not-happen")))
            .When("/issues/42/comments", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.False(vm.CanMerge.Value);

        await vm.MergeCommand.ExecuteAsync(null);

        // Should not have attempted merge.
        Assert.False(vm.IsMerged.Value);
        Assert.Empty(vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Merge_WithoutToken_SetsErrorMessage()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", mergeable: true))
            .When("/issues/42/comments", "[]");
        var factory = new FakeGitHubClientFactory(handler, token: null);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.MergeCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.Contains("No token", vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Merge_WithApiReturningNotMerged_SetsErrorMessage()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", mergeable: true, mergeableState: "clean"))
            .When("/pulls/42/merge", _ => new MockResponse(MergeJson("nosha", merged: false)))
            .When("/issues/42/comments", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.MergeCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.False(vm.IsMerged.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Merge_WithApiError_SetsErrorMessage()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", mergeable: true))
            .When("/issues/42/comments", "[]");
        // No merge route → 404 → exception
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.MergeCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.Contains("Merge failed", vm.ErrorMessage.Value);
        Assert.False(vm.IsMerged.Value);
        vm.Dispose();
    }

    [Fact]
    public void MergeMethod_DefaultIsMerge()
    {
        var vm = new PullRequestDetailViewModel(
            new FakeGitHubClientFactory(new MockHttpHandler()), new FakeBrowserLauncher());

        Assert.Equal("merge", vm.MergeMethod.Value);
        vm.Dispose();
    }
}

public class PullRequestModelTests
{
    [Fact]
    public void PullRequest_M6Fields_Defaults_AreValid()
    {
        var pr = new PullRequest { Number = 1 };

        Assert.Null(pr.Mergeable);
        Assert.Null(pr.MergeableState);
        Assert.Null(pr.MergeCommitSha);
        Assert.Equal(0, pr.Commits);
        Assert.Equal(0, pr.Additions);
        Assert.Equal(0, pr.Deletions);
        Assert.Equal(0, pr.ChangedFiles);
    }

    [Fact]
    public void PullRequest_WithM6Fields_PreservesValues()
    {
        var pr = new PullRequest
        {
            Number = 42,
            Title = "Feature PR",
            State = "open",
            Mergeable = true,
            MergeableState = "clean",
            MergeCommitSha = "abc123",
            Commits = 5,
            Additions = 100,
            Deletions = 20,
            ChangedFiles = 8,
        };

        Assert.True(pr.Mergeable);
        Assert.Equal("clean", pr.MergeableState);
        Assert.Equal("abc123", pr.MergeCommitSha);
        Assert.Equal(5, pr.Commits);
        Assert.Equal(100, pr.Additions);
        Assert.Equal(20, pr.Deletions);
        Assert.Equal(8, pr.ChangedFiles);
    }

    [Fact]
    public void MergeRequest_Defaults_AreValid()
    {
        var req = new MergeRequest();

        Assert.Equal("merge", req.Method);
        Assert.Null(req.CommitMessage);
        Assert.Null(req.CommitTitle);
        Assert.Null(req.Sha);
    }

    [Fact]
    public void MergeRequest_WithSquash_SetsMethod()
    {
        var req = new MergeRequest { Method = "squash", CommitTitle = "Squash merge" };

        Assert.Equal("squash", req.Method);
        Assert.Equal("Squash merge", req.CommitTitle);
    }

    [Fact]
    public void MergeResponse_Defaults_AreValid()
    {
        var resp = new MergeResponse();

        Assert.Equal(string.Empty, resp.Sha);
        Assert.False(resp.Merged);
        Assert.Equal(string.Empty, resp.Message);
    }

    [Fact]
    public void MergeResponse_WithValues_PreservesThem()
    {
        var resp = new MergeResponse { Sha = "merge123", Merged = true, Message = "Successfully merged" };

        Assert.Equal("merge123", resp.Sha);
        Assert.True(resp.Merged);
        Assert.Equal("Successfully merged", resp.Message);
    }
}
