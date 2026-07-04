using GitPulse.Core.Http;
using Xunit;

namespace GitPulse.Tests;

public class GitHubQueryHandlerTests
{
    /// <summary>
    /// Sends a request through <see cref="GitHubQueryHandler"/> via an
    /// <see cref="HttpClient"/> and returns the final request URI that the
    /// inner handler received — this is what <see cref="GitHubQueryHandler"/>
    /// actually modified.
    /// </summary>
    private static async Task<string> SendAsync(
        GitHubQueryHandler handler, string requestUri)
    {
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        // Don't actually send — we just need the handler to modify the URI.
        // Use SendAsync via HttpClient which calls the protected SendAsync.
        _ = await client.SendAsync(request, CancellationToken.None);
        return request.RequestUri?.OriginalString ?? "";
    }

    [Fact]
    public async Task SendAsync_DefaultPage1_DoesNotInjectPageParam()
    {
        var handler = new GitHubQueryHandler(new CapturingHandler());

        var uri = await SendAsync(handler, "https://api.github.com/user/repos");

        Assert.DoesNotContain("page=", uri);
    }

    [Fact]
    public async Task SendAsync_Page2_InjectsPageParam()
    {
        var handler = new GitHubQueryHandler(new CapturingHandler()) { Page = 2 };

        var uri = await SendAsync(handler, "https://api.github.com/user/repos");

        Assert.Contains("page=2", uri);
    }

    [Fact]
    public async Task SendAsync_PerPage50_InjectsPerPageParam()
    {
        var handler = new GitHubQueryHandler(new CapturingHandler()) { PerPage = 50 };

        var uri = await SendAsync(handler, "https://api.github.com/user/repos");

        Assert.Contains("per_page=50", uri);
    }

    [Fact]
    public async Task SendAsync_PerPage30_DoesNotInjectPerPageParam()
    {
        var handler = new GitHubQueryHandler(new CapturingHandler()) { PerPage = 30 };

        var uri = await SendAsync(handler, "https://api.github.com/user/repos");

        Assert.DoesNotContain("per_page", uri);
    }

    [Fact]
    public async Task SendAsync_StateOpen_InjectsStateParam()
    {
        var handler = new GitHubQueryHandler(new CapturingHandler()) { State = "open" };

        var uri = await SendAsync(handler, "https://api.github.com/repos/o/r/issues");

        Assert.Contains("state=open", uri);
    }

    [Fact]
    public async Task SendAsync_StateAll_DoesNotInjectStateParam()
    {
        var handler = new GitHubQueryHandler(new CapturingHandler()) { State = "all" };

        var uri = await SendAsync(handler, "https://api.github.com/repos/o/r/issues");

        Assert.DoesNotContain("state=", uri);
    }

    [Fact]
    public async Task SendAsync_StateNull_DoesNotInjectStateParam()
    {
        var handler = new GitHubQueryHandler(new CapturingHandler()) { State = null };

        var uri = await SendAsync(handler, "https://api.github.com/repos/o/r/issues");

        Assert.DoesNotContain("state=", uri);
    }

    [Fact]
    public async Task SendAsync_PreservesExistingQueryParams()
    {
        var handler = new GitHubQueryHandler(new CapturingHandler()) { Page = 3, PerPage = 50 };

        var uri = await SendAsync(handler, "https://api.github.com/user/repos?sort=updated");

        Assert.Contains("sort=updated", uri);
        Assert.Contains("page=3", uri);
        Assert.Contains("per_page=50", uri);
    }

    [Fact]
    public async Task SendAsync_PageAndPerPageAndStateAllCombined()
    {
        var handler = new GitHubQueryHandler(new CapturingHandler())
        {
            Page = 4,
            PerPage = 100,
            State = "closed",
        };

        var uri = await SendAsync(handler, "https://api.github.com/repos/o/r/issues");

        Assert.Contains("page=4", uri);
        Assert.Contains("per_page=100", uri);
        Assert.Contains("state=closed", uri);
    }

    /// <summary>
    /// Inner handler that returns a 200 OK without doing any I/O.
    /// </summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage());
    }
}
