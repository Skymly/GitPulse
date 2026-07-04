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
    private bool _loaded;

    public FileBrowserPage(FileBrowserViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    public string OwnerQuery { get; set; } = string.Empty;
    public string RepoQuery { get; set; } = string.Empty;
    public string PathQuery { get; set; } = string.Empty;

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_loaded)
        {
            _loaded = true;
            var owner = Uri.UnescapeDataString(OwnerQuery);
            var repo = Uri.UnescapeDataString(RepoQuery);
            var path = Uri.UnescapeDataString(PathQuery);
            if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo))
            {
                _viewModel.Initialize(owner, repo, path);
                _ = _viewModel.LoadCommand.ExecuteAsync(null);
            }
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
                await Shell.Current.GoToAsync(
                    $"FileEditorPage?owner={Uri.EscapeDataString(_viewModel.Owner.Value)}"
                    + $"&repo={Uri.EscapeDataString(_viewModel.RepoName.Value)}"
                    + $"&path={Uri.EscapeDataString(entry.Path)}"
                    + $"&sha={Uri.EscapeDataString(entry.Sha)}");
            }
        }
    }

    private async void OnNewFileClicked(object? sender, EventArgs e)
    {
        // Navigate to editor without SHA (new file mode).
        // The new file path will be relative to the current directory.
        var currentDir = _viewModel.CurrentPath.Value;
        var basePath = string.IsNullOrEmpty(currentDir) ? "" : currentDir + "/";
        await Shell.Current.GoToAsync(
            $"FileEditorPage?owner={Uri.EscapeDataString(_viewModel.Owner.Value)}"
            + $"&repo={Uri.EscapeDataString(_viewModel.RepoName.Value)}"
            + $"&path={Uri.EscapeDataString(basePath + "new-file.txt")}"
            + "&sha=");
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        _ = Shell.Current.GoToAsync("..");
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Dispose();
    }
}
