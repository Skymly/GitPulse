using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// File browser view model — the M5 RestAPI domain showcase for
/// repository content browsing. Navigates the directory tree via
/// <see cref="IGitHubReposApi.ListContents"/>, displaying files and
/// subdirectories. Selecting a file navigates to the editor page.
/// </summary>
/// <remarks>
/// <para>
/// The browser maintains a navigation stack of directory paths. Each
/// <see cref="LoadCommand"/> fetches the current directory's contents
/// from the GitHub Contents API. Directories are listed first (sorted
/// alphabetically), then files.
/// </para>
/// <para>
/// <b>Path handling:</b> The GitHub Contents API uses path as a URL
/// segment (<c>/repos/{owner}/{repo}/contents/{path}</c>). The root
/// directory uses an empty path. Subdirectory paths may contain slashes
/// (e.g. <c>src/GitPulse.Core/Models</c>).
/// </para>
/// </remarks>
public sealed partial class FileBrowserViewModel : IDisposable
{
    private readonly IGitHubClientFactory _clientFactory;
    private readonly IBrowserLauncher _browserLauncher;

    private string _owner = string.Empty;
    private string _repo = string.Empty;
    private string _currentPath = string.Empty;

    /// <summary>Directory entries currently displayed (dirs first, then files).</summary>
    public ObservableCollection<ContentEntry> Entries { get; } = [];

    /// <summary>Whether a load operation is in progress.</summary>
    public BindableReactiveProperty<bool> IsLoading { get; } = new(false);

    /// <summary>Error message; empty when no error.</summary>
    public BindableReactiveProperty<string> ErrorMessage { get; } = new(string.Empty);

    /// <summary>Repository full name for display (owner/repo).</summary>
    public BindableReactiveProperty<string> RepoFullName { get; } = new(string.Empty);

    /// <summary>Current directory path for breadcrumb display.</summary>
    public BindableReactiveProperty<string> CurrentPath { get; } = new(string.Empty);

    /// <summary>Whether the current directory is not the root (can go up).</summary>
    public BindableReactiveProperty<bool> CanGoUp { get; } = new(false);

    /// <summary>Owner part (for navigation to file editor).</summary>
    public BindableReactiveProperty<string> Owner { get; } = new(string.Empty);

    /// <summary>Repo name part (for navigation to file editor).</summary>
    public BindableReactiveProperty<string> RepoName { get; } = new(string.Empty);

    public FileBrowserViewModel(IGitHubClientFactory clientFactory, IBrowserLauncher browserLauncher)
    {
        _clientFactory = clientFactory;
        _browserLauncher = browserLauncher;
    }

    /// <summary>
    /// Initialize with repository coordinates and optional starting path.
    /// Called by the page when navigated to via Shell query parameters.
    /// </summary>
    public void Initialize(string owner, string repo, string path = "")
    {
        _owner = owner;
        _repo = repo;
        _currentPath = path;
        Owner.Value = owner;
        RepoName.Value = repo;
        RepoFullName.Value = $"{owner}/{repo}";
        CurrentPath.Value = path;
        CanGoUp.Value = !string.IsNullOrEmpty(path);
    }

    /// <summary>Load the current directory's contents.</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(_owner) || string.IsNullOrEmpty(_repo))
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

            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var entries = await api.ListContents(_owner, _repo, _currentPath).FirstAsync(cts.Token);

            Entries.Clear();
            // Sort: directories first, then files, each alphabetical.
            foreach (var entry in entries.OrderBy(e => e.Type == "file").ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
                Entries.Add(entry);
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

    /// <summary>
    /// Navigate into a subdirectory. Called when a directory entry is selected.
    /// Returns the new path so the page can decide whether to reload or navigate.
    /// </summary>
    [RelayCommand]
    private Task NavigateToAsync(ContentEntry entry)
    {
        if (entry.Type == "dir")
        {
            _currentPath = entry.Path;
            CurrentPath.Value = entry.Path;
            CanGoUp.Value = true;
            return LoadCommand.ExecuteAsync(null);
        }

        // File selection is handled by the page (navigation to editor).
        return Task.CompletedTask;
    }

    /// <summary>Go up one directory level. No-op at root.</summary>
    [RelayCommand]
    private Task GoUpAsync()
    {
        if (string.IsNullOrEmpty(_currentPath))
            return Task.CompletedTask;

        var idx = _currentPath.LastIndexOf('/');
        _currentPath = idx >= 0 ? _currentPath[..idx] : string.Empty;
        CurrentPath.Value = _currentPath;
        CanGoUp.Value = !string.IsNullOrEmpty(_currentPath);
        return LoadCommand.ExecuteAsync(null);
    }

    /// <summary>Open the current directory on GitHub.com in the browser.</summary>
    [RelayCommand]
    private async Task OpenInBrowserAsync()
    {
        var url = $"https://github.com/{_owner}/{_repo}/tree/{_currentPath}";
        if (string.IsNullOrEmpty(_currentPath))
            url = $"https://github.com/{_owner}/{_repo}";
        await _browserLauncher.OpenAsync(url);
    }

    public void Dispose()
    {
        IsLoading.Dispose();
        ErrorMessage.Dispose();
        RepoFullName.Dispose();
        CurrentPath.Dispose();
        CanGoUp.Dispose();
        Owner.Dispose();
        RepoName.Dispose();
    }
}
