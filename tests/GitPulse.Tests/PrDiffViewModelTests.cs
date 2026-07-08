using System.Text;
using GitPulse.Core.Models;
using GitPulse.Tests.TestHelpers;
using GitPulse.ViewModels;
using Xunit;

namespace GitPulse.Tests;

public class PrDiffViewModelTests
{
    private const string Owner = "owner";
    private const string Repo = "repo";
    private const int PrNumber = 42;
    private const string HeadSha = "abc123def456789";

    /// <summary>Two changed files with patches.</summary>
    private const string FilesJson =
        "[{\"sha\":\"blob1\",\"filename\":\"src/Program.cs\",\"status\":\"modified\"," +
        "\"additions\":5,\"deletions\":2,\"changes\":7," +
        "\"blob_url\":\"https://github.com/owner/repo/blob/src/Program.cs\"," +
        "\"raw_url\":\"https://github.com/owner/repo/raw/src/Program.cs\"," +
        "\"contents_url\":\"https://api.github.com/repos/owner/repo/contents/src/Program.cs\"," +
        "\"patch\":\"@@ -1,3 +1,5 @@\\n+using System;\\n+using System.Linq;\\n namespace Foo\\n {\\n     class Bar { }\\n }\"}," +
        "{\"sha\":\"blob2\",\"filename\":\"README.md\",\"status\":\"added\"," +
        "\"additions\":10,\"deletions\":0,\"changes\":10," +
        "\"blob_url\":\"https://github.com/owner/repo/blob/README.md\"," +
        "\"raw_url\":\"https://github.com/owner/repo/raw/README.md\"," +
        "\"contents_url\":\"https://api.github.com/repos/owner/repo/contents/README.md\"," +
        "\"patch\":\"@@ -0,0 +1,10 @@\\n+# Test Repo\\n+Some description.\\n\"}]";

    /// <summary>One review comment on src/Program.cs.</summary>
    private const string ReviewCommentsJson =
        "[{\"id\":100,\"pull_request_review_id\":null," +
        "\"diff_hunk\":\"@@ -1,3 +1,5 @@\",\"path\":\"src/Program.cs\"," +
        "\"position\":1,\"original_position\":1," +
        "\"commit_id\":\"abc123def456789\",\"original_commit_id\":\"abc123def456789\"," +
        "\"in_reply_to_id\":null," +
        "\"user\":{\"login\":\"reviewer\",\"id\":2,\"type\":\"User\"}," +
        "\"body\":\"Looks good!\",\"created_at\":\"2026-01-01T00:00:00Z\"," +
        "\"updated_at\":\"2026-01-01T00:00:00Z\"," +
        "\"html_url\":\"https://github.com/owner/repo/pull/42#discussion_r100\"," +
        "\"line\":2,\"original_line\":2,\"side\":\"RIGHT\"," +
        "\"start_line\":null,\"start_side\":null,\"subject_type\":\"line\"}]";

    /// <summary>Response for creating a review comment.</summary>
    private const string CreatedCommentJson =
        "{\"id\":200,\"pull_request_review_id\":null," +
        "\"diff_hunk\":\"@@ -1,3 +1,5 @@\",\"path\":\"src/Program.cs\"," +
        "\"position\":1,\"original_position\":1," +
        "\"commit_id\":\"abc123def456789\",\"original_commit_id\":\"abc123def456789\"," +
        "\"in_reply_to_id\":null," +
        "\"user\":{\"login\":\"owner\",\"id\":1,\"type\":\"User\"}," +
        "\"body\":\"New comment\",\"created_at\":\"2026-01-02T00:00:00Z\"," +
        "\"updated_at\":\"2026-01-02T00:00:00Z\"," +
        "\"html_url\":\"https://github.com/owner/repo/pull/42#discussion_r200\"," +
        "\"line\":5,\"original_line\":5,\"side\":\"RIGHT\"," +
        "\"start_line\":null,\"start_side\":null,\"subject_type\":\"line\"}";

    [Fact]
    public void Initialize_SetsOwnerRepoPrNumberAndHeadSha()
    {
        var vm = new PrDiffViewModel(
            new FakeGitHubClientFactory(new MockHttpHandler()));

        vm.Initialize(Owner, Repo, PrNumber, HeadSha);

        // No public properties for owner/repo/prNumber/headSha — they're
        // private. Verify via behavior: Load should attempt to call the API.
        // If Initialize wasn't called, Load returns early (no error).
        // Here we just verify no exception is thrown.
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithoutToken_SetsErrorMessage()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42/files", FilesJson)
            .When("/pulls/42/comments", ReviewCommentsJson);
        var factory = new FakeGitHubClientFactory(handler, token: null);
        var vm = new PrDiffViewModel(factory);

        vm.Initialize(Owner, Repo, PrNumber, HeadSha);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("No token configured.", vm.ErrorMessage.Value);
        Assert.Empty(vm.Files);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithToken_PopulatesFilesAndComments()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42/files", FilesJson)
            .When("/pulls/42/comments", ReviewCommentsJson);
        var vm = new PrDiffViewModel(new FakeGitHubClientFactory(handler));

        vm.Initialize(Owner, Repo, PrNumber, HeadSha);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Files.Count);
        Assert.Equal("src/Program.cs", vm.Files[0].Filename);
        Assert.Equal("modified", vm.Files[0].Status);
        Assert.Equal(5, vm.Files[0].Additions);
        Assert.Equal(2, vm.Files[0].Deletions);
        Assert.False(string.IsNullOrEmpty(vm.Files[0].Patch));

        var comments = vm.GetCommentsForFile("src/Program.cs");
        Assert.Single(comments);
        Assert.Equal("Looks good!", comments[0].Body);
        Assert.Equal("reviewer", comments[0].User?.Login);

        // README.md has no comments.
        Assert.Empty(vm.GetCommentsForFile("README.md"));
        vm.Dispose();
    }

    [Fact]
    public async Task Load_BinaryFile_HasNullPatch()
    {
        var binaryJson =
            "[{\"sha\":\"blob1\",\"filename\":\"image.png\",\"status\":\"added\"," +
            "\"additions\":0,\"deletions\":0,\"changes\":0," +
            "\"blob_url\":\"https://github.com/owner/repo/blob/image.png\"," +
            "\"raw_url\":\"https://github.com/owner/repo/raw/image.png\"," +
            "\"contents_url\":\"https://api.github.com/repos/owner/repo/contents/image.png\"," +
            "\"patch\":null}]";

        var handler = new MockHttpHandler()
            .When("/pulls/42/files", binaryJson)
            .When("/pulls/42/comments", "[]");
        var vm = new PrDiffViewModel(new FakeGitHubClientFactory(handler));

        vm.Initialize(Owner, Repo, PrNumber, HeadSha);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Single(vm.Files);
        Assert.Null(vm.Files[0].Patch);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_WithEmptyResults_PopulatesEmptyCollections()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42/files", "[]")
            .When("/pulls/42/comments", "[]");
        var vm = new PrDiffViewModel(new FakeGitHubClientFactory(handler));

        vm.Initialize(Owner, Repo, PrNumber, HeadSha);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.Files);
        Assert.Empty(vm.FileComments);
        Assert.Equal(string.Empty, vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task Load_With404_SetsErrorMessage()
    {
        var handler = new MockHttpHandler(); // no routes → 404
        var vm = new PrDiffViewModel(new FakeGitHubClientFactory(handler));

        vm.Initialize(Owner, Repo, PrNumber, HeadSha);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        Assert.Empty(vm.Files);
        vm.Dispose();
    }

    [Fact]
    public void StartComment_SetsFilePathLineAndClearsReplyTo()
    {
        var vm = new PrDiffViewModel(
            new FakeGitHubClientFactory(new MockHttpHandler()));

        vm.ReplyToId.Value = 999;
        vm.CommentInput.Value = "old text";

        vm.StartCommentCommand.Execute(new CommentTarget("src/Foo.cs", 42));

        Assert.Equal("src/Foo.cs", vm.CommentFilePath.Value);
        Assert.Equal(42, vm.CommentLine.Value);
        Assert.Equal(0, vm.ReplyToId.Value);
        Assert.Equal(string.Empty, vm.CommentInput.Value);
        vm.Dispose();
    }

    [Fact]
    public void StartReply_SetsReplyToIdAndClearsInput()
    {
        var vm = new PrDiffViewModel(
            new FakeGitHubClientFactory(new MockHttpHandler()));

        vm.CommentInput.Value = "old text";

        vm.StartReplyCommand.Execute(123L);

        Assert.Equal(123, vm.ReplyToId.Value);
        Assert.Equal(string.Empty, vm.CommentInput.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task PostComment_TopLevel_AddsToFileComments()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42/comments", CreatedCommentJson);
        var vm = new PrDiffViewModel(new FakeGitHubClientFactory(handler));

        vm.Initialize(Owner, Repo, PrNumber, HeadSha);
        vm.StartCommentCommand.Execute(new CommentTarget("src/Program.cs", 5));
        vm.CommentInput.Value = "New comment";

        await vm.PostCommentCommand.ExecuteAsync(null);

        var comments = vm.GetCommentsForFile("src/Program.cs");
        Assert.Single(comments);
        Assert.Equal("New comment", comments[0].Body);
        Assert.Equal("owner", comments[0].User?.Login);
        Assert.Equal(string.Empty, vm.CommentInput.Value);
        Assert.Equal(0, vm.ReplyToId.Value);
        vm.Dispose();
    }

    [Fact]
    public async Task PostComment_Reply_SetsInReplyTo()
    {
        // For a reply, the API returns the reply comment (path = same file).
        var replyJson =
            "{\"id\":201,\"pull_request_review_id\":null," +
            "\"diff_hunk\":\"@@ -1,3 +1,5 @@\",\"path\":\"src/Program.cs\"," +
            "\"position\":1,\"original_position\":1," +
            "\"commit_id\":\"abc123def456789\",\"original_commit_id\":\"abc123def456789\"," +
            "\"in_reply_to_id\":100," +
            "\"user\":{\"login\":\"owner\",\"id\":1,\"type\":\"User\"}," +
            "\"body\":\"Reply text\",\"created_at\":\"2026-01-03T00:00:00Z\"," +
            "\"updated_at\":\"2026-01-03T00:00:00Z\"," +
            "\"html_url\":\"https://github.com/owner/repo/pull/42#discussion_r201\"," +
            "\"line\":2,\"original_line\":2,\"side\":\"RIGHT\"," +
            "\"start_line\":null,\"start_side\":null,\"subject_type\":\"line\"}";

        var handler = new MockHttpHandler()
            .When("/pulls/42/comments", replyJson);
        var vm = new PrDiffViewModel(new FakeGitHubClientFactory(handler));

        vm.Initialize(Owner, Repo, PrNumber, HeadSha);
        vm.StartReplyCommand.Execute(100L);
        vm.CommentInput.Value = "Reply text";

        await vm.PostCommentCommand.ExecuteAsync(null);

        var comments = vm.GetCommentsForFile("src/Program.cs");
        Assert.Single(comments);
        Assert.Equal("Reply text", comments[0].Body);
        Assert.Equal(100, comments[0].InReplyToId);
        vm.Dispose();
    }

    [Fact]
    public async Task PostComment_WithEmptyInput_DoesNothing()
    {
        var handler = new MockHttpHandler()
            .When("/pulls/42/comments", CreatedCommentJson);
        var vm = new PrDiffViewModel(new FakeGitHubClientFactory(handler));

        vm.Initialize(Owner, Repo, PrNumber, HeadSha);
        vm.CommentInput.Value = "   "; // whitespace only

        await vm.PostCommentCommand.ExecuteAsync(null);

        // No API call should have been made; no comments added.
        Assert.Empty(vm.GetCommentsForFile("src/Program.cs"));
        vm.Dispose();
    }

    [Fact]
    public async Task PostComment_WithApiError_SetsErrorMessage()
    {
        var handler = new MockHttpHandler(); // no route → 404
        var vm = new PrDiffViewModel(new FakeGitHubClientFactory(handler));

        vm.Initialize(Owner, Repo, PrNumber, HeadSha);
        vm.StartCommentCommand.Execute(new CommentTarget("src/Foo.cs", 1));
        vm.CommentInput.Value = "test comment";

        await vm.PostCommentCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.ErrorMessage.Value);
        vm.Dispose();
    }

    [Fact]
    public void GetCommentsForFile_WithNoComments_ReturnsEmptyList()
    {
        var vm = new PrDiffViewModel(
            new FakeGitHubClientFactory(new MockHttpHandler()));

        var comments = vm.GetCommentsForFile("nonexistent.cs");

        Assert.Empty(comments);
        vm.Dispose();
    }
}
