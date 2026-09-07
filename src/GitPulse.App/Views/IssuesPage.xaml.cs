using GitPulse.App.Events;
using GitPulse.ViewModels;
using GitPulse.Core.Models;
using R3;

namespace GitPulse.App.Views;

/// <summary>
/// Issues list page — receives owner/repo via Shell navigation query
/// parameters, loads issues for the selected repository.
/// </summary>
[QueryProperty("OwnerQuery", "owner")]
[QueryProperty("RepoQuery", "repo")]
public partial class IssuesPage : ContentPage
{
    private readonly IssuesViewModel _viewModel;
    private readonly CompositeDisposable _events = [];
    private bool _loaded;

    public IssuesPage(IssuesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        _events.Add(UiEventPipelines.BindLoadMore(
            IssuesList,
            _viewModel.CanLoadMore,
            () => _viewModel.LoadMoreCommand.ExecuteAsync(null)));
    }

    // Shell binds query parameters to these properties.
    public string OwnerQuery { get; set; } = string.Empty;
    public string RepoQuery { get; set; } = string.Empty;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        PullToRefresh.WindowsOff(ListRefresh);

        if (!_loaded)
        {
            _loaded = true;
            var owner = Uri.UnescapeDataString(OwnerQuery);
            var repo = Uri.UnescapeDataString(RepoQuery);
            if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo))
            {
                _viewModel.Initialize(owner, repo);
                UpdateTabStyles("open");
                _ = _viewModel.LoadCommand.ExecuteAsync(null);
            }
        }
    }

    private async void OpenIssueDetail(Issue issue)
    {
        await AppNavigation.GoToAsync(
            $"IssueDetailPage?owner={Uri.EscapeDataString(_viewModel.Owner.Value)}"
            + $"&repo={Uri.EscapeDataString(_viewModel.RepoName.Value)}"
            + $"&number={issue.Number}");
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        _ = AppNavigation.GoToAsync("..");
    }

    private async void OnPrsClicked(object? sender, EventArgs e)
    {
        await AppNavigation.GoToAsync(
            $"PullRequestsPage?owner={Uri.EscapeDataString(_viewModel.Owner.Value)}"
            + $"&repo={Uri.EscapeDataString(_viewModel.RepoName.Value)}");
    }

    private async void OnFilesClicked(object? sender, EventArgs e)
    {
        await AppNavigation.GoToAsync(
            $"FileBrowserPage?owner={Uri.EscapeDataString(_viewModel.Owner.Value)}"
            + $"&repo={Uri.EscapeDataString(_viewModel.RepoName.Value)}");
    }

    private async void OnNewIssueClicked(object? sender, EventArgs e)
    {
        await AppNavigation.GoToAsync(
            $"CreateIssuePage?owner={Uri.EscapeDataString(_viewModel.Owner.Value)}"
            + $"&repo={Uri.EscapeDataString(_viewModel.RepoName.Value)}");
    }

    private void OnFilterOpen(object? sender, EventArgs e)
    {
        _viewModel.StateFilter.Value = "open";
        UpdateTabStyles("open");
    }

    private void OnFilterClosed(object? sender, EventArgs e)
    {
        _viewModel.StateFilter.Value = "closed";
        UpdateTabStyles("closed");
    }

    private void OnFilterAll(object? sender, EventArgs e)
    {
        _viewModel.StateFilter.Value = "all";
        UpdateTabStyles("all");
    }

    private void UpdateTabStyles(string active)
    {
        ChromeTabs.Style(OpenTab, active == "open");
        ChromeTabs.Style(ClosedTab, active == "closed");
        ChromeTabs.Style(AllTab, active == "all");
    }

    private void OnIssueSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Issue issue)
        {
            ((CollectionView)sender!).SelectedItem = null;
            OpenIssueDetail(issue);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Keep ViewModel(s) alive: pages stay on the navigation stack and are reused on pop.
    }
}
