using System.Net.Http.Headers;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Http;

namespace GitPulse.Tests.TestHelpers;

/// <summary>
/// <see cref="IGitHubClientFactory"/> stub for ViewModel tests. Builds an
/// <see cref="HttpClient"/> backed by a <see cref="MockHttpHandler"/>,
/// optionally pre-loaded with a Bearer token so the ViewModel's auth guard
/// passes. Mirrors the header setup of the real
/// <c>GitHubClientFactory</c> (base address, Accept, API version, User-Agent).
/// </summary>
public sealed class FakeGitHubClientFactory : IGitHubClientFactory
{
    private readonly MockHttpHandler _handler;
    private readonly string? _token;

    public int LiveClients { get; private set; }

    public FakeGitHubClientFactory(MockHttpHandler handler, string? token = "ghp_fake_test_token")
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
        return Task.FromResult(new PagedGitHubSession(client, queryHandler));
    }

    private HttpClient BuildClient(HttpMessageHandler handler)
    {
        LiveClients++;
        var client = new CountedHttpClient(handler, () => LiveClients--)
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
