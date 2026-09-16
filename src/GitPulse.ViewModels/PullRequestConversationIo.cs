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

    public async Task<(GitHubClientScope? Scope, IGitHubReposApi? Api, CancellationTokenSource? Cts)> OpenAsync(
        bool requireToken = true)
    {
        var scope = await factory.OpenAsync();
        if (requireToken && scope.Client.DefaultRequestHeaders.Authorization is null)
        {
            scope.Dispose();
            Error.Value = "No token configured.";
            return (null, null, null);
        }

        return (scope, RestService.For<IGitHubReposApi>(scope.Client), new CancellationTokenSource(TimeSpan.FromSeconds(30)));
    }

    public void Timeout() => Error.Value = "Request timed out.";
}
