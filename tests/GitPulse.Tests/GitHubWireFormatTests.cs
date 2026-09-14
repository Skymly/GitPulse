using System.Net;
using System.Text.Json;
using GitPulse.Core.Models;
using GitPulse.Tests.TestHelpers;
using Xunit;

namespace GitPulse.Tests;

public class GitHubWireFormatTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void OfficialPullRequest_FillsHeadBaseHtmlUrlAndCreatedAt()
    {
        var pr = JsonSerializer.Deserialize<PullRequest>(GitHubJson.OfficialPullRequest, Options);

        Assert.NotNull(pr);
        Assert.Equal(1347, pr.Number);
        Assert.Equal("new-topic", pr.Head?.Ref);
        Assert.Equal("master", pr.Base?.Ref);
        Assert.Equal("new-topic", pr.HeadRef);
        Assert.Equal("master", pr.BaseRef);
        Assert.Equal("https://github.com/octocat/Hello-World/pull/1347", pr.HtmlUrl);
        Assert.Equal(new DateTime(2011, 1, 26, 19, 1, 12, DateTimeKind.Utc), pr.CreatedAt);
        Assert.Equal("octocat", pr.MergedBy?.Login);
        Assert.Equal("https://github.com/images/error/octocat_happy.gif", pr.MergedBy?.AvatarUrl);
        Assert.DoesNotContain("headRef", GitHubJson.OfficialPullRequest, StringComparison.Ordinal);
        Assert.DoesNotContain("baseRef", GitHubJson.OfficialPullRequest, StringComparison.Ordinal);
    }

    [Fact]
    public void OfficialIssue_FillsCommentsCountHtmlUrlAndPullRequestUrls()
    {
        var issue = JsonSerializer.Deserialize<Issue>(GitHubJson.OfficialIssue, Options);

        Assert.NotNull(issue);
        Assert.Equal(1347, issue.Number);
        Assert.Equal(0, issue.CommentsCount);
        Assert.Equal("https://github.com/octocat/Hello-World/issues/1347", issue.HtmlUrl);
        Assert.Equal(new DateTime(2011, 4, 22, 13, 33, 48, DateTimeKind.Utc), issue.CreatedAt);
        Assert.Equal("https://github.com/octocat/Hello-World/pull/1347", issue.PullRequestRef?.HtmlUrl);
        Assert.Equal("https://github.com/octocat/Hello-World/pull/1347.diff", issue.PullRequestRef?.DiffUrl);
        Assert.True(issue.IsPullRequest);
    }

    [Fact]
    public void OfficialComment_FillsCreatedAtAndHtmlUrl()
    {
        var comment = JsonSerializer.Deserialize<Comment>(GitHubJson.OfficialComment, Options);

        Assert.NotNull(comment);
        Assert.Equal("Me too", comment.Body);
        Assert.Equal(new DateTime(2011, 4, 14, 16, 0, 49, DateTimeKind.Utc), comment.CreatedAt);
        Assert.Equal("https://github.com/octocat/Hello-World/issues/1347#issuecomment-1", comment.HtmlUrl);
        Assert.Equal("https://github.com/octocat", comment.User?.HtmlUrl);
    }
}

public class MockHttpHandlerTests
{
    [Fact]
    public async Task SendAsync_CancelledToken_Throws()
    {
        var handler = new MockHttpHandler().When("/repos/o/r", "{}");
        using var client = new HttpClient(handler);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

#pragma warning disable xUnit1051 // this test's point is a cancelled token, not the test host's
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GetAsync("https://api.github.com/repos/o/r", cts.Token));
#pragma warning restore xUnit1051
    }

    [Fact]
    public async Task When_MethodSpecific_DoesNotAnswerOtherMethods()
    {
        var handler = new MockHttpHandler()
            .When(HttpMethod.Get, "/issues/42", GitHubJson.Issue(42));
        using var client = new HttpClient(handler);
        var ct = TestContext.Current.CancellationToken;

        var get = await client.GetAsync("https://api.github.com/repos/o/r/issues/42", ct);
        var patch = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Patch, "https://api.github.com/repos/o/r/issues/42"),
            ct);

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, patch.StatusCode);
    }

    [Fact]
    public async Task When_MethodSpecific_WinsOverWildcardOnSameSuffix()
    {
        var handler = new MockHttpHandler()
            .When("/issues/42", GitHubJson.Issue(1))
            .When(HttpMethod.Get, "/issues/42", GitHubJson.Issue(42));
        using var client = new HttpClient(handler);

        var body = await client.GetStringAsync(
            "https://api.github.com/repos/o/r/issues/42",
            TestContext.Current.CancellationToken);

        Assert.Contains("\"number\":42", body, StringComparison.Ordinal);
    }
}
