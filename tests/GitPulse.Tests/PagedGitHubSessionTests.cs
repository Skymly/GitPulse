using System.Net;
using System.Net.Http.Headers;
using System.Text;
using GitPulse.Core.Http;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;
using Xunit;

namespace GitPulse.Tests;

public class PagedGitHubSessionTests
{
    [Fact]
    public async Task Reset_AfterAdvance_RestoresPage1AndClearsHasNextPage()
    {
        using var session = CreateSession();
        session.ApplyLink(HeadersWithNextPage(2));
        Assert.True(session.Advance());
        Assert.Contains("page=2", await SendThroughAsync(session));

        session.Reset();

        Assert.False(session.HasNextPage);
        Assert.DoesNotContain("page=", await SendThroughAsync(session));
    }

    [Fact]
    public async Task PrepareRequest_AfterReset_DoesNotInjectPageParam()
    {
        using var session = CreateSession();
        session.Reset();

        var uri = await SendThroughAsync(session);

        Assert.DoesNotContain("page=", uri);
    }

    [Fact]
    public async Task PrepareRequest_AfterAdvance_InjectsNextPage()
    {
        using var session = CreateSession();
        session.Reset();
        session.ApplyLink(HeadersWithNextPage(2));
        Assert.True(session.Advance());

        var uri = await SendThroughAsync(session);

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
        using var session = CreateSession();
        session.Reset();

        Assert.False(session.Advance());

        var uri = await SendThroughAsync(session);
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
    public async Task Advance_WithoutApplyLink_DoesNotCommitCursor()
    {
        using var session = CreateSession();
        session.Reset();
        session.ApplyLink(HeadersWithNextPage(2));
        Assert.True(session.Advance());
        Assert.Contains("page=2", await SendThroughAsync(session));

        // Failed LoadMore never calls ApplyLink — retry must request the same page.
        Assert.True(session.Advance());
        Assert.Contains("page=2", await SendThroughAsync(session));

        session.ApplyLink(HeadersWithNextPage(3));
        Assert.True(session.Advance());
        Assert.Contains("page=3", await SendThroughAsync(session));
    }

    [Fact]
    public async Task PrepareRequest_StateOpen_InjectsState()
    {
        using var session = CreateSession();
        session.State = "open";
        session.Reset();

        var uri = await SendThroughAsync(session);

        Assert.Contains("state=open", uri);
    }

    [Fact]
    public async Task PrepareRequest_StateAll_InjectsState()
    {
        using var session = CreateSession();
        session.State = "all";
        session.Reset();

        var uri = await SendThroughAsync(session);

        Assert.Contains("state=all", uri);
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

    [Fact]
    public async Task PrepareRequest_OverlappingProxyCalls_UseTheirOwnPageAndState()
    {
        var testCt = TestContext.Current.CancellationToken;
        var timeout = TimeSpan.FromSeconds(5);
        using var session = CreateSession(out var capturing);
        var api = RestService.For<IGitHubReposApi>(session.Client);

        session.State = "closed";
        session.ApplyLink(HeadersWithNextPage(2));
        Assert.True(session.Advance());
        var older = session.PrepareRequest(() => api.ListIssuesPaged("owner", "older"));

        session.Reset();
        session.State = "open";
        var newer = session.PrepareRequest(() => api.ListIssuesPaged("owner", "newer"));

        var unscoped = api.ListIssuesPaged("owner", "unscoped");

        var started = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        capturing.BeforeResponse = async (request, ct) =>
        {
            if (request.RequestUri?.AbsolutePath == "/repos/owner/newer/issues")
            {
                started.TrySetResult(request.RequestUri);
                await release.Task.WaitAsync(ct);
            }
        };

        var newerTask = newer.FirstAsync(testCt);
        try
        {
            var newerUri = await started.Task.WaitAsync(timeout, testCt);
            using var olderResponse = await older.FirstAsync(testCt).WaitAsync(timeout, testCt);
            using var unscopedResponse = await unscoped.FirstAsync(testCt).WaitAsync(timeout, testCt);

            Assert.True(olderResponse.IsSuccessStatusCode);
            var olderQuery = olderResponse.RequestMessage?.RequestUri?.Query;
            Assert.NotNull(olderQuery);
            Assert.Equal(
                new[] { "page=2", "state=closed" },
                olderQuery.TrimStart('?').Split('&').Order(StringComparer.Ordinal));
            Assert.True(unscopedResponse.IsSuccessStatusCode);
            Assert.Equal(string.Empty, unscopedResponse.RequestMessage?.RequestUri?.Query);
            Assert.Equal("?state=open", newerUri.Query);
        }
        finally
        {
            release.TrySetResult();
            using var response = await newerTask.WaitAsync(timeout, testCt);
        }
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
        return new PagedGitHubSession(client);
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

    private static async Task<string> SendThroughAsync(PagedGitHubSession session)
    {
        var api = RestService.For<IGitHubReposApi>(session.Client);
        using var response = await session
            .PrepareRequest(() => api.ListIssuesPaged("owner", "repo"))
            .FirstAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode);
        var uri = response.RequestMessage?.RequestUri;
        Assert.NotNull(uri);
        return uri.OriginalString;
    }

    /// <summary>
    /// Inner handler that returns 200 OK and lets tests send through the session client.
    /// </summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, CancellationToken, Task>? BeforeResponse { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (BeforeResponse is not null)
                await BeforeResponse(request, cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent("[]", Encoding.UTF8, "application/json"),
            };
        }
    }
}
