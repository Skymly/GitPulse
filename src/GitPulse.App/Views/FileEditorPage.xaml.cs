using GitPulse.ViewModels;

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
public partial class FileEditorPage : ContentPage
{
    private readonly FileEditorViewModel _viewModel;
    private bool _loaded;

    public FileEditorPage(FileEditorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    public string OwnerQuery { get; set; } = string.Empty;
    public string RepoQuery { get; set; } = string.Empty;
    public string PathQuery { get; set; } = string.Empty;
    public string ShaQuery { get; set; } = string.Empty;

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_loaded)
        {
            _loaded = true;
            var owner = Uri.UnescapeDataString(OwnerQuery);
            var repo = Uri.UnescapeDataString(RepoQuery);
            var path = Uri.UnescapeDataString(PathQuery);
            var sha = Uri.UnescapeDataString(ShaQuery);
            if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo) && !string.IsNullOrEmpty(path))
            {
                _viewModel.Initialize(owner, repo, path, string.IsNullOrEmpty(sha) ? null : sha);
                _ = _viewModel.LoadCommand.ExecuteAsync(null);
            }
        }
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
