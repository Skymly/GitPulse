using GitPulse.App.Events;
using GitPulse.ViewModels;
using GitPulse.Core.Models;
using R3;

namespace GitPulse.App.Views;

/// <summary>
/// Repos page code-behind — Observables.Events.R3 pipelines for filter text
/// and remaining-items load more. SearchBar still uses the ADR-007 adapter
/// because <c>SearchBar.Events()</c> hits CS0122.
/// </summary>
public partial class ReposPage : ContentPage
{
    private readonly ReposViewModel _viewModel;
    private readonly CompositeDisposable _events = [];
    private bool _loaded;

    public ReposPage(ReposViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        _events.Add(UiEventPipelines.BindSearchText(SearchBar, _viewModel.SearchText));
        _events.Add(UiEventPipelines.BindLoadMore(
            ReposList,
            _viewModel.CanLoadMore,
            () => _viewModel.LoadMoreCommand.ExecuteAsync(null)));
        _events.Add(_viewModel.SelectedHub.Subscribe(_ =>
            Dispatcher.Dispatch(UpdateHubStyles)));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        PullToRefresh.WindowsOff(ListRefresh);
        UpdateHubStyles();

        if (!_loaded)
        {
            _loaded = true;
            _ = _viewModel.LoadCommand.ExecuteAsync(null);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Keep the Events pipelines and ViewModel alive: Repos is a root tab and is
        // reused when switching back (same pattern as NotificationsPage).
    }

    private async void OnRepoSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Repo repo)
        {
            // Deselect so the same item can be re-tapped later.
            ((CollectionView)sender!).SelectedItem = null;

            // Parse owner/repo from FullName (format: "owner/repo").
            var parts = repo.FullName.Split('/', 2);
            if (parts.Length == 2)
            {
                await AppNavigation.GoToAsync(
                    $"RepoDetailPage?owner={Uri.EscapeDataString(parts[0])}" +
                    $"&repo={Uri.EscapeDataString(parts[1])}");
            }
        }
    }

    private void UpdateHubStyles()
    {
        var starred = string.Equals(
            _viewModel.SelectedHub.Value,
            ReposViewModel.StarredHub,
            StringComparison.Ordinal);
        ChromeTabs.Style(MyReposHubButton, !starred);
        ChromeTabs.Style(StarredHubButton, starred);
    }

    private void OnOpenSettingsClicked(object? sender, EventArgs e)
    {
        _ = AppNavigation.GoToAsync("//SettingsPage");
    }

}
