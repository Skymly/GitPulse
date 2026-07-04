using System.Net.Http.Headers;
using GitPulse.Core.Abstractions;
using GitPulse.Services;
using Xunit;
using Xunit.Sdk;

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

