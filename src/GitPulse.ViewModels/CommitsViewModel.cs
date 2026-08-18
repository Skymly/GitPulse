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
    private PagedGitHubSession? _session;

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
            _session?.Dispose();
            _session = null;
            Commits.Clear();

            var session = await _clientFactory.CreatePagedSessionAsync();
            if (session.Client.DefaultRequestHeaders.Authorization is null)
            {
                ErrorMessage.Value = "No token configured. Open Settings to add a GitHub PAT.";
                session.Dispose();
                return;
            }

            _session = session;
            _session.Reset();
            _session.PrepareRequest();

            var api = RestService.For<IGitHubReposApi>(_session.Client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var response = await api.ListCommitsPaged(_owner, _repo).FirstAsync(cts.Token);

            foreach (var commit in response.Content ?? [])
                Commits.Add(commit);

            _session.ApplyLink(response.Headers);
            CanLoadMore.Value = _session.HasNextPage;
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Request timed out.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Load failed: {ex.Message}";
        }
        finally
        {
            IsLoading.Value = false;
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (_session is null || !_session.HasNextPage || IsLoading.Value)
            return;

        if (!_session.Advance())
            return;

        IsLoading.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            _session.PrepareRequest();

            var api = RestService.For<IGitHubReposApi>(_session.Client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var response = await api.ListCommitsPaged(_owner, _repo).FirstAsync(cts.Token);

            foreach (var commit in response.Content ?? [])
                Commits.Add(commit);

            _session.ApplyLink(response.Headers);
            CanLoadMore.Value = _session.HasNextPage;
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Request timed out.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Load more failed: {ex.Message}";
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
        _session?.Dispose();
    }
}
