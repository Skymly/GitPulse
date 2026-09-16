using System.Text;
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
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly IGitHubClientFactory _clientFactory;
    private readonly IBrowserLauncher _browserLauncher;

    private string _owner = string.Empty;
    private string _repo = string.Empty;
    private string _path = string.Empty;
    private string _sha = string.Empty;
    private string _gitRef = string.Empty;

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

    /// <summary>True after a successful Contents delete. Not an error.</summary>
    public BindableReactiveProperty<bool> FileDeleted { get; } = new(false);

    /// <summary>Whether this is a new file (no existing SHA).</summary>
    public BindableReactiveProperty<bool> IsNewFile { get; } = new(true);

    /// <summary>Whether the file content is binary (cannot be edited as text).</summary>
    public BindableReactiveProperty<bool> IsBinary { get; } = new(false);

    /// <summary>Repository full name for display.</summary>
    public BindableReactiveProperty<string> RepoFullName { get; } = new(string.Empty);

    /// <summary>Title for the page header.</summary>
    public BindableReactiveProperty<string> Title { get; } = new(string.Empty);

    /// <summary>
    /// True when the file was opened at a historical git ref. Save and
    /// delete stay disabled so a commit SHA is never written back as a
    /// Contents blob SHA.
    /// </summary>
    public BindableReactiveProperty<bool> IsReadOnly { get; } = new(false);

    public FileEditorViewModel(IGitHubClientFactory clientFactory, IBrowserLauncher browserLauncher)
    {
        _clientFactory = clientFactory;
        _browserLauncher = browserLauncher;
    }

    /// <summary>
    /// Initialize with repository coordinates and file path. Called by
    /// the page when navigated to via Shell query parameters.
    /// </summary>
    public void Initialize(
        string owner,
        string repo,
        string path,
        string? sha = null,
        string? gitRef = null)
    {
        _owner = owner;
        _repo = repo;
        _path = path;
        _sha = sha ?? string.Empty;
        _gitRef = gitRef ?? string.Empty;

        RepoFullName.Value = $"{owner}/{repo}";
        FilePath.Value = path;
        FileName.Value = path.Contains('/') ? path[(path.LastIndexOf('/') + 1)..] : path;
        IsReadOnly.Value = _gitRef.Length > 0;
        IsNewFile.Value = string.IsNullOrEmpty(_sha) && string.IsNullOrEmpty(_gitRef);
        FileDeleted.Value = false;
        Title.Value = IsNewFile.Value ? $"New: {FileName.Value}" : FileName.Value;
    }

    /// <summary>Load file content from the API and decode from base64.</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(_owner) || string.IsNullOrEmpty(_repo) || string.IsNullOrEmpty(_path))
            return;

        if (IsBusy.Value)
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
            using var scope = await _clientFactory.OpenAsync();
            var client = scope.Client;
            if (client.DefaultRequestHeaders.Authorization is null)
            {
                ErrorMessage.Value = "No token configured. Open Settings to add a GitHub PAT.";
                return;
            }

            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var content = string.IsNullOrEmpty(_gitRef)
                ? await api.GetFileContent(_owner, _repo, _path).FirstAsync(cts.Token)
                : await api.GetFileContentAtRef(_owner, _repo, _path, _gitRef).FirstAsync(cts.Token);

            _sha = content.Sha;
            IsNewFile.Value = false;
            Title.Value = content.Name;

            if (CannotEditInApp(content))
            {
                IsReadOnly.Value = true;
                IsEditing.Value = false;
                IsBinary.Value = false;
                FileContent.Value = string.Empty;
                ErrorMessage.Value = "This file cannot be edited in GitPulse. Open it on GitHub.";
                return;
            }

            if (TryDecodeUtf8Text(content.Content, out var text))
            {
                FileContent.Value = text;
                IsBinary.Value = false;
            }
            else
            {
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
        if (IsBinary.Value || IsReadOnly.Value)
            return;
        IsEditing.Value = !IsEditing.Value;
    }

    /// <summary>Save (create or update) the file content.</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy.Value || IsBinary.Value || IsReadOnly.Value)
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
            using var scope = await _clientFactory.OpenAsync();
            var client = scope.Client;
            if (client.DefaultRequestHeaders.Authorization is null)
            {
                ErrorMessage.Value = "No token configured.";
                return;
            }

            var api = RestService.For<IGitHubReposApi>(client);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var encodedContent = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(FileContent.Value));

            var request = new FileUpdateRequest
            {
                Message = CommitMessage.Value,
                Content = encodedContent,
                Sha = IsNewFile.Value ? null : _sha,
            };

            var response = await api.CreateOrUpdateFile(_owner, _repo, _path, request)
                .FirstAsync(cts.Token);

            // Contents updates require the blob SHA, never the commit SHA.
            if (response.Content?.Sha is { Length: > 0 } newSha)
                _sha = newSha;

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
        if (IsBusy.Value || IsNewFile.Value || IsReadOnly.Value)
            return;

        if (string.IsNullOrWhiteSpace(CommitMessage.Value))
        {
            ErrorMessage.Value = "Please enter a commit message for the deletion.";
            return;
        }

        IsBusy.Value = true;
        ErrorMessage.Value = string.Empty;
        FileDeleted.Value = false;

        try
        {
            using var scope = await _clientFactory.OpenAsync();
            var client = scope.Client;
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

            FileDeleted.Value = true;
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
        await _browserLauncher.OpenAsync(GitHubWebUrl.RepoBlob(_owner, _repo, _path));
    }

    /// <summary>
    /// GitHub omits usable bytes when encoding is not base64 (files 1–100 MB
    /// use <c>encoding: none</c> and an empty or null content). Saving would
    /// PUT empty bytes with the real blob SHA.
    /// </summary>
    private static bool CannotEditInApp(FileContent content)
    {
        if (!string.Equals(content.Encoding, "base64", StringComparison.OrdinalIgnoreCase))
            return true;
        if (content.Size > 1_048_576)
            return true;
        if (content.Content is null || (content.Size > 0 && content.Content.Length == 0))
            return true;
        return false;
    }

    private static bool TryDecodeUtf8Text(string? base64Content, out string text)
    {
        text = string.Empty;
        if (base64Content is null)
            return false;

        try
        {
            var bytes = Convert.FromBase64String(
                base64Content.Replace("\n", "", StringComparison.Ordinal)
                    .Replace("\r", "", StringComparison.Ordinal));
            if (Array.IndexOf(bytes, (byte)0) >= 0)
                return false;

            text = StrictUtf8.GetString(bytes);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
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
        FileDeleted.Dispose();
        IsNewFile.Dispose();
        IsBinary.Dispose();
        RepoFullName.Dispose();
        Title.Dispose();
        IsReadOnly.Dispose();
    }
}
