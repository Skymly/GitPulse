using GitPulse.Core.Abstractions;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// Shared GitHub IO for Conversation composites: locator, error banner, 30s timeout.
/// </summary>
internal sealed class PullRequestConversationIo(
    IGitHubClientFactory factory,
    BindableReactiveProperty<string> errorMessage)
{
    public BindableReactiveProperty<string> Error { get; } = errorMessage;

    public string Owner { get; set; } = string.Empty;

    public string Repo { get; set; } = string.Empty;

    public int Number { get; set; }

    public async Task<GitHubApiCall?> OpenAsync(bool requireToken = true)
    {
        var client = await factory.CreateClientAsync();
        try
        {
            if (requireToken && client.DefaultRequestHeaders.Authorization is null)
            {
                Error.Value = "No token configured.";
                client.Dispose();
                return null;
            }

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            return new GitHubApiCall(client, cts, RestService.For<IGitHubReposApi>(client));
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public void Timeout() => Error.Value = "Request timed out.";
}

/// <summary>Owns one authenticated GitHub call cycle (client + timeout).</summary>
internal sealed class GitHubApiCall : IDisposable
{
    private readonly HttpClient _client;
    private readonly CancellationTokenSource _cts;

    public GitHubApiCall(HttpClient client, CancellationTokenSource cts, IGitHubReposApi api)
    {
        _client = client;
        _cts = cts;
        Api = api;
    }

    public IGitHubReposApi Api { get; }

    public CancellationToken Token => _cts.Token;

    public void Dispose()
    {
        _cts.Dispose();
        _client.Dispose();
    }
}
