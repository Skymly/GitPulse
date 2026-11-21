using System.Net.Http.Headers;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Http;

namespace GitPulse.Tests.TestHelpers;

/// <summary>
/// <see cref="IGitHubClientFactory"/> stub for ViewModel tests. Builds an
/// <see cref="HttpClient"/> backed by a test handler,
/// optionally pre-loaded with a Bearer token so the ViewModel's auth guard
/// passes. Mirrors the header setup of the real
/// <c>GitHubClientFactory</c> (base address, Accept, API version, User-Agent).
/// </summary>
public sealed class FakeGitHubClientFactory : IGitHubClientFactory
{
    private readonly HttpMessageHandler _handler;
    private readonly string? _token;

    private int _liveClients;
    public int LiveClients => Volatile.Read(ref _liveClients);

    public FakeGitHubClientFactory(HttpMessageHandler handler, string? token = "ghp_fake_test_token")
    {
        _handler = handler;
        _token = token;
    }

    public Task<HttpClient> CreateClientAsync(CancellationToken ct = default)
    {
        return Task.FromResult(BuildClient(_handler));
    }

    public Task<PagedGitHubSession> CreatePagedSessionAsync(CancellationToken ct = default)
    {
        var queryHandler = new GitHubQueryHandler(_handler);
        var client = BuildClient(queryHandler);
        return Task.FromResult(new PagedGitHubSession(client));
    }

    private HttpClient BuildClient(HttpMessageHandler handler)
    {
        Interlocked.Increment(ref _liveClients);
        var client = new CountedHttpClient(handler, () => Interlocked.Decrement(ref _liveClients))
        {
            BaseAddress = new Uri("https://api.github.com/"),
        };

        if (!string.IsNullOrEmpty(_token))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _token);
        }

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GitPulse");

        return client;
    }

    private sealed class CountedHttpClient(HttpMessageHandler handler, Action onDispose)
        : HttpClient(handler, disposeHandler: false)
    {
        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                onDispose();
            }

            base.Dispose(disposing);
        }
    }
}
