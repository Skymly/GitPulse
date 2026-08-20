using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// In-app Git Commit from Get-a-commit (M19). Loaded on a non-paged client.
/// </summary>
public sealed partial class CommitDetailViewModel : IDisposable
{
    private readonly IGitHubClientFactory _clientFactory;
    private readonly IBrowserLauncher _browserLauncher;

    private string _owner = string.Empty;
    private string _repo = string.Empty;
    private string _sha = string.Empty;

    public BindableReactiveProperty<GitCommit?> Commit { get; } = new(null);
    public BindableReactiveProperty<string> Message { get; } = new(string.Empty);
    public BindableReactiveProperty<string> Sha { get; } = new(string.Empty);
    public BindableReactiveProperty<string> AuthorName { get; } = new(string.Empty);
    public BindableReactiveProperty<DateTime?> AuthorDate { get; } = new(null);
    public BindableReactiveProperty<int> Additions { get; } = new(0);
    public BindableReactiveProperty<int> Deletions { get; } = new(0);
    public BindableReactiveProperty<int> Total { get; } = new(0);
    public BindableReactiveProperty<string> HtmlUrl { get; } = new(string.Empty);
    public BindableReactiveProperty<string> ErrorMessage { get; } = new(string.Empty);
    public BindableReactiveProperty<bool> IsLoading { get; } = new(false);
    public BindableReactiveProperty<string> RepoFullName { get; } = new(string.Empty);

    public ObservableCollection<DiffEntry> Files { get; } = [];

    public CommitDetailViewModel(
        IGitHubClientFactory clientFactory,
        IBrowserLauncher browserLauncher)
    {
        _clientFactory = clientFactory;
        _browserLauncher = browserLauncher;
    }

    public void Initialize(string owner, string repo, string sha)
    {
        _owner = owner;
        _repo = repo;
        _sha = sha;
        Sha.Value = sha;
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
        if (string.IsNullOrEmpty(_owner) || string.IsNullOrEmpty(_repo) || string.IsNullOrEmpty(_sha))
            return;

        IsLoading.Value = true;
        ErrorMessage.Value = string.Empty;
        Files.Clear();

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
            var commit = await api.GetCommit(_owner, _repo, _sha).FirstAsync(cts.Token);

            ApplyCommit(commit);
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

    private void ApplyCommit(GitCommit commit)
    {
        Commit.Value = commit;
        Message.Value = commit.Commit?.Message ?? string.Empty;
        Sha.Value = commit.Sha;
        AuthorName.Value = commit.Commit?.Author?.Name ?? string.Empty;
        AuthorDate.Value = commit.Commit?.Author?.Date;
        Additions.Value = commit.Stats?.Additions ?? 0;
        Deletions.Value = commit.Stats?.Deletions ?? 0;
        Total.Value = commit.Stats?.Total ?? 0;
        HtmlUrl.Value = commit.HtmlUrl;

        foreach (var file in commit.Files ?? [])
            Files.Add(file);
    }

    public void Dispose()
    {
        Commit.Dispose();
        Message.Dispose();
        Sha.Dispose();
        AuthorName.Dispose();
        AuthorDate.Dispose();
        Additions.Dispose();
        Deletions.Dispose();
        Total.Dispose();
        HtmlUrl.Dispose();
        ErrorMessage.Dispose();
        IsLoading.Dispose();
        RepoFullName.Dispose();
    }
}
