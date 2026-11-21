using System.Net;
using System.Net.Sockets;
using GitPulse.Core.Abstractions;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;
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
    public async Task CreatePagedSessionAsync_SendsScopedStateThroughTheProductionHandler()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var ct = timeout.Token;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        using var factory = new GitHubClientFactory(new FakeCredentialStore(null));
        using var session = await factory.CreatePagedSessionAsync(ct);
        session.Client.BaseAddress = new Uri($"http://{listener.LocalEndpoint}/");
        session.State = "closed";
        var api = RestService.For<IGitHubReposApi>(session.Client);
        var pending = session.PrepareRequest(() => api.ListIssuesPaged("owner", "repo")).FirstAsync(ct);

        using var connection = await listener.AcceptTcpClientAsync(ct);
        await using var stream = connection.GetStream();
        using var reader = new StreamReader(stream);
        var requestLine = await reader.ReadLineAsync(ct);
        while (await reader.ReadLineAsync(ct) is { Length: > 0 })
        {
            // Consume the request headers before sending the response.
        }

        await stream.WriteAsync("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 2\r\n\r\n[]"u8.ToArray(), ct);
        using var response = await pending;

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("GET /repos/owner/repo/issues?state=closed HTTP/1.1", requestLine);
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
