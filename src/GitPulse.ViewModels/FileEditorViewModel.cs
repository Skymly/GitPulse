using CommunityToolkit.Mvvm.Input;
using GitPulse.Core.Abstractions;
using GitPulse.Core.Models;
using GitPulse.GitHubApi;
using Observables.RestAPI;
using R3;

namespace GitPulse.ViewModels;

/// <summary>
/// File editor view model — the M5 RestAPI domain showcase for
/// file content viewing and editing. Loads file content via
/// <see cref="IGitHubReposApi.GetFileContent"/>, decodes from base64,
/// and supports create/update (PUT) and delete (DELETE) operations.
/// </summary>
/// <remarks>
/// <para>
/// <b>Base64 encoding:</b> The GitHub Contents API returns file content
/// as base64-encoded text. This ViewModel decodes it to a string for
/// display/editing, and re-encodes to base64 before sending updates.
/// Binary files are not supported — only text files (UTF-8 decodable).
/// </para>
/// <para>
/// <b>Update flow:</b> The file's SHA is required for updates (optimistic
/// concurrency). On save, the ViewModel sends the new content + commit
/// message via <see cref="IGitHubReposApi.CreateOrUpdateFile"/>. The
/// response includes the new SHA, which updates the ViewModel state.
/// </para>
/// </remarks>
public sealed partial class FileEditorViewModel : IDisposable
{
    private readonly IGitHubClientFactory _clientFactory;
    private readonly IBrowserLauncher _browserLauncher;

    private string _owner = string.Empty;
    private string _repo = string.Empty;
    private string _path = string.Empty;
    private string _sha = string.Empty;

    /// <summary>Decoded file content for display/editing.</summary>
    public BindableReactiveProperty<string> FileContent { get; } = new(string.Empty);

    /// <summary>Commit message for save/delete operations.</summary>
    public BindableReactiveProperty<string> CommitMessage { get; } = new(string.Empty);

    /// <summary>File name for display.</summary>
    public BindableReactiveProperty<string> FileName { get; } = new(string.Empty);

    /// <summary>Full file path for display.</summary>
    public BindableReactiveProperty<string> FilePath { get; } = new(string.Empty);

    /// <summary>Whether the editor is in edit mode (vs. view mode).</summary>
    public BindableReactiveProperty<bool> IsEditing { get; } = new(false);

    /// <summary>Whether a load/save/delete operation is in progress.</summary>
    public BindableReactiveProperty<bool> IsBusy { get; } = new(false);

    /// <summary>Error message; empty when no error.</summary>
    public BindableReactiveProperty<string> ErrorMessage { get; } = new(string.Empty);

    /// <summary>Whether this is a new file (no existing SHA).</summary>
    public BindableReactiveProperty<bool> IsNewFile { get; } = new(true);

    /// <summary>Whether the file content is binary (cannot be edited as text).</summary>
    public BindableReactiveProperty<bool> IsBinary { get; } = new(false);

    /// <summary>Repository full name for display.</summary>
    public BindableReactiveProperty<string> RepoFullName { get; } = new(string.Empty);

    /// <summary>Title for the page header.</summary>
    public BindableReactiveProperty<string> Title { get; } = new(string.Empty);

    public FileEditorViewModel(IGitHubClientFactory clientFactory, IBrowserLauncher browserLauncher)
    {
        _clientFactory = clientFactory;
        _browserLauncher = browserLauncher;
    }

    /// <summary>
    /// Initialize with repository coordinates and file path. Called by
    /// the page when navigated to via Shell query parameters.
    /// </summary>
    public void Initialize(string owner, string repo, string path, string? sha = null)
    {
        _owner = owner;
        _repo = repo;
        _path = path;
        _sha = sha ?? string.Empty;

        RepoFullName.Value = $"{owner}/{repo}";
        FilePath.Value = path;
        FileName.Value = path.Contains('/') ? path[(path.LastIndexOf('/') + 1)..] : path;
        IsNewFile.Value = string.IsNullOrEmpty(_sha);
        Title.Value = IsNewFile.Value ? $"New: {FileName.Value}" : FileName.Value;
    }

    /// <summary>Load file content from the API and decode from base64.</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(_owner) || string.IsNullOrEmpty(_repo) || string.IsNullOrEmpty(_path))
            return;

        // Don't load if this is a new file (no existing content).
        if (IsNewFile.Value)
        {
            IsEditing.Value = true;
            return;
        }

        IsBusy.Value = true;
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
            var content = await api.GetFileContent(_owner, _repo, _path).FirstAsync(cts.Token);

            _sha = content.Sha;
            IsNewFile.Value = false;
            Title.Value = content.Name;

            // Decode base64 content to UTF-8 string.
            try
            {
                var bytes = Convert.FromBase64String(content.Content.Replace("\n", "").Replace("\r", ""));
                FileContent.Value = System.Text.Encoding.UTF8.GetString(bytes);
                IsBinary.Value = false;
            }
            catch
            {
                // If base64 decode or UTF-8 decode fails, it's a binary file.
                FileContent.Value = "[Binary file — cannot display as text]";
                IsBinary.Value = true;
                IsEditing.Value = false;
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
            IsBusy.Value = false;
        }
    }

    /// <summary>Toggle between view and edit mode.</summary>
    [RelayCommand]
    private void ToggleEdit()
    {
        if (IsBinary.Value)
            return;
        IsEditing.Value = !IsEditing.Value;
    }

    /// <summary>Save (create or update) the file content.</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy.Value || IsBinary.Value)
            return;

        if (string.IsNullOrWhiteSpace(CommitMessage.Value))
        {
            ErrorMessage.Value = "Please enter a commit message.";
            return;
        }

        IsBusy.Value = true;
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

            var encodedContent = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(FileContent.Value));

            var request = new FileUpdateRequest
            {
                Message = CommitMessage.Value,
                Content = encodedContent,
                Sha = IsNewFile.Value ? null : _sha,
            };

            var response = await api.CreateOrUpdateFile(_owner, _repo, _path, request)
                .FirstAsync(cts.Token);

            // Update SHA from response for subsequent edits.
            if (response.Content?.Sha is { } newSha)
                _sha = newSha;
            else if (response.Commit?.Sha is { } commitSha)
                _sha = commitSha;

            IsNewFile.Value = false;
            IsEditing.Value = false;
            Title.Value = FileName.Value;
            CommitMessage.Value = string.Empty;
            ErrorMessage.Value = string.Empty;
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Request timed out.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsBusy.Value = false;
        }
    }

    /// <summary>Delete the file.</summary>
    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (IsBusy.Value || IsNewFile.Value)
            return;

        if (string.IsNullOrWhiteSpace(CommitMessage.Value))
        {
            ErrorMessage.Value = "Please enter a commit message for the deletion.";
            return;
        }

        IsBusy.Value = true;
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

            var request = new FileDeleteRequest
            {
                Message = CommitMessage.Value,
                Sha = _sha,
            };

            await api.DeleteFile(_owner, _repo, _path, request).FirstAsync(cts.Token);

            ErrorMessage.Value = "File deleted successfully.";
            FileContent.Value = string.Empty;
            IsEditing.Value = false;
        }
        catch (OperationCanceledException)
        {
            ErrorMessage.Value = "Request timed out.";
        }
        catch (Exception ex)
        {
            ErrorMessage.Value = $"Delete failed: {ex.Message}";
        }
        finally
        {
            IsBusy.Value = false;
        }
    }

    /// <summary>Open the file on GitHub.com in the browser.</summary>
    [RelayCommand]
    private async Task OpenInBrowserAsync()
    {
        var url = $"https://github.com/{_owner}/{_repo}/blob/HEAD/{_path}";
        await _browserLauncher.OpenAsync(url);
    }

    public void Dispose()
    {
        FileContent.Dispose();
        CommitMessage.Dispose();
        FileName.Dispose();
        FilePath.Dispose();
        IsEditing.Dispose();
        IsBusy.Dispose();
        ErrorMessage.Dispose();
        IsNewFile.Dispose();
        IsBinary.Dispose();
        RepoFullName.Dispose();
        Title.Dispose();
    }
}
