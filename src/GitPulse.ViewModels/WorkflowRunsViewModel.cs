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
/// Paged list of GitHub Actions workflow runs for a repository.
/// </summary>
public sealed partial class WorkflowRunsViewModel : IDisposable
{
    private readonly IGitHubClientFactory _clientFactory;

    private string _owner = string.Empty;
    private string _repo = string.Empty;
    private HttpClient? _pagedClient;
    private GitHubQueryHandler? _queryHandler;
    private int _currentPage;
    private bool _hasNextPage;

    public ObservableCollection<WorkflowRun> Runs { get; } = [];

    public BindableReactiveProperty<bool> IsLoading { get; } = new(false);
    public BindableReactiveProperty<bool> CanLoadMore { get; } = new(false);
    public BindableReactiveProperty<string> ErrorMessage { get; } = new(string.Empty);
    public BindableReactiveProperty<string> RepoFullName { get; } = new(string.Empty);
    public BindableReactiveProperty<string> Owner { get; } = new(string.Empty);
    public BindableReactiveProperty<string> RepoName { get; } = new(string.Empty);

    public WorkflowRunsViewModel(IGitHubClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
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
    private async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(_owner) || string.IsNullOrEmpty(_repo))
            return;

        IsLoading.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            _pagedClient?.Dispose();

            var (client, queryHandler) = await _clientFactory.CreatePagedClientAsync();
            if (client.DefaultRequestHeaders.Authorization is null)
            {
                ErrorMessage.Value = "No token configured. Open Settings to add a GitHub PAT.";
                client.Dispose();
                return;
            }

            _pagedClient = client;
            _queryHandler = queryHandler;
            _queryHandler.Page = 1;
            _queryHandler.PerPage = 30;

            var api = RestService.For<IGitHubActionsApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var response = await api.ListWorkflowRuns(_owner, _repo).FirstAsync(cts.Token);

            Runs.Clear();
            foreach (var run in response.Content?.WorkflowRuns ?? [])
                Runs.Add(run);

            _currentPage = 1;
            _hasNextPage = LinkHeaderParser.GetNextUrl(response.Headers) is not null;
            CanLoadMore.Value = _hasNextPage;
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
        if (!_hasNextPage || _queryHandler is null || _pagedClient is null || IsLoading.Value)
            return;

        IsLoading.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            _queryHandler.Page = _currentPage + 1;

            var api = RestService.For<IGitHubActionsApi>(_pagedClient);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var response = await api.ListWorkflowRuns(_owner, _repo).FirstAsync(cts.Token);

            foreach (var run in response.Content?.WorkflowRuns ?? [])
                Runs.Add(run);

            _currentPage++;
            _hasNextPage = LinkHeaderParser.GetNextUrl(response.Headers) is not null;
            CanLoadMore.Value = _hasNextPage;
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
        _pagedClient?.Dispose();
    }
}
