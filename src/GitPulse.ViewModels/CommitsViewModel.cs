using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Http;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// Paged commit history for a repository (M18).
/// </summary>
public sealed partial class CommitsViewModel : IDisposable
{
    private readonly IGitHubClientFactory _clientFactory;
    private readonly IBrowserLauncher _browserLauncher;

    private string _owner = string.Empty;
    private string _repo = string.Empty;
    private readonly PagedListCycle _cycle;

    public ObservableCollection<GitCommit> Commits { get; } = [];

    public BindableReactiveProperty<bool> IsLoading { get; } = new(false);
    public BindableReactiveProperty<bool> CanLoadMore { get; } = new(false);
    public BindableReactiveProperty<string> ErrorMessage { get; } = new(string.Empty);
    public BindableReactiveProperty<string> RepoFullName { get; } = new(string.Empty);
    public BindableReactiveProperty<string> Owner { get; } = new(string.Empty);
    public BindableReactiveProperty<string> RepoName { get; } = new(string.Empty);

    public CommitsViewModel(IGitHubClientFactory clientFactory, IBrowserLauncher browserLauncher)
    {
        _clientFactory = clientFactory;
        _cycle = new PagedListCycle(clientFactory);
        _browserLauncher = browserLauncher;
    }

    public void Initialize(string owner, string repo)
    {
        _owner = owner;
        _repo = repo;
        Owner.Value = owner;
        RepoName.Value = repo;
        RepoFullName.Value = $"{owner}/{repo}";
    }

    [RelayCommand]
    private async Task OpenInBrowserAsync(string? url)
    {
        if (!string.IsNullOrEmpty(url))
            await _browserLauncher.OpenAsync(url);
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(_owner) || string.IsNullOrEmpty(_repo))
            return;

        IsLoading.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            Commits.Clear();
            var result = await _cycle.LoadAsync(null, async (client, ct) =>
            {
                var api = RestService.For<IGitHubReposApi>(client);
                var response = await api.ListCommitsPaged(_owner, _repo).FirstAsync(ct);
                return new PagedListPage<GitCommit>(response.Content ?? [], response.Headers);
            });
            if (!result.Completed)
                return;
            if (result.Error is not null)
            {
                ErrorMessage.Value = result.Error;
                return;
            }

            foreach (var commit in result.Items)
                Commits.Add(commit);
            CanLoadMore.Value = result.HasNextPage;
        }
        finally
        {
            IsLoading.Value = false;
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!_cycle.CanLoadMore || IsLoading.Value)
            return;

        IsLoading.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            var result = await _cycle.LoadMoreAsync(async (client, ct) =>
            {
                var api = RestService.For<IGitHubReposApi>(client);
                var response = await api.ListCommitsPaged(_owner, _repo).FirstAsync(ct);
                return new PagedListPage<GitCommit>(response.Content ?? [], response.Headers);
            });
            if (!result.Completed)
                return;
            if (result.Error is not null)
            {
                ErrorMessage.Value = result.Error;
                return;
            }

            foreach (var commit in result.Items)
                Commits.Add(commit);
            CanLoadMore.Value = result.HasNextPage;
        }
        finally
        {
            IsLoading.Value = false;
        }
    }

    public void Dispose()
    {
        IsLoading.Dispose();
        CanLoadMore.Dispose();
        ErrorMessage.Dispose();
        RepoFullName.Dispose();
        Owner.Dispose();
        RepoName.Dispose();
        _cycle.Dispose();
    }
}
