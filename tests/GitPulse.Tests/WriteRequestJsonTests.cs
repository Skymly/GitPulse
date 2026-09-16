using System.Text.Json;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using GitPulse.Tests.TestHelpers;
using Observables.RestAPI;
using R3;
using Xunit;

namespace GitPulse.Tests;

public class WriteRequestJsonTests
{
    [Fact]
    public void IssueUpdateRequest_CloseOnly_OmitsUnsetMembers()
    {
        var json = JsonSerializer.Serialize(new IssueUpdateRequest { State = "closed" });

        Assert.Equal("""{"state":"closed"}""", json);
        Assert.DoesNotContain("title", json, StringComparison.Ordinal);
        Assert.DoesNotContain("body", json, StringComparison.Ordinal);
        Assert.DoesNotContain("labels", json, StringComparison.Ordinal);
    }

    [Fact]
    public void IssueUpdateRequest_TitleBodySave_OmitsStateAndLabels()
    {
        var json = JsonSerializer.Serialize(new IssueUpdateRequest
        {
            Title = "Keep the title",
            Body = "Keep the body",
        });

        Assert.Equal("""{"title":"Keep the title","body":"Keep the body"}""", json);
        Assert.DoesNotContain("state", json, StringComparison.Ordinal);
        Assert.DoesNotContain("labels", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewCommentRequest_UnsetLine_OmitsLineMember()
    {
        var json = JsonSerializer.Serialize(
            new ReviewCommentRequest
            {
                Body = "file comment",
                CommitId = "abc",
                Path = "src/Foo.cs",
            },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.DoesNotContain("\"line\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewCommentRequest_FileSubject_OmitsLineAndWritesSubjectType()
    {
        var json = JsonSerializer.Serialize(
            new ReviewCommentRequest
            {
                Body = "file comment",
                CommitId = "abc",
                Path = "src/Foo.cs",
                SubjectType = "file",
            },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.DoesNotContain("\"line\"", json, StringComparison.Ordinal);
        Assert.Contains("\"subject_type\":\"file\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewCommentRequest_WithLine_WritesLineAndOmitsSubjectType()
    {
        var json = JsonSerializer.Serialize(
            new ReviewCommentRequest
            {
                Body = "line comment",
                CommitId = "abc",
                Path = "src/Foo.cs",
                Line = 12,
            },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.Contains("\"line\":12", json, StringComparison.Ordinal);
        Assert.DoesNotContain("subject_type", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewCommentRequest_LineZero_OmitsLineMember()
    {
        var json = JsonSerializer.Serialize(
            new ReviewCommentRequest
            {
                Body = "file comment",
                CommitId = "abc",
                Path = "src/Foo.cs",
                Line = 0,
            },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.DoesNotContain("\"line\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IssueUpdateRequest_CloseOnly_RestApiOmitsUnsetMembers()
    {
        var json = await CaptureIssuePatchAsync(new IssueUpdateRequest { State = "closed" });

        Assert.Equal("""{"state":"closed"}""", json);
        Assert.DoesNotContain("title", json, StringComparison.Ordinal);
        Assert.DoesNotContain("body", json, StringComparison.Ordinal);
        Assert.DoesNotContain("labels", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IssueUpdateRequest_TitleBodySave_RestApiOmitsStateAndLabels()
    {
        var json = await CaptureIssuePatchAsync(new IssueUpdateRequest
        {
            Title = "Keep the title",
            Body = "Keep the body",
        });

        Assert.Equal("""{"title":"Keep the title","body":"Keep the body"}""", json);
        Assert.DoesNotContain("state", json, StringComparison.Ordinal);
        Assert.DoesNotContain("labels", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReviewCommentRequest_FileSubject_RestApiOmitsLineAndWritesSubjectType()
    {
        var json = await CaptureReviewCommentAsync(new ReviewCommentRequest
        {
            Body = "file comment",
            CommitId = "abc",
            Path = "src/Foo.cs",
            SubjectType = "file",
        });

        Assert.DoesNotContain("\"line\"", json, StringComparison.Ordinal);
        Assert.Contains("\"subject_type\":\"file\"", json, StringComparison.Ordinal);
        Assert.Contains("\"commit_id\":\"abc\"", json, StringComparison.Ordinal);
        Assert.Contains("\"body\":\"file comment\"", json, StringComparison.Ordinal);
        Assert.Contains("\"path\":\"src/Foo.cs\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReviewCommentRequest_WithLine_RestApiWritesLineAndOmitsSubjectType()
    {
        var json = await CaptureReviewCommentAsync(new ReviewCommentRequest
        {
            Body = "line comment",
            CommitId = "abc",
            Path = "src/Foo.cs",
            Line = 12,
        });

        Assert.Contains("\"line\":12", json, StringComparison.Ordinal);
        Assert.DoesNotContain("subject_type", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReviewCommentRequest_LineZero_RestApiOmitsLineMember()
    {
        var json = await CaptureReviewCommentAsync(new ReviewCommentRequest
        {
            Body = "file comment",
            CommitId = "abc",
            Path = "src/Foo.cs",
            Line = 0,
        });

        Assert.DoesNotContain("\"line\"", json, StringComparison.Ordinal);
    }

    private static async Task<string> CaptureIssuePatchAsync(IssueUpdateRequest body)
    {
        string? json = null;
        var handler = new MockHttpHandler()
            .When(HttpMethod.Patch, "/issues/1", req =>
            {
                json = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new MockResponse(GitHubJson.Issue(1));
            });
        var factory = new FakeGitHubClientFactory(handler);
        using var client = await factory.CreateClientAsync(TestContext.Current.CancellationToken);
        var api = RestService.For<IGitHubReposApi>(client);

        await api.UpdateIssue("o", "r", 1, body).FirstAsync(TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrEmpty(json));
        return json;
    }

    private static async Task<string> CaptureReviewCommentAsync(ReviewCommentRequest body)
    {
        string? json = null;
        var handler = new MockHttpHandler()
            .When(HttpMethod.Post, "/pulls/1/comments", req =>
            {
                json = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new MockResponse(
                    """{"id":1,"body":"ok","path":"src/Foo.cs","commit_id":"abc","diff_hunk":"","original_commit_id":""}""");
            });
        var factory = new FakeGitHubClientFactory(handler);
        using var client = await factory.CreateClientAsync(TestContext.Current.CancellationToken);
        var api = RestService.For<IGitHubReposApi>(client);

        await api.CreateReviewComment("o", "r", 1, body).FirstAsync(TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrEmpty(json));
        return json;
    }
}
