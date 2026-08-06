using System.Net.Http.Headers;
using GitPulse.Core.Http;
using Xunit;

namespace GitPulse.Tests;

public class PagedGitHubSessionTests
{
    [Fact]
    public async Task Reset_AfterAdvance_RestoresPage1AndClearsHasNextPage()
    {
        using var session = CreateSession(out var capturing);
        session.ApplyLink(HeadersWithNextPage(2));
        Assert.True(session.Advance());
        session.PrepareRequest();
        Assert.Contains("page=2", await capturing.SendThroughAsync(session));

        session.Reset();
        session.PrepareRequest();

        Assert.False(session.HasNextPage);
        Assert.DoesNotContain("page=", await capturing.SendThroughAsync(session));
    }

    [Fact]
    public async Task PrepareRequest_AfterReset_DoesNotInjectPageParam()
    {
        using var session = CreateSession(out var capturing);
        session.Reset();
        session.PrepareRequest();

        var uri = await capturing.SendThroughAsync(session);

        Assert.DoesNotContain("page=", uri);
    }

    [Fact]
    public async Task PrepareRequest_AfterAdvance_InjectsNextPage()
    {
        using var session = CreateSession(out var capturing);
        session.Reset();
        session.ApplyLink(HeadersWithNextPage(2));
        Assert.True(session.Advance());
        session.PrepareRequest();

        var uri = await capturing.SendThroughAsync(session);

        Assert.Contains("page=2", uri);
    }

    [Fact]
    public void ApplyLink_WithNextRel_SetsHasNextPage()
    {
        using var session = CreateSession();

        session.ApplyLink(HeadersWithNextPage(2));

        Assert.True(session.HasNextPage);
    }

    [Fact]
    public void ApplyLink_WithoutNextRel_ClearsHasNextPage()
    {
        using var session = CreateSession();
        session.ApplyLink(HeadersWithNextPage(2));

        session.ApplyLink(HeadersWithoutNextPage());

        Assert.False(session.HasNextPage);
    }

    [Fact]
    public void ApplyLink_NullHeaders_ClearsHasNextPage()
    {
        using var session = CreateSession();
        session.ApplyLink(HeadersWithNextPage(2));

        session.ApplyLink(null);

        Assert.False(session.HasNextPage);
    }

    [Fact]
    public async Task Advance_WhenNoNextPage_ReturnsFalseAndKeepsPage()
    {
        using var session = CreateSession(out var capturing);
        session.Reset();

        Assert.False(session.Advance());

        session.PrepareRequest();
        var uri = await capturing.SendThroughAsync(session);
        Assert.DoesNotContain("page=", uri);
    }

    [Fact]
    public void Advance_WhenHasNextPage_ReturnsTrue()
    {
        using var session = CreateSession();
        session.Reset();
        session.ApplyLink(HeadersWithNextPage(2));

        Assert.True(session.Advance());
    }

    [Fact]
    public async Task PrepareRequest_WritesStateToHandler()
    {
        using var session = CreateSession(out var capturing);
        session.State = "open";
        session.Reset();
        session.PrepareRequest();

        var uri = await capturing.SendThroughAsync(session);

        Assert.Contains("state=open", uri);
    }

    [Fact]
    public async Task PrepareRequest_StateAll_DoesNotInjectState()
    {
        using var session = CreateSession(out var capturing);
        session.State = "all";
        session.Reset();
        session.PrepareRequest();

        var uri = await capturing.SendThroughAsync(session);

        Assert.DoesNotContain("state=", uri);
    }

    [Fact]
    public void Reset_PreservesState()
    {
        using var session = CreateSession();
        session.State = "closed";

        session.Reset();

        Assert.Equal("closed", session.State);
    }

    [Fact]
    public async Task Dispose_DisposesClient()
    {
        var session = CreateSession();
        var client = session.Client;
        session.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => client.GetAsync("https://api.github.com/", TestContext.Current.CancellationToken));
    }

    private static PagedGitHubSession CreateSession() => CreateSession(out _);

    private static PagedGitHubSession CreateSession(out CapturingHandler capturing)
    {
        capturing = new CapturingHandler();
        var queryHandler = new GitHubQueryHandler(capturing);
        var client = new HttpClient(queryHandler, disposeHandler: true)
        {
            BaseAddress = new Uri("https://api.github.com/"),
        };
        return new PagedGitHubSession(client, queryHandler);
    }

    private static HttpResponseHeaders HeadersWithNextPage(int page)
    {
        var response = new HttpResponseMessage();
        response.Headers.Add(
            "Link",
            $"<https://api.github.com/user/repos?page={page}>; rel=\"next\"");
        return response.Headers;
    }

    private static HttpResponseHeaders HeadersWithoutNextPage()
    {
        var response = new HttpResponseMessage();
        response.Headers.Add(
            "Link",
            "<https://api.github.com/user/repos?page=1>; rel=\"first\"");
        return response.Headers;
    }

    /// <summary>
    /// Inner handler that returns 200 OK and lets tests send through the session client.
    /// </summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public async Task<string> SendThroughAsync(PagedGitHubSession session)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/repos");
            _ = await session.Client.SendAsync(request, TestContext.Current.CancellationToken);
            return request.RequestUri?.OriginalString ?? "";
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage());
    }
}
