using GitPulse.ViewModels;
using GitPulse.Core.Models;

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
    private bool _loaded;

    public IssuesPage(IssuesViewModel viewModel)
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

    private async void OpenIssueDetail(Issue issue)
    {
        await Shell.Current.GoToAsync(
            $"IssueDetailPage?owner={Uri.EscapeDataString(_viewModel.Owner.Value)}"
            + $"&repo={Uri.EscapeDataString(_viewModel.RepoName.Value)}"
            + $"&number={issue.Number}");
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        _ = Shell.Current.GoToAsync("..");
    }

    private async void OnPrsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(
            $"PullRequestsPage?owner={Uri.EscapeDataString(_viewModel.Owner.Value)}"
            + $"&repo={Uri.EscapeDataString(_viewModel.RepoName.Value)}");
    }

    private async void OnNewIssueClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(
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
        var primary = Application.Current?.Resources["Primary"] as Color;
        var gray = Application.Current?.Resources["Gray200"] as Color;

        OpenTab.BackgroundColor = active == "open" ? primary : gray;
        OpenTab.TextColor = active == "open" ? Colors.White : Colors.Black;

        ClosedTab.BackgroundColor = active == "closed" ? primary : gray;
        ClosedTab.TextColor = active == "closed" ? Colors.White : Colors.Black;

        AllTab.BackgroundColor = active == "all" ? primary : gray;
        AllTab.TextColor = active == "all" ? Colors.White : Colors.Black;
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
        _viewModel.Dispose();
    }
}
