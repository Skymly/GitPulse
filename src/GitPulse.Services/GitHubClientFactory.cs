using GitPulse.Core.Abstractions;
using GitPulse.Core.Http;

namespace GitPulse.Services;

/// <summary>
/// Builds an <see cref="HttpClient"/> with GitHub-required headers.
/// Base address: https://api.github.com
/// Headers: Authorization Bearer &lt;PAT&gt;, Accept application/vnd.github+json,
/// X-GitHub-Api-Version 2022-11-28, User-Agent GitPulse.
/// </summary>
public sealed class GitHubClientFactory : IGitHubClientFactory
{
    private const string ApiBaseAddress = "https://api.github.com/";
    private const string GitHubApiVersion = "2022-11-28";
    private const string UserAgent = "GitPulse";

    private readonly ICredentialStore _credentialStore;

    public GitHubClientFactory(ICredentialStore credentialStore)
    {
        _credentialStore = credentialStore;
    }

    public async Task<HttpClient> CreateClientAsync(CancellationToken ct = default)
    {
        var token = await _credentialStore.GetTokenAsync(ct);
        return BuildClient(token, new HttpClientHandler());
    }

    public async Task<(HttpClient Client, GitHubQueryHandler QueryHandler)> CreatePagedClientAsync(
        CancellationToken ct = default)
    {
        var token = await _credentialStore.GetTokenAsync(ct);
        var queryHandler = new GitHubQueryHandler(new HttpClientHandler());
        var client = BuildClient(token, queryHandler);
        return (client, queryHandler);
    }

    private HttpClient BuildClient(string? token, HttpMessageHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(ApiBaseAddress),
        };
        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
        client.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", GitHubApiVersion);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }
}
