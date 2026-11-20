using GitPulse.Core.Http;
using Xunit;

namespace GitPulse.Tests;

public class GitHubQueryHandlerTests
{
    private static readonly HttpRequestOptionsKey<int> PageOption = new("GitPulse.Pagination.Page");
    private static readonly HttpRequestOptionsKey<int> PerPageOption = new("GitPulse.Pagination.PerPage");
    private static readonly HttpRequestOptionsKey<string> StateOption = new("GitPulse.Pagination.State");

    /// <summary>
    /// Sends a request through <see cref="GitHubQueryHandler"/> via an
    /// <see cref="HttpClient"/> and returns the final request URI that the
    /// inner handler received — this is what <see cref="GitHubQueryHandler"/>
    /// actually modified.
    /// </summary>
    private static async Task<string> SendAsync(
        string requestUri,
        int? page = null,
        int? perPage = null,
        string? state = null)
    {
        var handler = new GitHubQueryHandler(new CapturingHandler());
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        if (page is { } pageValue)
            request.Options.Set(PageOption, pageValue);
        if (perPage is { } perPageValue)
            request.Options.Set(PerPageOption, perPageValue);
        if (state is { } stateValue)
            request.Options.Set(StateOption, stateValue);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return request.RequestUri?.OriginalString ?? "";
    }

    [Fact]
    public async Task SendAsync_DefaultPage1_DoesNotInjectPageParam()
    {
        var uri = await SendAsync("https://api.github.com/user/repos", page: 1);

        Assert.DoesNotContain("page=", uri);
    }

    [Fact]
    public async Task SendAsync_Page2_InjectsPageParam()
    {
        var uri = await SendAsync("https://api.github.com/user/repos", page: 2);

        Assert.Contains("page=2", uri);
    }

    [Fact]
    public async Task SendAsync_PerPage50_InjectsPerPageParam()
    {
        var uri = await SendAsync("https://api.github.com/user/repos", perPage: 50);

        Assert.Contains("per_page=50", uri);
    }

    [Fact]
    public async Task SendAsync_PerPage30_DoesNotInjectPerPageParam()
    {
        var uri = await SendAsync("https://api.github.com/user/repos", perPage: 30);

        Assert.DoesNotContain("per_page", uri);
    }

    [Fact]
    public async Task SendAsync_StateOpen_InjectsStateParam()
    {
        var uri = await SendAsync("https://api.github.com/repos/o/r/issues", state: "open");

        Assert.Contains("state=open", uri);
    }

    [Fact]
    public async Task SendAsync_StateAll_InjectsStateParam()
    {
        var uri = await SendAsync("https://api.github.com/repos/o/r/issues", state: "all");

        Assert.Contains("state=all", uri);
    }

    [Fact]
    public async Task SendAsync_WithoutState_DoesNotInjectStateParam()
    {
        var uri = await SendAsync("https://api.github.com/repos/o/r/issues", page: 2);

        Assert.DoesNotContain("state=", uri);
    }

    [Fact]
    public async Task SendAsync_PreservesExistingQueryParams()
    {
        var uri = await SendAsync(
            "https://api.github.com/user/repos?sort=updated", page: 3, perPage: 50);

        Assert.Contains("sort=updated", uri);
        Assert.Contains("page=3", uri);
        Assert.Contains("per_page=50", uri);
    }

    [Fact]
    public async Task SendAsync_PageAndPerPageAndStateAllCombined()
    {
        var uri = await SendAsync(
            "https://api.github.com/repos/o/r/issues",
            page: 4,
            perPage: 100,
            state: "closed");

        Assert.Contains("page=4", uri);
        Assert.Contains("per_page=100", uri);
        Assert.Contains("state=closed", uri);
    }

    [Fact]
    public async Task SendAsync_Page1_OverwritesExistingPageParam()
    {
        var uri = await SendAsync("https://api.github.com/user/repos?page=9", page: 1);

        Assert.Contains("page=1", uri);
        Assert.DoesNotContain("page=9", uri);
    }

    [Fact]
    public async Task SendAsync_DefaultPerPage_OverwritesExistingPerPageParam()
    {
        var uri = await SendAsync("https://api.github.com/user/repos?per_page=100", perPage: 30);

        Assert.Contains("per_page=30", uri);
        Assert.DoesNotContain("per_page=100", uri);
    }

    [Fact]
    public async Task SendAsync_EncodedAmpersandInQuery_RoundTrips()
    {
        var uri = await SendAsync("https://api.github.com/search/code?q=a%26b", page: 2);

        Assert.Contains("q=a%26b", uri);
        Assert.Contains("page=2", uri);
        Assert.DoesNotContain("q=a&b", uri);
    }

    [Fact]
    public async Task SendAsync_FlagQueryPair_IsPreserved()
    {
        var uri = await SendAsync("https://api.github.com/user/repos?draft", page: 2);

        Assert.Contains("draft=", uri);
        Assert.Contains("page=2", uri);
    }

    [Fact]
    public async Task SendAsync_DuplicateQueryKeys_LastValueWins()
    {
        var uri = await SendAsync(
            "https://api.github.com/user/repos?sort=created&sort=updated", page: 2);

        Assert.Contains("sort=updated", uri);
        Assert.DoesNotContain("sort=created", uri);
    }

    [Fact]
    public async Task SendAsync_WithoutOptions_DoesNotRewriteExistingQuery()
    {
        const string requestUri =
            "https://api.github.com/user/repos?page=9&per_page=100&sort=created&sort=updated";

        var uri = await SendAsync(requestUri);

        Assert.Equal(requestUri, uri);
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
