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

    public async Task<(IGitHubReposApi? Api, CancellationTokenSource? Cts)> OpenAsync(
        bool requireToken = true)
    {
        var client = await factory.CreateClientAsync();
        if (requireToken && client.DefaultRequestHeaders.Authorization is null)
        {
            Error.Value = "No token configured.";
            return (null, null);
        }

        return (RestService.For<IGitHubReposApi>(client), new CancellationTokenSource(TimeSpan.FromSeconds(30)));
    }

    public void Timeout() => Error.Value = "Request timed out.";
}
