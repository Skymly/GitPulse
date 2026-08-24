using GitPulse.Core.Models;
using GitPulse.ViewModels;

namespace GitPulse.App.Views;

/// <summary>
/// In-app Git Commit — receives owner/repo/sha via Shell query parameters.
/// </summary>
[QueryProperty(nameof(OwnerQuery), "owner")]
[QueryProperty(nameof(RepoQuery), "repo")]
[QueryProperty(nameof(ShaQuery), "sha")]
public partial class CommitDetailPage : ContentPage
{
    private readonly CommitDetailViewModel _viewModel;
    private bool _loaded;

    public CommitDetailPage(CommitDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    public string OwnerQuery { get; set; } = string.Empty;
    public string RepoQuery { get; set; } = string.Empty;
    public string ShaQuery { get; set; } = string.Empty;

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_loaded)
        {
            _loaded = true;
            var owner = Uri.UnescapeDataString(OwnerQuery);
            var repo = Uri.UnescapeDataString(RepoQuery);
            var sha = Uri.UnescapeDataString(ShaQuery);
            if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo) && !string.IsNullOrEmpty(sha))
            {
                _viewModel.Initialize(owner, repo, sha);
                _ = _viewModel.LoadCommand.ExecuteAsync(null);
            }
        }
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        _ = AppNavigation.GoToAsync("..");
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Keep ViewModel(s) alive: pages stay on the navigation stack and are reused on pop.
    }

    private async void OnCheckRunOpened(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: CheckRun run } || run.Id <= 0)
            return;

        await AppNavigation.GoToAsync(
            $"CheckRunDetailPage?owner={Uri.EscapeDataString(OwnerQuery)}"
            + $"&repo={Uri.EscapeDataString(RepoQuery)}"
            + $"&checkRunId={run.Id}");
    }
}
