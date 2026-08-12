using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// Same-repo Create PR view model. Demonstrates
/// <see cref="IGitHubReposApi.CreatePullRequest"/> with path + [Body]
/// (Observables.RestAPI 0.1.5+) and branch choices via
/// <see cref="IGitHubReposApi.ListBranches"/>.
/// </summary>
public sealed partial class CreatePullRequestViewModel : IDisposable
{
    private readonly IGitHubClientFactory _clientFactory;

    private string _owner = string.Empty;
    private string _repo = string.Empty;

    /// <summary>PR title input (two-way bound to entry).</summary>
    public BindableReactiveProperty<string> TitleInput { get; } = new(string.Empty);

    /// <summary>PR body input (two-way bound to editor); optional.</summary>
    public BindableReactiveProperty<string> BodyInput { get; } = new(string.Empty);

    /// <summary>Head branch name (same-repo).</summary>
    public BindableReactiveProperty<string> HeadInput { get; } = new(string.Empty);

    /// <summary>Base branch name (same-repo).</summary>
    public BindableReactiveProperty<string> BaseInput { get; } = new(string.Empty);

    /// <summary>Create-time Draft PR flag (Glossary: Draft PR).</summary>
    public BindableReactiveProperty<bool> IsDraft { get; } = new(false);

    /// <summary>Branches loaded for head/base selection.</summary>
    public ObservableCollection<Branch> Branches { get; } = [];

    /// <summary>Whether a branch list load is in progress.</summary>
    public BindableReactiveProperty<bool> IsLoadingBranches { get; } = new(false);

    /// <summary>Whether a create operation is in progress.</summary>
    public BindableReactiveProperty<bool> IsSaving { get; } = new(false);

    /// <summary>Error message; empty when no error.</summary>
    public BindableReactiveProperty<string> ErrorMessage { get; } = new(string.Empty);

    /// <summary>The created PR number (set after successful creation).</summary>
    public BindableReactiveProperty<int?> CreatedPullRequestNumber { get; } = new(null);

    public CreatePullRequestViewModel(IGitHubClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public void Initialize(string owner, string repo)
    {
        _owner = owner;
        _repo = repo;
    }

    /// <summary>Load same-repo branches for head/base selection.</summary>
    [RelayCommand]
    private async Task LoadBranchesAsync()
    {
        if (string.IsNullOrEmpty(_owner) || string.IsNullOrEmpty(_repo) || IsLoadingBranches.Value)
            return;

        IsLoadingBranches.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            var client = await _clientFactory.CreateClientAsync();
            if (client.DefaultRequestHeaders.Authorization is null)
            {
                ErrorMessage.Value = "No token configured.";
                return;
            }

            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var branches = await api.ListBranches(_owner, _repo).FirstAsync(cts.Token);
            Branches.Clear();
            foreach (var b in branches)
                Branches.Add(b);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Branches request timed out.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Branches load failed: {ex.Message}";
        }
        finally
        {
            IsLoadingBranches.Value = false;
        }
    }

    /// <summary>
    /// Create a pull request. On success, sets <see cref="CreatedPullRequestNumber"/>.
    /// </summary>
    [RelayCommand]
    private async Task CreateAsync()
    {
        if (IsSaving.Value)
            return;

        var title = TitleInput.Value.Trim();
        var head = HeadInput.Value.Trim();
        var @base = BaseInput.Value.Trim();

        // Clear prior success so a blocked retry cannot re-fire navigation.
        CreatedPullRequestNumber.Value = null;

        if (string.IsNullOrWhiteSpace(title)
            || string.IsNullOrWhiteSpace(head)
            || string.IsNullOrWhiteSpace(@base)
            || string.Equals(head, @base, StringComparison.Ordinal))
        {
            return;
        }

        IsSaving.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            var client = await _clientFactory.CreateClientAsync();
            if (client.DefaultRequestHeaders.Authorization is null)
            {
                ErrorMessage.Value = "No token configured.";
                return;
            }

            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var request = new PullRequestCreateRequest
            {
                Title = title,
                Head = head,
                Base = @base,
                Body = string.IsNullOrEmpty(BodyInput.Value) ? null : BodyInput.Value,
                Draft = IsDraft.Value ? true : null,
            };

            var pr = await api.CreatePullRequest(_owner, _repo, request).FirstAsync(cts.Token);
            CreatedPullRequestNumber.Value = pr.Number;
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Request timed out.";
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

    public void Dispose()
    {
        TitleInput.Dispose();
        BodyInput.Dispose();
        HeadInput.Dispose();
        BaseInput.Dispose();
        IsDraft.Dispose();
        IsLoadingBranches.Dispose();
        IsSaving.Dispose();
        ErrorMessage.Dispose();
        CreatedPullRequestNumber.Dispose();
    }
}
