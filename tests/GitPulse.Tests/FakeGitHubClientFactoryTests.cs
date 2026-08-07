using System.Net.Http.Headers;
using GitPulse.Tests.TestHelpers;
using Xunit;

namespace GitPulse.Tests;

public class FakeGitHubClientFactoryTests
{
    [Fact]
    public async Task CreatePagedSessionAsync_WithToken_ReturnsAuthenticatedSession()
    {
        var factory = new FakeGitHubClientFactory(new MockHttpHandler());

        using var session = await factory.CreatePagedSessionAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(session.Client);
        Assert.Equal("https://api.github.com/", session.Client.BaseAddress?.OriginalString);
        Assert.NotNull(session.Client.DefaultRequestHeaders.Authorization);
        Assert.Equal("Bearer", session.Client.DefaultRequestHeaders.Authorization.Scheme);
        Assert.Equal("ghp_fake_test_token", session.Client.DefaultRequestHeaders.Authorization.Parameter);
        Assert.Contains(session.Client.DefaultRequestHeaders.Accept,
            h => h.MediaType == "application/vnd.github+json");
        Assert.True(session.Client.DefaultRequestHeaders.Contains("X-GitHub-Api-Version"));
        Assert.Equal("GitPulse", session.Client.DefaultRequestHeaders.UserAgent.ToString());
        Assert.False(session.HasNextPage);
    }

    [Fact]
    public async Task CreatePagedSessionAsync_WithoutToken_ReturnsSessionWithNoAuth()
    {
        var factory = new FakeGitHubClientFactory(new MockHttpHandler(), token: null);

        using var session = await factory.CreatePagedSessionAsync(TestContext.Current.CancellationToken);

        Assert.Null(session.Client.DefaultRequestHeaders.Authorization);
        Assert.False(session.HasNextPage);
    }

    [Fact]
    public async Task CreatePagedSessionAsync_PrepareRequestAndAdvance_InjectsPageViaMockHttp()
    {
        string? capturedQuery = null;
        var handler = new MockHttpHandler()
            .When("/user/repos", req =>
            {
                capturedQuery = req.RequestUri?.Query;
                return new MockResponse("[]");
            });
        var factory = new FakeGitHubClientFactory(handler);

        using var session = await factory.CreatePagedSessionAsync(TestContext.Current.CancellationToken);
        session.Reset();
        session.ApplyLink(HeadersWithNextPage(2));
        Assert.True(session.HasNextPage);
        Assert.True(session.Advance());
        session.PrepareRequest();

        using var response = await session.Client.GetAsync(
            "user/repos", TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("page=2", capturedQuery);
    }

    [Fact]
    public async Task CreatePagedSessionAsync_PrepareRequest_WritesStateViaMockHttp()
    {
        string? capturedQuery = null;
        var handler = new MockHttpHandler()
            .When("/repos/owner/repo/issues", req =>
            {
                capturedQuery = req.RequestUri?.Query;
                return new MockResponse("[]");
            });
        var factory = new FakeGitHubClientFactory(handler);

        using var session = await factory.CreatePagedSessionAsync(TestContext.Current.CancellationToken);
        session.State = "closed";
        session.Reset();
        session.PrepareRequest();

        using var response = await session.Client.GetAsync(
            "repos/owner/repo/issues", TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("state=closed", capturedQuery);
    }

    [Fact]
    public async Task CreatePagedSessionAsync_Dispose_DisposesClient()
    {
        var factory = new FakeGitHubClientFactory(new MockHttpHandler());
        var session = await factory.CreatePagedSessionAsync(TestContext.Current.CancellationToken);
        var client = session.Client;
        session.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => client.GetAsync("https://api.github.com/", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreatePagedClientAsync_WithToken_ReturnsClientAndQueryHandler()
    {
        var factory = new FakeGitHubClientFactory(new MockHttpHandler());

        var (client, queryHandler) = await factory.CreatePagedClientAsync(
            TestContext.Current.CancellationToken);

        Assert.NotNull(client);
        Assert.NotNull(client.DefaultRequestHeaders.Authorization);
        Assert.NotNull(queryHandler);
        Assert.Equal(1, queryHandler.Page);
        Assert.Equal(30, queryHandler.PerPage);
        client.Dispose();
    }

    private static HttpResponseHeaders HeadersWithNextPage(int page)
    {
        var response = new HttpResponseMessage();
        response.Headers.Add(
            "Link",
            $"<https://api.github.com/user/repos?page={page}>; rel=\"next\"");
        return response.Headers;
    }
}
