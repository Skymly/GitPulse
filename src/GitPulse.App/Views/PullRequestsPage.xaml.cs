using GitPulse.Core.Models;
using GitPulse.ViewModels;

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
    private bool _loaded;

    public PullRequestsPage(PullRequestsViewModel viewModel)
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
                UpdateTabStyles("open");
                _ = _viewModel.LoadCommand.ExecuteAsync(null);
            }
        }
    }

    private async void OpenPrDetail(PullRequest pr)
    {
        await Shell.Current.GoToAsync(
            $"PullRequestDetailPage?owner={Uri.EscapeDataString(_viewModel.Owner.Value)}"
            + $"&repo={Uri.EscapeDataString(_viewModel.RepoName.Value)}"
            + $"&number={pr.Number}");
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        _ = Shell.Current.GoToAsync("..");
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
        var primary = Application.Current?.Resources["Primary"] as Color;
        var gray = Application.Current?.Resources["Gray200"] as Color;

        OpenTab.BackgroundColor = active == "open" ? primary : gray;
        OpenTab.TextColor = active == "open" ? Colors.White : Colors.Black;

        ClosedTab.BackgroundColor = active == "closed" ? primary : gray;
        ClosedTab.TextColor = active == "closed" ? Colors.White : Colors.Black;

        AllTab.BackgroundColor = active == "all" ? primary : gray;
        AllTab.TextColor = active == "all" ? Colors.White : Colors.Black;
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
        _viewModel.Dispose();
    }
}
