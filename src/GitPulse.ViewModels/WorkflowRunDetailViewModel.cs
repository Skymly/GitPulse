using System.Collections.ObjectModel;
using System.Net;
using CommunityToolkit.Mvvm.Input;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// Detail for a single workflow run: metadata, jobs, optional rerun.
/// </summary>
public sealed partial class WorkflowRunDetailViewModel : IDisposable
{
    private readonly IGitHubClientFactory _clientFactory;
    private readonly IBrowserLauncher _browserLauncher;

    private string _owner = string.Empty;
    private string _repo = string.Empty;
    private long _runId;

    public BindableReactiveProperty<WorkflowRun?> Run { get; } = new(null);
    public ObservableCollection<WorkflowJob> Jobs { get; } = [];

    public BindableReactiveProperty<bool> IsLoading { get; } = new(false);
    public BindableReactiveProperty<bool> IsRerunning { get; } = new(false);
    public BindableReactiveProperty<string> ErrorMessage { get; } = new(string.Empty);
    public BindableReactiveProperty<string> Title { get; } = new(string.Empty);
    public BindableReactiveProperty<string> StatusSummary { get; } = new(string.Empty);

    public WorkflowRunDetailViewModel(
        IGitHubClientFactory clientFactory,
        IBrowserLauncher browserLauncher)
    {
        _clientFactory = clientFactory;
        _browserLauncher = browserLauncher;
    }

    public void Initialize(string owner, string repo, long runId)
    {
        _owner = owner;
        _repo = repo;
        _runId = runId;
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
        if (string.IsNullOrEmpty(_owner) || string.IsNullOrEmpty(_repo) || _runId <= 0)
            return;

        IsLoading.Value = true;
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

            var run = await api.GetWorkflowRun(_owner, _repo, _runId).FirstAsync(cts.Token);
            Run.Value = run;
            Title.Value = string.IsNullOrWhiteSpace(run.DisplayTitle)
                ? $"#{run.RunNumber} {run.Name}"
                : $"#{run.RunNumber} {run.DisplayTitle}";
            StatusSummary.Value = FormatStatus(run.Status, run.Conclusion);

            var jobsResponse = await api.ListWorkflowJobs(_owner, _repo, _runId).FirstAsync(cts.Token);
            Jobs.Clear();
            foreach (var job in jobsResponse.Content?.Jobs ?? [])
                Jobs.Add(job);
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
    private async Task RerunAsync()
    {
        if (string.IsNullOrEmpty(_owner) || string.IsNullOrEmpty(_repo) || _runId <= 0 || IsRerunning.Value)
            return;

        IsRerunning.Value = true;
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
            await api.RerunWorkflow(_owner, _repo, _runId).FirstAsync(cts.Token);

            // Refresh so status/jobs reflect the new attempt.
            await LoadAsync();
        }
        catch (Exception ex) when (IsForbidden(ex))
        {
            ErrorMessage.Value =
                "Rerun forbidden. Ensure the PAT has Actions write permission.";
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Request timed out.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Rerun failed: {ex.Message}";
        }
        finally
        {
            IsRerunning.Value = false;
        }
    }

    private static bool IsForbidden(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException { StatusCode: HttpStatusCode.Forbidden })
                return true;

            if (current.Message.Contains("403", StringComparison.Ordinal)
                || current.Message.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatStatus(string status, string? conclusion)
    {
        if (!string.IsNullOrWhiteSpace(conclusion))
            return $"{status} · {conclusion}";
        return status;
    }

    public void Dispose()
    {
        Run.Dispose();
        IsLoading.Dispose();
        IsRerunning.Dispose();
        ErrorMessage.Dispose();
        Title.Dispose();
        StatusSummary.Dispose();
    }
}
