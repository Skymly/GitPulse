using GitPulse.App.Events;
using GitPulse.Core.Models;
using GitPulse.ViewModels;
using R3;

namespace GitPulse.App.Views;

/// <summary>
/// Pull requests list page — receives owner/repo via Shell navigation query
/// parameters, loads pull requests for the selected repository.
/// </summary>
[QueryProperty("OwnerQuery", "owner")]
[QueryProperty("RepoQuery", "repo")]
public partial class PullRequestsPage : ContentPage
{
    private readonly PullRequestsViewModel _viewModel;
    private readonly CompositeDisposable _events = [];
    private bool _loaded;

    public PullRequestsPage(PullRequestsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        _events.Add(UiEventPipelines.BindLoadMore(
            PullRequestsList,
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

    private async void OpenPrDetail(PullRequest pr)
    {
        await AppNavigation.GoToAsync(
            $"PullRequestDetailPage?owner={Uri.EscapeDataString(_viewModel.Owner.Value)}"
            + $"&repo={Uri.EscapeDataString(_viewModel.RepoName.Value)}"
            + $"&number={pr.Number}");
    }

    private async void OnNewPullRequestClicked(object? sender, EventArgs e)
    {
        await AppNavigation.GoToAsync(
            $"CreatePullRequestPage?owner={Uri.EscapeDataString(_viewModel.Owner.Value)}"
            + $"&repo={Uri.EscapeDataString(_viewModel.RepoName.Value)}");
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        _ = AppNavigation.GoToAsync("..");
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

    private void OnPrSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is PullRequest pr)
        {
            ((CollectionView)sender!).SelectedItem = null;
            OpenPrDetail(pr);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Keep ViewModel(s) alive: pages stay on the navigation stack and are reused on pop.
    }
}
