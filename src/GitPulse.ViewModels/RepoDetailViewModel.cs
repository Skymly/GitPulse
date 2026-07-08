using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// Repository detail view model — the M7 RestAPI domain showcase.
/// Loads repo metadata + README in parallel, then lazily loads branches
/// and releases on tab activation. Demonstrates <see cref="IGitHubReposApi.GetRepo"/>,
/// <see cref="IGitHubReposApi.GetReadme"/>, <see cref="IGitHubReposApi.ListBranches"/>,
/// and <see cref="IGitHubReposApi.ListReleases"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>README decoding:</b> The GitHub README endpoint returns a
/// <see cref="FileContent"/> with base64-encoded <see cref="FileContent.Content"/>.
/// This ViewModel decodes it to a Markdown string for rendering via
/// <c>MarkdownView</c>. A 404 (no README) sets <see cref="HasReadme"/> to false.
/// </para>
/// <para>
/// <b>Lazy loading:</b> Branches and releases are loaded on-demand when
/// their tab is first activated, avoiding three parallel requests on
/// page open. Each section tracks its own loading state.
/// </para>
/// </remarks>
public sealed partial class RepoDetailViewModel : IDisposable
{
    private readonly IGitHubClientFactory _clientFactory;
    private readonly IBrowserLauncher _browserLauncher;

    private string _owner = string.Empty;
    private string _repo = string.Empty;
    private bool _branchesLoaded;
    private bool _releasesLoaded;

    /// <summary>Repository metadata.</summary>
    public BindableReactiveProperty<Repo?> Repo { get; } = new(null);

    /// <summary>Decoded README Markdown text (empty when no README).</summary>
    public BindableReactiveProperty<string> ReadmeMarkdown { get; } = new(string.Empty);

    /// <summary>Whether a README exists for this repository.</summary>
    public BindableReactiveProperty<bool> HasReadme { get; } = new(false);

    /// <summary>Branches collection (lazily loaded).</summary>
    public ObservableCollection<Branch> Branches { get; } = [];

    /// <summary>Releases collection (lazily loaded).</summary>
    public ObservableCollection<Release> Releases { get; } = [];

    /// <summary>Whether the main load (repo + readme) is in progress.</summary>
    public BindableReactiveProperty<bool> IsLoading { get; } = new(false);

    /// <summary>Whether branches are being loaded.</summary>
    public BindableReactiveProperty<bool> IsLoadingBranches { get; } = new(false);

    /// <summary>Whether releases are being loaded.</summary>
    public BindableReactiveProperty<bool> IsLoadingReleases { get; } = new(false);

    /// <summary>Error message; empty when no error.</summary>
    public BindableReactiveProperty<string> ErrorMessage { get; } = new(string.Empty);

    /// <summary>Owner part of the repository.</summary>
    public BindableReactiveProperty<string> Owner { get; } = new(string.Empty);

    /// <summary>Repo name part.</summary>
    public BindableReactiveProperty<string> RepoName { get; } = new(string.Empty);

    /// <summary>Full name for display (owner/repo).</summary>
    public BindableReactiveProperty<string> RepoFullName { get; } = new(string.Empty);

    public RepoDetailViewModel(IGitHubClientFactory clientFactory, IBrowserLauncher browserLauncher)
    {
        _clientFactory = clientFactory;
        _browserLauncher = browserLauncher;
    }

    /// <summary>
    /// Initialize with repository coordinates. Called by the page when
    /// navigated to via Shell query parameters.
    /// </summary>
    public void Initialize(string owner, string repo)
    {
        _owner = owner;
        _repo = repo;
        Owner.Value = owner;
        RepoName.Value = repo;
        RepoFullName.Value = $"{owner}/{repo}";
    }

    /// <summary>Initial load: repo metadata + README in parallel.</summary>
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

            var repo = await api.GetRepo(_owner, _repo).FirstAsync(cts.Token);
            Repo.Value = repo;

            var readme = await TryGetReadmeAsync(api, cts.Token);
            if (readme is not null)
            {
                HasReadme.Value = true;
                ReadmeMarkdown.Value = DecodeBase64Content(readme.Content);
            }
            else
            {
                HasReadme.Value = false;
                ReadmeMarkdown.Value = string.Empty;
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

    /// <summary>
    /// Attempt to fetch the README. Returns null when no README exists (404).
    /// </summary>
    private async Task<FileContent?> TryGetReadmeAsync(IGitHubReposApi api, CancellationToken ct)
    {
        try
        {
            return await api.GetReadme(_owner, _repo).FirstAsync(ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex) when (IsNotFoundError(ex))
        {
            return null;
        }
    }

    private static bool IsNotFoundError(Exception ex)
    {
        if (ex is HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound })
            return true;

        if (ex.Message.Contains("404", StringComparison.Ordinal)
            || ex.Message.Contains("NotFound", StringComparison.OrdinalIgnoreCase))
            return true;

        return ex.InnerException is not null && IsNotFoundError(ex.InnerException);
    }

    /// <summary>Decode base64-encoded file content to a UTF-8 string.</summary>
    private static string DecodeBase64Content(string base64)
    {
        if (string.IsNullOrEmpty(base64))
            return string.Empty;

        var bytes = Convert.FromBase64String(base64.Replace("\n", "").Replace("\r", ""));
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    /// <summary>Load branches (lazy — only on first tab activation).</summary>
    [RelayCommand]
    private async Task LoadBranchesAsync()
    {
        if (_branchesLoaded || IsLoadingBranches.Value)
            return;

        IsLoadingBranches.Value = true;

        try
        {
            var client = await _clientFactory.CreateClientAsync();
            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var branches = await api.ListBranches(_owner, _repo).FirstAsync(cts.Token);
            Branches.Clear();
            foreach (var b in branches)
                Branches.Add(b);
            _branchesLoaded = true;
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

    /// <summary>Load releases (lazy — only on first tab activation).</summary>
    [RelayCommand]
    private async Task LoadReleasesAsync()
    {
        if (_releasesLoaded || IsLoadingReleases.Value)
            return;

        IsLoadingReleases.Value = true;

        try
        {
            var client = await _clientFactory.CreateClientAsync();
            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var releases = await api.ListReleases(_owner, _repo).FirstAsync(cts.Token);
            Releases.Clear();
            foreach (var r in releases)
                Releases.Add(r);
            _releasesLoaded = true;
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Releases request timed out.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Releases load failed: {ex.Message}";
        }
        finally
        {
            IsLoadingReleases.Value = false;
        }
    }

    [RelayCommand]
    private async Task OpenInBrowserAsync(string url)
    {
        if (!string.IsNullOrEmpty(url))
            await _browserLauncher.OpenAsync(url);
    }

    public void Dispose()
    {
        Repo.Dispose();
        ReadmeMarkdown.Dispose();
        HasReadme.Dispose();
        IsLoading.Dispose();
        IsLoadingBranches.Dispose();
        IsLoadingReleases.Dispose();
        ErrorMessage.Dispose();
        Owner.Dispose();
        RepoName.Dispose();
        RepoFullName.Dispose();
    }
}
