using System.Net.Http.Headers;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Http;
using GitPulse.Services;
using Xunit;

namespace GitPulse.Tests;

public class GitHubClientFactoryTests
{
    [Fact]
    public async Task CreateClientAsync_WithToken_SetsBearerAuth()
    {
        var store = new FakeCredentialStore("ghp_test123");
        var factory = new GitHubClientFactory(store);

        var client = await factory.CreateClientAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(client.DefaultRequestHeaders.Authorization);
        Assert.Equal("Bearer", client.DefaultRequestHeaders.Authorization.Scheme);
        Assert.Equal("ghp_test123", client.DefaultRequestHeaders.Authorization.Parameter);
    }

    [Fact]
    public async Task CreateClientAsync_WithoutToken_NoAuthHeader()
    {
        var store = new FakeCredentialStore(null);
        var factory = new GitHubClientFactory(store);

        var client = await factory.CreateClientAsync(TestContext.Current.CancellationToken);

        Assert.Null(client.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public async Task CreateClientAsync_SetsBaseAddress()
    {
        var store = new FakeCredentialStore(null);
        var factory = new GitHubClientFactory(store);

        var client = await factory.CreateClientAsync(TestContext.Current.CancellationToken);

        Assert.Equal("https://api.github.com/", client.BaseAddress?.OriginalString);
    }

    [Fact]
    public async Task CreateClientAsync_SetsAcceptHeader()
    {
        var store = new FakeCredentialStore(null);
        var factory = new GitHubClientFactory(store);

        var client = await factory.CreateClientAsync(TestContext.Current.CancellationToken);

        Assert.Contains(client.DefaultRequestHeaders.Accept,
            h => h.MediaType == "application/vnd.github+json");
    }

    [Fact]
    public async Task CreateClientAsync_SetsApiVersionHeader()
    {
        var store = new FakeCredentialStore(null);
        var factory = new GitHubClientFactory(store);

        var client = await factory.CreateClientAsync(TestContext.Current.CancellationToken);

        Assert.True(client.DefaultRequestHeaders.Contains("X-GitHub-Api-Version"));
    }

    [Fact]
    public async Task CreateClientAsync_SetsUserAgent()
    {
        var store = new FakeCredentialStore(null);
        var factory = new GitHubClientFactory(store);

        var client = await factory.CreateClientAsync(TestContext.Current.CancellationToken);

        Assert.Equal("GitPulse", client.DefaultRequestHeaders.UserAgent.ToString());
    }

    [Fact]
    public async Task CreatePagedClientAsync_WithToken_ReturnsClientAndQueryHandler()
    {
        var store = new FakeCredentialStore("ghp_test123");
        var factory = new GitHubClientFactory(store);

        var (client, queryHandler) = await factory.CreatePagedClientAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(client);
        Assert.NotNull(client.DefaultRequestHeaders.Authorization);
        Assert.NotNull(queryHandler);
        Assert.Equal(1, queryHandler.Page);
        Assert.Equal(30, queryHandler.PerPage);
    }

    [Fact]
    public async Task CreatePagedClientAsync_WithoutToken_ReturnsClientWithNoAuth()
    {
        var store = new FakeCredentialStore(null);
        var factory = new GitHubClientFactory(store);

        var (client, queryHandler) = await factory.CreatePagedClientAsync(TestContext.Current.CancellationToken);

        Assert.Null(client.DefaultRequestHeaders.Authorization);
        Assert.NotNull(queryHandler);
    }

    [Fact]
    public async Task CreatePagedSessionAsync_WithToken_ReturnsAuthenticatedSession()
    {
        var store = new FakeCredentialStore("ghp_test123");
        var factory = new GitHubClientFactory(store);

        using var session = await factory.CreatePagedSessionAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(session.Client);
        Assert.Equal("https://api.github.com/", session.Client.BaseAddress?.OriginalString);
        Assert.NotNull(session.Client.DefaultRequestHeaders.Authorization);
        Assert.Equal("Bearer", session.Client.DefaultRequestHeaders.Authorization.Scheme);
        Assert.Equal("ghp_test123", session.Client.DefaultRequestHeaders.Authorization.Parameter);
        Assert.Contains(session.Client.DefaultRequestHeaders.Accept,
            h => h.MediaType == "application/vnd.github+json");
        Assert.True(session.Client.DefaultRequestHeaders.Contains("X-GitHub-Api-Version"));
        Assert.Equal("GitPulse", session.Client.DefaultRequestHeaders.UserAgent.ToString());
        Assert.False(session.HasNextPage);
    }

    [Fact]
    public async Task CreatePagedSessionAsync_WithoutToken_ReturnsSessionWithNoAuth()
    {
        var store = new FakeCredentialStore(null);
        var factory = new GitHubClientFactory(store);

        using var session = await factory.CreatePagedSessionAsync(TestContext.Current.CancellationToken);

        Assert.Null(session.Client.DefaultRequestHeaders.Authorization);
        Assert.False(session.HasNextPage);
    }

    [Fact]
    public async Task CreatePagedSessionAsync_PrepareRequestAndAdvance_UsesWiredQueryHandler()
    {
        var store = new FakeCredentialStore("ghp_test123");
        var factory = new GitHubClientFactory(store);

        using var session = await factory.CreatePagedSessionAsync(TestContext.Current.CancellationToken);
        var queryHandler = GetPrimaryQueryHandler(session.Client);

        session.PrepareRequest();
        Assert.Equal(1, queryHandler.Page);
        Assert.Equal(30, queryHandler.PerPage);

        session.ApplyLink(HeadersWithNextPage(2));
        Assert.True(session.HasNextPage);
        Assert.True(session.Advance());

        session.PrepareRequest();
        Assert.Equal(2, queryHandler.Page);
        Assert.Equal(30, queryHandler.PerPage);
    }

    /// <summary>
    /// Production factory installs <see cref="GitHubQueryHandler"/> as the client's
    /// primary handler; the session does not expose it. Reflection is the seam for
    /// asserting page/per_page defaults after <c>PrepareRequest</c>.
    /// </summary>
    private static GitHubQueryHandler GetPrimaryQueryHandler(HttpClient client)
    {
        var field = typeof(HttpMessageInvoker).GetField(
            "_handler",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<GitHubQueryHandler>(field.GetValue(client));
    }

    private static HttpResponseHeaders HeadersWithNextPage(int page)
    {
        var response = new HttpResponseMessage();
        response.Headers.Add(
            "Link",
            $"<https://api.github.com/user/repos?page={page}>; rel=\"next\"");
        return response.Headers;
    }

    private sealed class FakeCredentialStore : ICredentialStore
    {
        private readonly string? _token;

        public FakeCredentialStore(string? token) => _token = token;

        public Task<string?> GetTokenAsync(CancellationToken ct = default)
            => Task.FromResult(_token);

        public Task SetTokenAsync(string token, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task ClearTokenAsync(CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
