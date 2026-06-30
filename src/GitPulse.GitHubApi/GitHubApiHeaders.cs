using GitPulse.Core.Abstractions;

namespace GitPulse.GitHubApi;

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
        var client = new HttpClient
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
