using GitPulse.ViewModels;

namespace GitPulse.App.Views;

/// <summary>
/// Repository detail page — receives owner/repo via Shell navigation query
/// parameters. Shows repo metadata + README (Overview tab), branches
/// (Branches tab, lazily loaded), and releases (Releases tab, lazily loaded).
/// </summary>
[QueryProperty("OwnerQuery", "owner")]
[QueryProperty("RepoQuery", "repo")]
public partial class RepoDetailPage : ContentPage
{
    private readonly RepoDetailViewModel _viewModel;
    private bool _loaded;

    public RepoDetailPage(RepoDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    // Shell binds query parameters to these properties.
    public string OwnerQuery { get; set; } = string.Empty;
    public string RepoQuery { get; set; } = string.Empty;

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_loaded)
        {
            _loaded = true;
            var owner = Uri.UnescapeDataString(OwnerQuery);
            var repo = Uri.UnescapeDataString(RepoQuery);
            if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo))
            {
                _viewModel.Initialize(owner, repo);
                _ = _viewModel.LoadCommand.ExecuteAsync(null);
            }
        }
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        _ = Shell.Current.GoToAsync("..");
    }

    private async void OnIssuesClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(
            $"IssuesPage?owner={Uri.EscapeDataString(_viewModel.Owner.Value)}"
            + $"&repo={Uri.EscapeDataString(_viewModel.RepoName.Value)}");
    }

    private async void OnPullRequestsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(
            $"PullRequestsPage?owner={Uri.EscapeDataString(_viewModel.Owner.Value)}"
            + $"&repo={Uri.EscapeDataString(_viewModel.RepoName.Value)}");
    }

    private async void OnFilesClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(
            $"FileBrowserPage?owner={Uri.EscapeDataString(_viewModel.Owner.Value)}"
            + $"&repo={Uri.EscapeDataString(_viewModel.RepoName.Value)}");
    }

    private async void OnActionsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(
            $"WorkflowRunsPage?owner={Uri.EscapeDataString(_viewModel.Owner.Value)}"
            + $"&repo={Uri.EscapeDataString(_viewModel.RepoName.Value)}");
    }

    // ── Tab switching ──────────────────────────────────────────────

    private void OnOverviewTabClicked(object? sender, EventArgs e)
    {
        ShowTab("overview");
    }

    private void OnBranchesTabClicked(object? sender, EventArgs e)
    {
        ShowTab("branches");
        // Lazy-load branches on first tab activation.
        if (!_viewModel.IsLoadingBranches.Value && _viewModel.Branches.Count == 0)
            _ = _viewModel.LoadBranchesCommand.ExecuteAsync(null);
    }

    private void OnReleasesTabClicked(object? sender, EventArgs e)
    {
        ShowTab("releases");
        // Lazy-load releases on first tab activation.
        if (!_viewModel.IsLoadingReleases.Value && _viewModel.Releases.Count == 0)
            _ = _viewModel.LoadReleasesCommand.ExecuteAsync(null);
    }

    private void ShowTab(string tab)
    {
        var primary = Application.Current?.Resources["Primary"] as Color;
        var gray = Application.Current?.Resources["Gray200"] as Color;

        OverviewSection.IsVisible = tab == "overview";
        BranchesSection.IsVisible = tab == "branches";
        ReleasesSection.IsVisible = tab == "releases";

        OverviewTab.BackgroundColor = tab == "overview" ? primary : gray;
        OverviewTab.TextColor = tab == "overview" ? Colors.White : Colors.Black;

        BranchesTab.BackgroundColor = tab == "branches" ? primary : gray;
        BranchesTab.TextColor = tab == "branches" ? Colors.White : Colors.Black;

        ReleasesTab.BackgroundColor = tab == "releases" ? primary : gray;
        ReleasesTab.TextColor = tab == "releases" ? Colors.White : Colors.Black;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Dispose();
    }
}
