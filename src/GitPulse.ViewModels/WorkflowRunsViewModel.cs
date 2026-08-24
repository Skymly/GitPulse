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
    private readonly PagedListCycle _cycle;

    public ObservableCollection<WorkflowRun> Runs { get; } = [];
    public ObservableCollection<Workflow> Workflows { get; } = [];

    public BindableReactiveProperty<bool> IsLoading { get; } = new(false);
    public BindableReactiveProperty<bool> CanLoadMore { get; } = new(false);
    public BindableReactiveProperty<string> ErrorMessage { get; } = new(string.Empty);
    public BindableReactiveProperty<string> RepoFullName { get; } = new(string.Empty);
    public BindableReactiveProperty<string> Owner { get; } = new(string.Empty);
    public BindableReactiveProperty<string> RepoName { get; } = new(string.Empty);
    public BindableReactiveProperty<Workflow?> SelectedWorkflow { get; } = new(null);
    public BindableReactiveProperty<string> DispatchRef { get; } = new("main");
    public BindableReactiveProperty<bool> IsDispatching { get; } = new(false);

    public WorkflowRunsViewModel(IGitHubClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
        _cycle = new PagedListCycle(clientFactory);
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
            var result = await _cycle.LoadAsync(null, async (client, ct) =>
            {
                var api = RestService.For<IGitHubActionsApi>(client);
                var response = await api.ListWorkflowRuns(_owner, _repo).FirstAsync(ct);
                return new PagedListPage<WorkflowRun>(
                    response.Content?.WorkflowRuns ?? [], response.Headers);
            });
            if (!result.Completed)
                return;
            if (result.Error is not null)
            {
                ErrorMessage.Value = result.Error;
                return;
            }

            Runs.Clear();
            foreach (var run in result.Items)
                Runs.Add(run);
            CanLoadMore.Value = result.HasNextPage;

            if (_cycle.Client is { } client)
            {
                var api = RestService.For<IGitHubActionsApi>(client);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await LoadWorkflowsAsync(api, cts.Token);
            }
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
        if (!_cycle.CanLoadMore || IsLoading.Value)
            return;

        IsLoading.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            var result = await _cycle.LoadMoreAsync(async (client, ct) =>
            {
                var api = RestService.For<IGitHubActionsApi>(client);
                var response = await api.ListWorkflowRuns(_owner, _repo).FirstAsync(ct);
                return new PagedListPage<WorkflowRun>(
                    response.Content?.WorkflowRuns ?? [], response.Headers);
            });
            if (!result.Completed)
                return;
            if (result.Error is not null)
            {
                ErrorMessage.Value = result.Error;
                return;
            }

            foreach (var run in result.Items)
                Runs.Add(run);
            CanLoadMore.Value = result.HasNextPage;
        }
        finally
        {
            IsLoading.Value = false;
        }
    }

    [RelayCommand]
    private async Task DispatchAsync()
    {
        var workflow = SelectedWorkflow.Value;
        var gitRef = DispatchRef.Value.Trim();
        if (string.IsNullOrEmpty(_owner) || string.IsNullOrEmpty(_repo)
            || workflow is null || gitRef.Length == 0 || IsDispatching.Value)
        {
            return;
        }

        IsDispatching.Value = true;
        ErrorMessage.Value = string.Empty;

        try
        {
            var client = await _clientFactory.CreateClientAsync();
            if (client.DefaultRequestHeaders.Authorization is null)
            {
                ErrorMessage.Value = "No token configured. Open Settings to add a GitHub PAT.";
                return;
            }

            var api = RestService.For<IGitHubActionsApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var response = await api.DispatchWorkflow(
                    _owner, _repo, workflow.Id, new WorkflowDispatchRequest { Ref = gitRef })
                .FirstAsync(cts.Token);
            var code = (int)(response.StatusCode ?? 0);
            if (code is >= 200 and < 300)
                return;

            ErrorMessage.Value = code switch
            {
                403 => "Not allowed to dispatch this workflow.",
                422 => "GitHub rejected the dispatch. The workflow may not support workflow_dispatch.",
                _ => $"Dispatch failed: {code}.",
            };
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Request timed out.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Dispatch failed: {ex.Message}";
        }
        finally
        {
            IsDispatching.Value = false;
        }
    }

    private async Task LoadWorkflowsAsync(IGitHubActionsApi api, CancellationToken cancellationToken)
    {
        try
        {
            var result = await api.ListWorkflows(_owner, _repo).FirstAsync(cancellationToken);
            Workflows.Clear();
            foreach (var workflow in result.Workflows ?? [])
            {
                if (string.Equals(workflow.State, "active", StringComparison.OrdinalIgnoreCase))
                    Workflows.Add(workflow);
            }

            if (SelectedWorkflow.Value is null)
                SelectedWorkflow.Value = Workflows.FirstOrDefault();
        }
        catch
        {
            Workflows.Clear();
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
        SelectedWorkflow.Dispose();
        DispatchRef.Dispose();
        IsDispatching.Dispose();
        _cycle.Dispose();
    }
}
