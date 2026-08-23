using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// In-app Check Run from Get-a-check-run (M22). Non-paged client.
/// M27 loads the first page of annotations.
/// </summary>
public sealed partial class CheckRunDetailViewModel : IDisposable
{
    private readonly IGitHubClientFactory _clientFactory;
    private readonly IBrowserLauncher _browserLauncher;

    private string _owner = string.Empty;
    private string _repo = string.Empty;
    private long _checkRunId;

    public BindableReactiveProperty<CheckRun?> CheckRun { get; } = new(null);
    public BindableReactiveProperty<string> Name { get; } = new(string.Empty);
    public BindableReactiveProperty<string> Status { get; } = new(string.Empty);
    public BindableReactiveProperty<string> Conclusion { get; } = new(string.Empty);
    public BindableReactiveProperty<string> OutputTitle { get; } = new(string.Empty);
    public BindableReactiveProperty<string> OutputSummary { get; } = new(string.Empty);
    public BindableReactiveProperty<string> OutputText { get; } = new(string.Empty);
    public BindableReactiveProperty<string> HtmlUrl { get; } = new(string.Empty);
    public BindableReactiveProperty<string> ErrorMessage { get; } = new(string.Empty);
    public BindableReactiveProperty<bool> IsLoading { get; } = new(false);
    public BindableReactiveProperty<string> RepoFullName { get; } = new(string.Empty);

    public ObservableCollection<CheckRunAnnotation> Annotations { get; } = [];

    public CheckRunDetailViewModel(
        IGitHubClientFactory clientFactory,
        IBrowserLauncher browserLauncher)
    {
        _clientFactory = clientFactory;
        _browserLauncher = browserLauncher;
    }

    public void Initialize(string owner, string repo, long checkRunId)
    {
        _owner = owner;
        _repo = repo;
        _checkRunId = checkRunId;
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
        if (string.IsNullOrEmpty(_owner) || string.IsNullOrEmpty(_repo) || _checkRunId <= 0)
            return;

        IsLoading.Value = true;
        ErrorMessage.Value = string.Empty;
        Annotations.Clear();

        try
        {
            var client = await _clientFactory.CreateClientAsync();
            if (client.DefaultRequestHeaders.Authorization is null)
            {
                ErrorMessage.Value = "No token configured. Open Settings to add a GitHub PAT.";
                return;
            }

            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var run = await api.GetCheckRun(_owner, _repo, _checkRunId).FirstAsync(cts.Token);
            Apply(run);
            await LoadAnnotationsAsync(api, cts.Token);
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


    private async Task LoadAnnotationsAsync(IGitHubReposApi api, CancellationToken cancellationToken)
    {
        try
        {
            var items = await api.ListCheckRunAnnotations(_owner, _repo, _checkRunId)
                .FirstAsync(cancellationToken);
            Annotations.Clear();
            foreach (var item in items ?? [])
                Annotations.Add(item);
        }
        catch
        {
            Annotations.Clear();
        }
    }

    private void Apply(CheckRun run)
    {
        CheckRun.Value = run;
        Name.Value = run.Name;
        Status.Value = run.Status;
        Conclusion.Value = run.Conclusion ?? string.Empty;
        HtmlUrl.Value = run.HtmlUrl;
        OutputTitle.Value = run.Output?.Title ?? string.Empty;
        OutputSummary.Value = run.Output?.Summary ?? string.Empty;
        OutputText.Value = run.Output?.Text ?? string.Empty;
    }

    public void Dispose()
    {
        CheckRun.Dispose();
        Name.Dispose();
        Status.Dispose();
        Conclusion.Dispose();
        OutputTitle.Dispose();
        OutputSummary.Dispose();
        OutputText.Dispose();
        HtmlUrl.Dispose();
        ErrorMessage.Dispose();
        IsLoading.Dispose();
        RepoFullName.Dispose();
    }
}
