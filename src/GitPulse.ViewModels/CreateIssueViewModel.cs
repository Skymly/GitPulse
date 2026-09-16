using CommunityToolkit.Mvvm.Input;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// New issue creation view model. Demonstrates
/// <see cref="IGitHubReposApi.CreateIssue"/> with path + [Body] parameters
/// (requires Observables.RestAPI 0.1.5+).
/// </summary>
public sealed partial class CreateIssueViewModel : IDisposable
{
    private readonly IGitHubClientFactory _clientFactory;

    private string _owner = string.Empty;
    private string _repo = string.Empty;

    /// <summary>Issue title input (two-way bound to entry).</summary>
    public BindableReactiveProperty<string> TitleInput { get; } = new(string.Empty);

    /// <summary>Issue body input (two-way bound to editor).</summary>
    public BindableReactiveProperty<string> BodyInput { get; } = new(string.Empty);

    /// <summary>Comma-separated label names (optional).</summary>
    public BindableReactiveProperty<string> LabelsInput { get; } = new(string.Empty);

    /// <summary>Whether a create operation is in progress.</summary>
    public BindableReactiveProperty<bool> IsSaving { get; } = new(false);

    /// <summary>Error message; empty when no error.</summary>
    public BindableReactiveProperty<string> ErrorMessage { get; } = new(string.Empty);

    /// <summary>The created issue number (set after successful creation).</summary>
    public BindableReactiveProperty<int?> CreatedIssueNumber { get; } = new(null);

    public CreateIssueViewModel(IGitHubClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public void Initialize(string owner, string repo)
    {
        _owner = owner;
        _repo = repo;
    }

    /// <summary>Create a new issue. On success, sets <see cref="CreatedIssueNumber"/>.</summary>
    [RelayCommand]
    private async Task CreateAsync()
    {
        if (string.IsNullOrWhiteSpace(TitleInput.Value) || IsSaving.Value)
            return;

        IsSaving.Value = true;
        ErrorMessage.Value = string.Empty;
        CreatedIssueNumber.Value = null;

        try
        {
            using var scope = await _clientFactory.OpenAsync();
            var client = scope.Client;
            if (client.DefaultRequestHeaders.Authorization is null)
            {
                ErrorMessage.Value = "No token configured.";
                return;
            }

            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var labels = LabelsInput.Value
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToArray();

            var request = new IssueCreateRequest
            {
                Title = TitleInput.Value,
                Body = string.IsNullOrEmpty(BodyInput.Value) ? null : BodyInput.Value,
                Labels = labels.Length > 0 ? labels : null,
            };

            var issue = await api.CreateIssue(_owner, _repo, request).FirstAsync(cts.Token);
            CreatedIssueNumber.Value = issue.Number;
        }
        catch (Exception ex) when (WriteTimeout.IsCanceled(ex))
        {
            await ConfirmCreatedIssueAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Create failed: {ex.Message}";
        }
        finally
        {
            IsSaving.Value = false;
        }
    }

    private async Task ConfirmCreatedIssueAsync()
    {
        try
        {
            using var scope = await _clientFactory.OpenAsync();
            var client = scope.Client;
            if (client.DefaultRequestHeaders.Authorization is null)
            {
                ErrorMessage.Value = WriteTimeout.MaybeSubmitted;
                return;
            }

            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var response = await api.ListIssuesPaged(_owner, _repo).FirstAsync(cts.Token);
            var title = TitleInput.Value;
            var match = (response.Content ?? []).FirstOrDefault(issue =>
                !issue.IsPullRequest
                && string.Equals(issue.Title, title, StringComparison.Ordinal));
            if (match is not null)
            {
                CreatedIssueNumber.Value = match.Number;
                return;
            }
        }
        catch (Exception)
        {
            // Still unknown whether GitHub created the issue.
        }

        ErrorMessage.Value = WriteTimeout.MaybeSubmitted;
    }

    public void Dispose()
    {
        TitleInput.Dispose();
        BodyInput.Dispose();
        LabelsInput.Dispose();
        IsSaving.Dispose();
        ErrorMessage.Dispose();
        CreatedIssueNumber.Dispose();
    }
}
