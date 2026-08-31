using System.Net;
using GitPulse.Core.Models;
using GitPulse.Tests.TestHelpers;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class PullRequestDetailViewModelTests
{
    private static string PrJson(int number, string state = "open", bool draft = false, bool merged = false,
        bool? mergeable = null, string? mergeableState = null, int commits = 0, int additions = 0, int deletions = 0, int changedFiles = 0,
        string? title = null, string body = "", string? headSha = null) =>
        $"{{\"number\":{number},\"title\":\"{title ?? $"PR {number}"}\",\"state\":\"{state}\"," +
        $"\"body\":\"{body}\"," +
        $"\"draft\":{draft.ToString().ToLower()},\"merged\":{merged.ToString().ToLower()}," +
        $"\"headRef\":\"feature\",\"baseRef\":\"main\"," +
        (mergeable.HasValue ? $"\"mergeable\":{mergeable.Value.ToString().ToLower()}," : "") +
        (mergeableState is not null ? $"\"mergeable_state\":\"{mergeableState}\"," : "") +
        $"\"commits\":{commits},\"additions\":{additions},\"deletions\":{deletions},\"changed_files\":{changedFiles}," +
        (headSha is not null ? $"\"head\":{{\"sha\":\"{headSha}\"}}," : "") +
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

    // ── M14: Save title/body ─────────────────────────────────────

    [Fact]
    public async Task SaveTitleBody_WithEmptyTitle_DoesNothing()
    {
        var patchCount = 0;
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, title: "Original", body: "keep me"))
            .When("/issues/42/comments", "[]")
            .When("/issues/42", req =>
            {
                if (req.Method == HttpMethod.Patch)
                {
                    patchCount++;
                    return new MockResponse(
                        "{\"number\":42,\"title\":\"should-not-happen\",\"state\":\"open\",\"body\":\"changed body\"}");
                }

                return new MockResponse("[]");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.TitleInput.Value = "   ";
        vm.BodyInput.Value = "changed body";
        await vm.SaveTitleBodyCommand.ExecuteAsync(null);

        Assert.Equal(0, patchCount);
        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal("#42 Original", vm.Title.Value);
        Assert.Equal("Original", vm.PullRequest.Value!.Title);
        Assert.Equal("keep me", vm.PullRequest.Value.Body);
        vm.Dispose();
    }

    [Fact]
    public async Task SaveTitleBody_WithEmptyBody_RefreshesDetail()
    {
        var saved = false;
        var handler = new MockHttpHandler()
            .When("/pulls/42", _ => new MockResponse(
                saved
                    ? PrJson(42, title: "Kept title", body: "")
                    : PrJson(42, title: "Kept title", body: "old body")))
            .When("/issues/42/comments", "[]")
            .When("/issues/42", req =>
            {
                if (req.Method == HttpMethod.Patch)
                {
                    saved = true;
                    return new MockResponse(
                        "{\"number\":42,\"title\":\"Kept title\",\"state\":\"open\",\"body\":\"\"}");
                }

                return new MockResponse("[]");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.TitleInput.Value = "Kept title";
        vm.BodyInput.Value = "";
        await vm.SaveTitleBodyCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal("#42 Kept title", vm.Title.Value);
        Assert.Equal("Kept title", vm.TitleInput.Value);
        Assert.Equal(string.Empty, vm.BodyInput.Value);
        Assert.Equal(string.Empty, vm.PullRequest.Value!.Body);
        vm.Dispose();
    }

    [Fact]
    public async Task SaveTitleBody_WithValidInputs_RefreshesDetail()
    {
        var saved = false;
        var handler = new MockHttpHandler()
            .When("/pulls/42", _ => new MockResponse(
                saved
                    ? PrJson(42, title: "Updated title", body: "Updated body")
                    : PrJson(42, title: "Original", body: "old")))
            .When("/issues/42/comments", "[]")
            .When("/issues/42", req =>
            {
                if (req.Method == HttpMethod.Patch)
                {
                    saved = true;
                    return new MockResponse(
                        "{\"number\":42,\"title\":\"Updated title\",\"state\":\"open\",\"body\":\"Updated body\"}");
                }

                return new MockResponse("[]");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.TitleInput.Value = "Updated title";
        vm.BodyInput.Value = "Updated body";
        await vm.SaveTitleBodyCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal("#42 Updated title", vm.Title.Value);
        Assert.Equal("Updated title", vm.TitleInput.Value);
        Assert.Equal("Updated body", vm.BodyInput.Value);
        Assert.Equal("Updated title", vm.PullRequest.Value!.Title);
        Assert.Equal("Updated body", vm.PullRequest.Value.Body);
        vm.Dispose();
    }

    [Fact]
    public async Task SaveTitleBody_WithoutToken_SetsErrorMessage()
    {
        var handler = new MockHttpHandler();
        var factory = new FakeGitHubClientFactory(handler, token: null);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        vm.PullRequest.Value = new PullRequest
        {
            Number = 42,
            Title = "Original",
            Body = "body",
            State = "open",
        };
        vm.TitleInput.Value = "New title";
        vm.BodyInput.Value = "New body";

        await vm.SaveTitleBodyCommand.ExecuteAsync(null);

        Assert.Contains("No token", vm.ErrorMessage.Value);
        Assert.Equal("Original", vm.PullRequest.Value!.Title);
        vm.Dispose();
    }

    [Fact]
    public async Task SaveTitleBody_WithApiError_SetsErrorMessage()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, title: "Original", body: "old"))
            .When("/issues/42/comments", "[]");
        // No /issues/42 PATCH route → 404
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.TitleInput.Value = "New title";
        await vm.SaveTitleBodyCommand.ExecuteAsync(null);

        Assert.Contains("Save failed", vm.ErrorMessage.Value);
        Assert.Equal("#42 Original", vm.Title.Value);
        Assert.Equal("Original", vm.PullRequest.Value!.Title);
        vm.Dispose();
    }

    [Fact]
    public async Task SaveTitleBody_WhileSaving_DoesNothing()
    {
        var patchCount = 0;
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, title: "Original", body: "old"))
            .When("/issues/42/comments", "[]")
            .When("/issues/42", req =>
            {
                if (req.Method == HttpMethod.Patch)
                {
                    patchCount++;
                    return new MockResponse(
                        "{\"number\":42,\"title\":\"should-not-happen\",\"state\":\"open\"}");
                }

                return new MockResponse("[]");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.TitleInput.Value = "New title";
        vm.IsSaving.Value = true;
        await vm.SaveTitleBodyCommand.ExecuteAsync(null);

        Assert.Equal(0, patchCount);
        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal("#42 Original", vm.Title.Value);
        vm.Dispose();
    }
    private static string UserJson(string login) => $"{{\"login\":\"{login}\"}}";

    private static string ReviewJson(long id, string state, string body, string login = "alice") =>
        $"{{\"id\":{id},\"state\":\"{state}\",\"body\":\"{body}\",\"user\":{{\"login\":\"{login}\"}}}}";

    [Fact]
    public async Task Load_ListsSubmittedReviews_SkipsPending()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open"))
            .When("/issues/42/comments", "[]")
            .When("/user", UserJson("alice"))
            .When("/pulls/42/reviews",
                $"[{ReviewJson(1, "APPROVED", "LGTM")},{ReviewJson(2, "PENDING", "wip")},{ReviewJson(3, "COMMENTED", "nit")}]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal(2, vm.Reviews.Count);
        Assert.Equal("APPROVED", vm.Reviews[0].State);
        Assert.Equal("COMMENTED", vm.Reviews[1].State);
        Assert.Equal("alice", vm.ViewerLogin.Value);
        Assert.True(vm.CanReview.Value);
        Assert.True(vm.CanApproveOrRequestChanges.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WhenUserAndReviewsMissing_StillLoadsPullRequest()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open"))
            .When("/issues/42/comments", "[]");
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.NotNull(vm.PullRequest.Value);
        Assert.Empty(vm.Reviews);
        Assert.Equal(string.Empty, vm.ViewerLogin.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task SubmitReview_Comment_PostsEventAndClearsBody()
    {
        string? posted = null;
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", mergeable: true, mergeableState: "clean"))
            .When("/issues/42/comments", "[]")
            .When("/user", UserJson("alice"))
            .When("/pulls/42/reviews", req =>
            {
                if (req.Method == HttpMethod.Post)
                {
                    posted = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                    return new MockResponse(ReviewJson(9, "COMMENTED", "Looks good"));
                }

                return new MockResponse($"[{ReviewJson(9, "COMMENTED", "Looks good")}]");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.ReviewEvent.Value = "COMMENT";
        vm.ReviewBody.Value = "Looks good";
        await vm.SubmitReviewCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal(string.Empty, vm.ReviewBody.Value);
        Assert.Contains("\"event\":\"COMMENT\"", posted, StringComparison.OrdinalIgnoreCase);
        Assert.Single(vm.Reviews);
        vm.Dispose();
    }

    [Fact]
    public async Task SubmitReview_Approve_AllowsEmptyBody()
    {
        string? posted = null;
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open"))
            .When("/issues/42/comments", "[]")
            .When("/user", UserJson("alice"))
            .When("/pulls/42/reviews", req =>
            {
                if (req.Method == HttpMethod.Post)
                {
                    posted = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                    return new MockResponse(ReviewJson(9, "APPROVED", ""));
                }

                return new MockResponse("[]");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.ReviewEvent.Value = "APPROVE";
        vm.ReviewBody.Value = "   ";
        await vm.SubmitReviewCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.NotNull(posted);
        Assert.Contains("\"event\":\"APPROVE\"", posted, StringComparison.OrdinalIgnoreCase);
        vm.Dispose();
    }

    [Fact]
    public async Task SubmitReview_Comment_EmptyBody_DoesNothing()
    {
        var posted = false;
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open"))
            .When("/issues/42/comments", "[]")
            .When("/user", UserJson("alice"))
            .When("/pulls/42/reviews", req =>
            {
                if (req.Method == HttpMethod.Post)
                    posted = true;
                return new MockResponse("[]");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.ReviewEvent.Value = "COMMENT";
        vm.ReviewBody.Value = "  ";
        await vm.SubmitReviewCommand.ExecuteAsync(null);

        Assert.False(posted);
        vm.Dispose();
    }

    [Fact]
    public async Task SubmitReview_Author_CannotApprove()
    {
        var posted = false;
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open"))
            .When("/issues/42/comments", "[]")
            .When("/user", UserJson("bob"))
            .When("/pulls/42/reviews", req =>
            {
                if (req.Method == HttpMethod.Post)
                    posted = true;
                return new MockResponse("[]");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.CanApproveOrRequestChanges.Value);
        vm.ReviewEvent.Value = "APPROVE";
        await vm.SubmitReviewCommand.ExecuteAsync(null);

        Assert.False(posted);
        vm.Dispose();
    }

    [Fact]
    public async Task SubmitReview_ClosedPullRequest_DoesNothing()
    {
        var posted = false;
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "closed"))
            .When("/issues/42/comments", "[]")
            .When("/user", UserJson("alice"))
            .When("/pulls/42/reviews", req =>
            {
                if (req.Method == HttpMethod.Post)
                    posted = true;
                return new MockResponse("[]");
            });
        var factory = new FakeGitHubClientFactory(handler);
        var vm = new PullRequestDetailViewModel(factory, new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.CanReview.Value);
        vm.ReviewEvent.Value = "COMMENT";
        vm.ReviewBody.Value = "too late";
        await vm.SubmitReviewCommand.ExecuteAsync(null);

        Assert.False(posted);
        vm.Dispose();
    }

    private static string CheckRunsJson(params (long Id, string Name, string Status, string? Conclusion)[] runs)
    {
        var items = string.Join(",", runs.Select(run =>
            $"{{\"id\":{run.Id},\"name\":\"{run.Name}\",\"status\":\"{run.Status}\"," +
            $"\"conclusion\":{(run.Conclusion is null ? "null" : $"\"{run.Conclusion}\"")}," +
            $"\"html_url\":\"https://example/runs/{run.Id}\",\"head_sha\":\"abc123\"}}"));
        return $"{{\"total_count\":{runs.Length},\"check_runs\":[{items}]}}";
    }

    private static string CombinedStatusJson(
        string state, params (long Id, string State, string Context)[] statuses)
    {
        var items = string.Join(",", statuses.Select(status =>
            $"{{\"id\":{status.Id},\"state\":\"{status.State}\",\"context\":\"{status.Context}\"," +
            $"\"target_url\":\"https://ci/{status.Id}\"}}"));
        return $"{{\"state\":\"{state}\",\"sha\":\"abc123\",\"total_count\":{statuses.Length},\"statuses\":[{items}]}}";
    }

    private static PullRequestDetailViewModel LoadWithGate(
        MockHttpHandler handler)
    {
        var vm = new PullRequestDetailViewModel(
            new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        return vm;
    }

    [Fact]
    public async Task Load_ListsCheckRunsAndStatuses_SendsLatestFilter()
    {
        string? checkQuery = null;
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", headSha: "abc123"))
            .When("/issues/42/comments", "[]")
            .When("/user", UserJson("alice"))
            .When("/pulls/42/reviews", "[]")
            .When("/commits/abc123/check-runs", req =>
            {
                checkQuery = req.RequestUri?.Query;
                return new MockResponse(CheckRunsJson((1, "CI", "completed", "success")));
            })
            .When("/commits/abc123/status", CombinedStatusJson("success", (9, "success", "jenkins")));
        var vm = LoadWithGate(handler);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Contains("filter=latest", checkQuery);
        Assert.Single(vm.CheckRuns);
        Assert.Equal("CI", vm.CheckRuns[0].Name);
        Assert.Single(vm.CommitStatuses);
        Assert.Equal("jenkins", vm.CommitStatuses[0].Context);
        Assert.Equal("Success", vm.GateRollup.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_EmptyGate_IsNoChecks()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", headSha: "abc123"))
            .When("/issues/42/comments", "[]")
            .When("/user", UserJson("alice"))
            .When("/pulls/42/reviews", "[]")
            .When("/commits/abc123/check-runs", CheckRunsJson())
            .When("/commits/abc123/status", CombinedStatusJson("pending"));
        var vm = LoadWithGate(handler);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("No checks", vm.GateRollup.Value);
        Assert.Empty(vm.CheckRuns);
        Assert.Empty(vm.CommitStatuses);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_EmptyStatusesWithPassingRuns_IsSuccessNotPending()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", headSha: "abc123"))
            .When("/issues/42/comments", "[]")
            .When("/user", UserJson("alice"))
            .When("/pulls/42/reviews", "[]")
            .When("/commits/abc123/check-runs", CheckRunsJson((1, "CI", "completed", "success")))
            .When("/commits/abc123/status", CombinedStatusJson("pending"));
        var vm = LoadWithGate(handler);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Success", vm.GateRollup.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_InProgressCheckRun_IsPending()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", headSha: "abc123"))
            .When("/issues/42/comments", "[]")
            .When("/user", UserJson("alice"))
            .When("/pulls/42/reviews", "[]")
            .When("/commits/abc123/check-runs", CheckRunsJson((1, "CI", "in_progress", null)))
            .When("/commits/abc123/status", CombinedStatusJson("success"));
        var vm = LoadWithGate(handler);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Pending", vm.GateRollup.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_FailedConclusion_IsFailure()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", headSha: "abc123"))
            .When("/issues/42/comments", "[]")
            .When("/user", UserJson("alice"))
            .When("/pulls/42/reviews", "[]")
            .When("/commits/abc123/check-runs", CheckRunsJson((1, "CI", "completed", "failure")))
            .When("/commits/abc123/status", CombinedStatusJson("success"));
        var vm = LoadWithGate(handler);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Failure", vm.GateRollup.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_SkippedAndNeutral_IsSuccess()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", headSha: "abc123"))
            .When("/issues/42/comments", "[]")
            .When("/user", UserJson("alice"))
            .When("/pulls/42/reviews", "[]")
            .When("/commits/abc123/check-runs",
                CheckRunsJson((1, "lint", "completed", "skipped"), (2, "review", "completed", "neutral")))
            .When("/commits/abc123/status", CombinedStatusJson("success"));
        var vm = LoadWithGate(handler);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Success", vm.GateRollup.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WhenGateEndpoints404_StillLoadsPullRequest()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", headSha: "abc123"))
            .When("/issues/42/comments", "[]")
            .When("/user", UserJson("alice"))
            .When("/pulls/42/reviews", "[]")
            .When("/commits/abc123/check-runs", HttpStatusCode.NotFound)
            .When("/commits/abc123/status", HttpStatusCode.NotFound);
        var vm = LoadWithGate(handler);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.NotNull(vm.PullRequest.Value);
        Assert.Equal("No checks", vm.GateRollup.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_MissingHeadSha_SkipsGateCalls()
    {
        var gateCalled = false;
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open"))
            .When("/issues/42/comments", "[]")
            .When("/user", UserJson("alice"))
            .When("/pulls/42/reviews", "[]")
            .When("/check-runs", req =>
            {
                gateCalled = true;
                return new MockResponse(CheckRunsJson());
            })
            .When("/status", req =>
            {
                gateCalled = true;
                return new MockResponse(CombinedStatusJson("pending"));
            });
        var vm = LoadWithGate(handler);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(gateCalled);
        Assert.Equal("No checks", vm.GateRollup.Value);
        vm.Dispose();
    }
    private static string RequestedReviewersJson(string usersJson = "[]", string teamsJson = "[]") =>
        $"{{\"users\":{usersJson},\"teams\":{teamsJson}}}";

    [Fact]
    public async Task Load_ListsRequestedReviewersAndTeams()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open"))
            .When("/issues/42/comments", "[]")
            .When("/user", UserJson("alice"))
            .When("/pulls/42/reviews", "[]")
            .When("/pulls/42/requested_reviewers",
                RequestedReviewersJson(
                    "[{\"login\":\"carol\"}]",
                    "[{\"slug\":\"docs\",\"name\":\"Docs\"}]"));
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal("carol", Assert.Single(vm.RequestedReviewers).Login);
        Assert.Equal("docs", Assert.Single(vm.RequestedTeams).Slug);
        Assert.True(vm.CanManageReviewers.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WhenRequestedReviewersMissing_StillLoadsPullRequest()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open"))
            .When("/issues/42/comments", "[]");
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.NotNull(vm.PullRequest.Value);
        Assert.Empty(vm.RequestedReviewers);
        vm.Dispose();
    }

    [Fact]
    public async Task RequestReviewer_PostsLoginAndRefreshesList()
    {
        string? posted = null;
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open"))
            .When("/issues/42/comments", "[]")
            .When("/user", UserJson("alice"))
            .When("/pulls/42/reviews", "[]")
            .When("/pulls/42/requested_reviewers", req =>
            {
                if (req.Method == HttpMethod.Post)
                {
                    posted = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                    return new MockResponse(PrJson(42), StatusCode: HttpStatusCode.Created);
                }

                if (posted is null)
                    return new MockResponse(RequestedReviewersJson());
                return new MockResponse(RequestedReviewersJson("[{\"login\":\"carol\"}]"));
            });
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.ReviewerLogin.Value = "@carol";
        await vm.RequestReviewerCommand.ExecuteAsync(null);

        Assert.Contains("carol", posted, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, vm.ReviewerLogin.Value);
        Assert.Equal("carol", Assert.Single(vm.RequestedReviewers).Login);
        vm.Dispose();
    }

    [Fact]
    public async Task RequestReviewer_EmptyLogin_DoesNothing()
    {
        var posts = 0;
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open"))
            .When("/issues/42/comments", "[]")
            .When("/pulls/42/requested_reviewers", req =>
            {
                if (req.Method == HttpMethod.Post)
                {
                    posts++;
                    return new MockResponse(PrJson(42), StatusCode: HttpStatusCode.Created);
                }

                return new MockResponse(RequestedReviewersJson());
            });
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.ReviewerLogin.Value = "  ";
        await vm.RequestReviewerCommand.ExecuteAsync(null);

        Assert.Equal(0, posts);
        vm.Dispose();
    }

    [Fact]
    public async Task RequestReviewer_Unprocessable_StaysOnPage()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open"))
            .When("/issues/42/comments", "[]")
            .When("/pulls/42/requested_reviewers", req =>
            {
                if (req.Method == HttpMethod.Post)
                    return new MockResponse("{}", StatusCode: HttpStatusCode.UnprocessableEntity, AttachRequest: true);
                return new MockResponse(RequestedReviewersJson());
            });
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.ReviewerLogin.Value = "not-a-collaborator";
        await vm.RequestReviewerCommand.ExecuteAsync(null);

        Assert.Contains("collaborator", vm.ErrorMessage.Value, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(vm.PullRequest.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task RequestReviewer_Forbidden_StaysOnPage()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open"))
            .When("/issues/42/comments", "[]")
            .When("/pulls/42/requested_reviewers", req =>
            {
                if (req.Method == HttpMethod.Post)
                    return new MockResponse("{}", StatusCode: HttpStatusCode.Forbidden, AttachRequest: true);
                return new MockResponse(RequestedReviewersJson());
            });
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.ReviewerLogin.Value = "carol";
        await vm.RequestReviewerCommand.ExecuteAsync(null);

        Assert.Contains("Not allowed", vm.ErrorMessage.Value, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(vm.PullRequest.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task RemoveRequestedReviewer_DeletesLoginAndRefreshesList()
    {
        string? deleted = null;
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open"))
            .When("/issues/42/comments", "[]")
            .When("/pulls/42/requested_reviewers", req =>
            {
                if (req.Method == HttpMethod.Delete)
                {
                    deleted = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                    return new MockResponse(PrJson(42));
                }

                if (deleted is null)
                    return new MockResponse(RequestedReviewersJson("[{\"login\":\"carol\"}]"));
                return new MockResponse(RequestedReviewersJson());
            });
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.RemoveRequestedReviewerCommand.ExecuteAsync("carol");

        Assert.Contains("carol", deleted, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(vm.RequestedReviewers);
        vm.Dispose();
    }

    [Fact]
    public async Task RequestReviewer_ClosedPullRequest_DoesNothing()
    {
        var posts = 0;
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "closed"))
            .When("/issues/42/comments", "[]")
            .When("/pulls/42/requested_reviewers", req =>
            {
                if (req.Method == HttpMethod.Post)
                {
                    posts++;
                    return new MockResponse(PrJson(42), StatusCode: HttpStatusCode.Created);
                }

                return new MockResponse(RequestedReviewersJson());
            });
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.CanManageReviewers.Value);
        vm.ReviewerLogin.Value = "carol";
        await vm.RequestReviewerCommand.ExecuteAsync(null);

        Assert.Equal(0, posts);
        vm.Dispose();
    }

    private static string PrJsonWithAssignees(string assigneesJson) =>
        PrJson(42, "open")[..^1] + $",\"assignees\":{assigneesJson}}}";

    [Fact]
    public async Task Load_PopulatesAssigneesFromPullPayload()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJsonWithAssignees("[{\"login\":\"carol\"}]"))
            .When("/issues/42/comments", "[]");
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("carol", Assert.Single(vm.Assignees).Login);
        vm.Dispose();
    }

    [Fact]
    public async Task AddAssignee_PostsLoginAndRefreshesList()
    {
        string? posted = null;
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open"))
            .When("/issues/42/comments", "[]")
            .When("/issues/42/assignees", req =>
            {
                posted = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new MockResponse(
                    "{\"number\":42,\"assignees\":[{\"login\":\"carol\"}]}",
                    StatusCode: HttpStatusCode.Created,
                    AttachRequest: true);
            });
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.AssigneeLogin.Value = "@carol";
        await vm.AddAssigneeCommand.ExecuteAsync(null);

        Assert.Contains("carol", posted, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, vm.AssigneeLogin.Value);
        Assert.Equal("carol", Assert.Single(vm.Assignees).Login);
        vm.Dispose();
    }

    [Fact]
    public async Task AddAssignee_ClosedPullRequest_DoesNothing()
    {
        var posts = 0;
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "closed"))
            .When("/issues/42/comments", "[]")
            .When("/issues/42/assignees", _ =>
            {
                posts++;
                return new MockResponse("{}", StatusCode: HttpStatusCode.Created, AttachRequest: true);
            });
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.AssigneeLogin.Value = "carol";
        await vm.AddAssigneeCommand.ExecuteAsync(null);

        Assert.Equal(0, posts);
        vm.Dispose();
    }

    [Fact]
    public async Task AddAssignee_Forbidden_StaysOnPage()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open"))
            .When("/issues/42/comments", "[]")
            .When("/issues/42/assignees", _ =>
                new MockResponse("{}", StatusCode: HttpStatusCode.Forbidden, AttachRequest: true));
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.AssigneeLogin.Value = "carol";
        await vm.AddAssigneeCommand.ExecuteAsync(null);

        Assert.Contains("Not allowed", vm.ErrorMessage.Value);
        Assert.NotNull(vm.PullRequest.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task RemoveAssignee_DeletesLoginAndRefreshesList()
    {
        HttpRequestMessage? delete = null;
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJsonWithAssignees("[{\"login\":\"carol\"}]"))
            .When("/issues/42/comments", "[]")
            .When("/issues/42/assignees", req =>
            {
                delete = req;
                return new MockResponse(
                    "{\"number\":42,\"assignees\":[]}",
                    StatusCode: HttpStatusCode.OK,
                    AttachRequest: true);
            });
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.RemoveAssigneeCommand.ExecuteAsync("carol");

        Assert.NotNull(delete);
        Assert.Equal(HttpMethod.Delete, delete!.Method);
        Assert.Empty(vm.Assignees);
        vm.Dispose();
    }

    private static string PrJsonWithLabels(string labelsJson) =>
        PrJson(42, "open")[..^1] + $",\"labels\":{labelsJson}}}";

    [Fact]
    public async Task Load_PopulatesLabelsFromPullPayload()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJsonWithLabels("[{\"name\":\"bug\",\"color\":\"ff0000\"}]"))
            .When("/issues/42/comments", "[]");
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("bug", Assert.Single(vm.Labels).Name);
        Assert.Equal("bug", vm.LabelInput.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task SaveLabels_ReplacesLabelsCollection()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open"))
            .When("/issues/42/comments", "[]")
            .When("/issues/42/labels", req =>
            {
                if (req.Method == HttpMethod.Put)
                    return new MockResponse("[{\"name\":\"bug\"},{\"name\":\"wontfix\"}]");
                return new MockResponse("[]");
            });
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.LabelInput.Value = "bug, wontfix";
        await vm.SaveLabelsCommand.ExecuteAsync(null);

        Assert.Empty(vm.ErrorMessage.Value);
        Assert.Equal(["bug", "wontfix"], vm.Labels.Select(l => l.Name));
        vm.Dispose();
    }

    [Fact]
    public async Task SaveLabels_ClosedPullRequest_DoesNothing()
    {
        var puts = 0;
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "closed"))
            .When("/issues/42/comments", "[]")
            .When("/issues/42/labels", _ =>
            {
                puts++;
                return new MockResponse("[]");
            });
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.LabelInput.Value = "bug";
        await vm.SaveLabelsCommand.ExecuteAsync(null);

        Assert.Equal(0, puts);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_OpenPR_SetsCanUpdateBranchTrue()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open"))
            .When("/issues/42/comments", "[]");
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.CanUpdateBranch.Value);
        vm.Dispose();
    }
    [Fact]
    public async Task Load_DraftPR_SetsCanUpdateBranchTrue()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", draft: true))
            .When("/issues/42/comments", "[]");
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.CanUpdateBranch.Value);
        Assert.False(vm.CanMerge.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_MergedPR_SetsCanUpdateBranchFalse()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "closed", merged: true))
            .When("/issues/42/comments", "[]");
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.CanUpdateBranch.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_ClosedPR_SetsCanUpdateBranchFalse()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "closed", merged: false))
            .When("/issues/42/comments", "[]");
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.CanUpdateBranch.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task UpdateBranch_When202_RefreshesPullRequest()
    {
        HttpRequestMessage? put = null;
        var updated = false;
        var handler = new MockHttpHandler()
            .When("/pulls/42", _ => new MockResponse(
                PrJson(42, "open", headSha: updated ? "def456" : "abc123")))
            .When("/issues/42/comments", "[]")
            .When("/pulls/42/update-branch", req =>
            {
                put = req;
                updated = true;
                return new MockResponse(
                    "{\"message\":\"Updating pull request branch.\",\"url\":\"https://api.github.com/repos/owner/repo/pulls/42\"}",
                    StatusCode: HttpStatusCode.Accepted,
                    AttachRequest: true);
            });
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal("abc123", vm.PullRequest.Value!.Head!.Sha);

        await vm.UpdateBranchCommand.ExecuteAsync(null);

        Assert.NotNull(put);
        Assert.Equal(HttpMethod.Put, put!.Method);
        Assert.Equal("def456", vm.PullRequest.Value!.Head!.Sha);
        Assert.Empty(vm.ErrorMessage.Value);
        Assert.False(vm.IsUpdatingBranch.Value);
        Assert.NotNull(vm.PullRequest.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task UpdateBranch_WithoutToken_SetsErrorWithoutRequest()
    {
        var called = false;
        var handler = new MockHttpHandler()
            .When("/pulls/42/update-branch", _ =>
            {
                called = true;
                return new MockResponse("{}", StatusCode: HttpStatusCode.Accepted, AttachRequest: true);
            });
        var vm = new PullRequestDetailViewModel(
            new FakeGitHubClientFactory(handler, token: null), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        vm.PullRequest.Value = new PullRequest { Number = 42, State = "open" };
        vm.CanUpdateBranch.Value = true;

        await vm.UpdateBranchCommand.ExecuteAsync(null);

        Assert.Contains("No token", vm.ErrorMessage.Value);
        Assert.False(called);
        vm.Dispose();
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "Not allowed")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "could not update")]
    public async Task UpdateBranch_HttpFailure_StaysOnPage(HttpStatusCode statusCode, string expected)
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", headSha: "abc123"))
            .When("/issues/42/comments", "[]")
            .When("/pulls/42/update-branch", _ =>
                new MockResponse("{}", StatusCode: statusCode, AttachRequest: true));
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.UpdateBranchCommand.ExecuteAsync(null);

        Assert.Contains(expected, vm.ErrorMessage.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("abc123", vm.PullRequest.Value!.Head!.Sha);
        Assert.False(vm.IsUpdatingBranch.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task UpdateBranch_MergedPR_DoesNotCallApi()
    {
        var called = false;
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "closed", merged: true))
            .When("/issues/42/comments", "[]")
            .When("/pulls/42/update-branch", _ =>
            {
                called = true;
                return new MockResponse("{}", StatusCode: HttpStatusCode.Accepted, AttachRequest: true);
            });
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.UpdateBranchCommand.ExecuteAsync(null);

        Assert.False(called);
        Assert.Empty(vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task UpdateBranch_ClosedPR_DoesNotCallApi()
    {
        var called = false;
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "closed", merged: false))
            .When("/issues/42/comments", "[]")
            .When("/pulls/42/update-branch", _ =>
            {
                called = true;
                return new MockResponse("{}", StatusCode: HttpStatusCode.Accepted, AttachRequest: true);
            });
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.UpdateBranchCommand.ExecuteAsync(null);

        Assert.False(called);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_DraftPR_SetsCanMarkReadyForReviewTrue()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", draft: true))
            .When("/issues/42/comments", "[]");
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.CanMarkReadyForReview.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_OpenNonDraft_SetsCanMarkReadyForReviewFalse()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", draft: false))
            .When("/issues/42/comments", "[]");
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.CanMarkReadyForReview.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_MergedPR_SetsCanMarkReadyForReviewFalse()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "closed", merged: true))
            .When("/issues/42/comments", "[]");
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.CanMarkReadyForReview.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task MarkReadyForReview_When201_RefreshesDraftFalse()
    {
        HttpRequestMessage? post = null;
        var updated = false;
        var handler = new MockHttpHandler()
            .When("/pulls/42", _ => new MockResponse(
                PrJson(42, "open", draft: !updated)))
            .When("/issues/42/comments", "[]")
            .When("/pulls/42/ready_for_review", req =>
            {
                post = req;
                updated = true;
                return new MockResponse(
                    PrJson(42, "open", draft: false),
                    StatusCode: HttpStatusCode.Created,
                    AttachRequest: true);
            });
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.True(vm.PullRequest.Value!.Draft);

        await vm.MarkReadyForReviewCommand.ExecuteAsync(null);

        Assert.NotNull(post);
        Assert.Equal(HttpMethod.Post, post!.Method);
        Assert.False(vm.PullRequest.Value!.Draft);
        Assert.False(vm.CanMarkReadyForReview.Value);
        Assert.Empty(vm.ErrorMessage.Value);
        Assert.False(vm.IsMarkingReadyForReview.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task MarkReadyForReview_WithoutToken_SetsErrorWithoutRequest()
    {
        var called = false;
        var handler = new MockHttpHandler()
            .When("/pulls/42/ready_for_review", _ =>
            {
                called = true;
                return new MockResponse("{}", StatusCode: HttpStatusCode.Created, AttachRequest: true);
            });
        var vm = new PullRequestDetailViewModel(
            new FakeGitHubClientFactory(handler, token: null), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        vm.PullRequest.Value = new PullRequest { Number = 42, State = "open", Draft = true };
        vm.CanMarkReadyForReview.Value = true;

        await vm.MarkReadyForReviewCommand.ExecuteAsync(null);

        Assert.Contains("No token", vm.ErrorMessage.Value);
        Assert.False(called);
        vm.Dispose();
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "Not allowed")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "could not mark")]
    public async Task MarkReadyForReview_HttpFailure_StaysOnPage(HttpStatusCode statusCode, string expected)
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", draft: true))
            .When("/issues/42/comments", "[]")
            .When("/pulls/42/ready_for_review", _ =>
                new MockResponse("{}", StatusCode: statusCode, AttachRequest: true));
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.MarkReadyForReviewCommand.ExecuteAsync(null);

        Assert.Contains(expected, vm.ErrorMessage.Value, StringComparison.OrdinalIgnoreCase);
        Assert.True(vm.PullRequest.Value!.Draft);
        Assert.False(vm.IsMarkingReadyForReview.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task MarkReadyForReview_NonDraft_DoesNotCallApi()
    {
        var called = false;
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "open", draft: false))
            .When("/issues/42/comments", "[]")
            .When("/pulls/42/ready_for_review", _ =>
            {
                called = true;
                return new MockResponse("{}", StatusCode: HttpStatusCode.Created, AttachRequest: true);
            });
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.MarkReadyForReviewCommand.ExecuteAsync(null);

        Assert.False(called);
        vm.Dispose();
    }

    [Fact]
    public async Task MarkReadyForReview_MergedPR_DoesNotCallApi()
    {
        var called = false;
        var handler = new MockHttpHandler()
            .When("/pulls/42", PrJson(42, "closed", merged: true))
            .When("/issues/42/comments", "[]")
            .When("/pulls/42/ready_for_review", _ =>
            {
                called = true;
                return new MockResponse("{}", StatusCode: HttpStatusCode.Created, AttachRequest: true);
            });
        var vm = new PullRequestDetailViewModel(new FakeGitHubClientFactory(handler), new FakeBrowserLauncher());
        vm.Initialize("owner", "repo", 42);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.MarkReadyForReviewCommand.ExecuteAsync(null);

        Assert.False(called);
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
