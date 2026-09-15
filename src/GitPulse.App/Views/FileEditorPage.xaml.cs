using GitPulse.App.Events;
using GitPulse.App.Services;
using GitPulse.ViewModels;
using R3;

namespace GitPulse.App.Views;

/// <summary>
/// File editor page — M5 RestAPI domain showcase for file content
/// viewing and editing. Receives owner/repo/path/sha via Shell query
/// parameters. Supports view mode (read-only), edit mode (create/update),
/// and delete operations with commit messages.
/// </summary>
[QueryProperty("OwnerQuery", "owner")]
[QueryProperty("RepoQuery", "repo")]
[QueryProperty("PathQuery", "path")]
[QueryProperty("ShaQuery", "sha")]
[QueryProperty("RefQuery", "ref")]
public partial class FileEditorPage : ContentPage
{
    private readonly FileEditorViewModel _viewModel;
    private readonly IDisposable _deletedSubscription;
    private string? _appliedQuery;
    private bool _leavingAfterDelete;
    private RetryAction _retryAction = RetryAction.Load;

    private enum RetryAction
    {
        Load,
        Save,
        Delete,
    }

    public FileEditorPage(FileEditorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        _deletedSubscription = _viewModel.FileDeleted
            .Where(deleted => deleted)
            .ObserveOnCurrentSynchronizationContext()
            .Subscribe(deleted => _ = LeaveAfterDeleteAsync());
    }

    public string OwnerQuery { get; set; } = string.Empty;
    public string RepoQuery { get; set; } = string.Empty;
    public string PathQuery { get; set; } = string.Empty;
    public string ShaQuery { get; set; } = string.Empty;
    public string RefQuery { get; set; } = string.Empty;

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var owner = Uri.UnescapeDataString(OwnerQuery);
        var repo = Uri.UnescapeDataString(RepoQuery);
        var path = Uri.UnescapeDataString(PathQuery);
        var sha = Uri.UnescapeDataString(ShaQuery);
        var gitRef = Uri.UnescapeDataString(RefQuery);
        var query = $"{owner}/{repo}/{path}/{sha}/{gitRef}";
        if (_appliedQuery == query)
            return;

        _appliedQuery = query;
        if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo) && !string.IsNullOrEmpty(path))
        {
            _viewModel.Initialize(
                owner,
                repo,
                path,
                string.IsNullOrEmpty(sha) ? null : sha,
                string.IsNullOrEmpty(gitRef) ? null : gitRef);
            _retryAction = RetryAction.Load;
            _ = _viewModel.LoadCommand.ExecuteAsync(null);
            if (!string.IsNullOrEmpty(gitRef))
            {
                EditButton.IsVisible = false;
                DeleteButton.IsVisible = false;
            }
        }
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        _ = AppNavigation.GoToAsync("..");
    }

    private async Task LeaveAfterDeleteAsync()
    {
        if (_leavingAfterDelete)
            return;

        _leavingAfterDelete = true;
        try
        {
            NotifyFileListStale();
            await AppNavigation.GoToAsync("..");
        }
        finally
        {
            _leavingAfterDelete = false;
        }
    }

    private bool ShowCommitFieldErrorIfEmpty()
    {
        var empty = string.IsNullOrWhiteSpace(_viewModel.CommitMessage.Value);
        CommitFieldError.IsVisible = empty;
        return empty;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (ShowCommitFieldErrorIfEmpty())
            return;
        _retryAction = RetryAction.Save;
        await _viewModel.SaveCommand.ExecuteAsync(null);
        NotifyFileListStaleIfSaveSucceeded();
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (ShowCommitFieldErrorIfEmpty())
            return;

        var confirmed = await DestructiveConfirm.ShowAsync(
            this,
            "Delete file?",
            _viewModel.FilePath.Value,
            "Delete");
        if (confirmed)
        {
            _retryAction = RetryAction.Delete;
            await _viewModel.DeleteCommand.ExecuteAsync(null);
        }
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        switch (_retryAction)
        {
            case RetryAction.Save:
                if (ShowCommitFieldErrorIfEmpty())
                    return;
                await _viewModel.SaveCommand.ExecuteAsync(null);
                NotifyFileListStaleIfSaveSucceeded();
                break;
            case RetryAction.Delete:
                await _viewModel.DeleteCommand.ExecuteAsync(null);
                break;
            default:
                await _viewModel.LoadCommand.ExecuteAsync(null);
                break;
        }
    }

    private void NotifyFileListStaleIfSaveSucceeded()
    {
        if (!string.IsNullOrEmpty(_viewModel.ErrorMessage.Value))
            return;

        NotifyFileListStale();
    }

    private void NotifyFileListStale()
    {
        RepoListStale.Send(
            RepoListKind.Files,
            Uri.UnescapeDataString(OwnerQuery),
            Uri.UnescapeDataString(RepoQuery));
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Keep ViewModel(s) alive: pages stay on the navigation stack and are reused on pop.
    }
}
