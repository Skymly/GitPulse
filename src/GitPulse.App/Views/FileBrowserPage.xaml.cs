using CommunityToolkit.Mvvm.Messaging;
using GitPulse.App.Events;
using GitPulse.Core.Models;
using GitPulse.ViewModels;

namespace GitPulse.App.Views;

/// <summary>
/// File browser page — M5 RestAPI domain showcase for repository
/// content browsing. Receives owner/repo via Shell query parameters
/// and displays the directory tree. Directory selection navigates
/// deeper; file selection opens the editor.
/// </summary>
[QueryProperty("OwnerQuery", "owner")]
[QueryProperty("RepoQuery", "repo")]
[QueryProperty("PathQuery", "path")]
public partial class FileBrowserPage : ContentPage
{
    private readonly FileBrowserViewModel _viewModel;
    private string? _appliedQuery;
    private bool _reloadOnAppear;
    private bool _hadParent;
    private bool _viewModelDisposed;

    public FileBrowserPage(FileBrowserViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        WeakReferenceMessenger.Default.Register<RepoListStaleMessage>(this, (_, message) =>
        {
            if (RepoListStale.Matches(message, RepoListKind.Files, OwnerQuery, RepoQuery))
                _reloadOnAppear = true;
        });
    }

    public string OwnerQuery { get; set; } = string.Empty;
    public string RepoQuery { get; set; } = string.Empty;
    public string PathQuery { get; set; } = string.Empty;

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var owner = OwnerQuery;
        var repo = RepoQuery;
        var path = PathQuery;
        var query = $"{owner}/{repo}/{path}";
        if (_appliedQuery == query)
        {
            if (_reloadOnAppear)
            {
                _reloadOnAppear = false;
                _ = _viewModel.LoadCommand.ExecuteAsync(null);
            }

            return;
        }

        _appliedQuery = query;
        _reloadOnAppear = false;
        if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo))
        {
            _viewModel.Initialize(owner, repo, path);
            _ = _viewModel.LoadCommand.ExecuteAsync(null);
        }
    }

    private async void OnEntrySelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ContentEntry entry)
        {
            ((CollectionView)sender!).SelectedItem = null;

            if (entry.Type == "dir")
            {
                // Navigate into subdirectory — reload the list.
                await _viewModel.NavigateToCommand.ExecuteAsync(entry);
            }
            else
            {
                // Navigate to file editor with path.
                await AppNavigation.GoToAsync(
                    $"FileEditorPage?owner={Uri.EscapeDataString(_viewModel.Owner.Value)}"
                    + $"&repo={Uri.EscapeDataString(_viewModel.RepoName.Value)}"
                    + $"&path={Uri.EscapeDataString(entry.Path)}"
                    + $"&sha={Uri.EscapeDataString(entry.Sha)}");
            }
        }
    }

    private async void OnNewFileClicked(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync(
            "New file",
            "File name in this folder.",
            accept: "Create",
            cancel: "Cancel",
            placeholder: "new-file.txt",
            initialValue: "new-file.txt");
        if (string.IsNullOrWhiteSpace(name))
            return;

        name = name.Trim().Replace('\\', '/').Trim('/');
        if (name.Length == 0)
            return;

        var currentDir = _viewModel.CurrentPath.Value;
        var basePath = string.IsNullOrEmpty(currentDir) ? "" : currentDir + "/";
        await AppNavigation.GoToAsync(
            $"FileEditorPage?owner={Uri.EscapeDataString(_viewModel.Owner.Value)}"
            + $"&repo={Uri.EscapeDataString(_viewModel.RepoName.Value)}"
            + $"&path={Uri.EscapeDataString(basePath + name)}"
            + "&sha=");
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        _ = AppNavigation.GoToAsync("..");
    }

    private void OnOpenSettingsClicked(object? sender, EventArgs e)
    {
        _ = AppNavigation.GoToAsync("//SettingsPage");
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        PageViewModelLifetime.OnParentSet(
            this,
            _viewModel,
            ref _hadParent,
            ref _viewModelDisposed,
            () => WeakReferenceMessenger.Default.UnregisterAll(this));
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Do not dispose here: disappear is tab switch, push, or peek.
        // ViewModel Dispose is Page ViewModel lifetime (leave the back stack).
    }
}
