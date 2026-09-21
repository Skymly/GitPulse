using GitPulse.App.Events;
using GitPulse.Core.Models;
using GitPulse.ViewModels;
using R3;

namespace GitPulse.App.Views;

public partial class SearchPage : ContentPage
{
    private readonly SearchViewModel _viewModel;
    private readonly CompositeDisposable _loadMore = [];
    private IDisposable? _searchPipeline;

    public SearchPage(SearchViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        BindLoadMore(RepositoriesList);
        BindLoadMore(IssuesList);
        BindLoadMore(PullRequestsList);
        BindLoadMore(CodeList);
        BindLoadMore(ReviewRequestedList);
        BindLoadMore(AssignedList);
        BindLoadMore(MentionsList);
    }

    private void BindLoadMore(CollectionView list)
    {
        _loadMore.Add(UiEventPipelines.BindLoadMore(
            list,
            _viewModel.CanLoadMore,
            () => _viewModel.LoadMoreCommand.ExecuteAsync(null)));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        SearchBar.Text = _viewModel.Query.Value;
        StartSearchBridge();
        ListRefresh.Command = new Command(RefreshVisibleInbox);
        UpdateTabStyles(_viewModel.SelectedType.Value);
        UpdateHubStyles();
    }

    protected override void OnDisappearing()
    {
        StopSearchBridge();
        base.OnDisappearing();
        // Do not dispose the ViewModel: Search is a root tab (Page ViewModel lifetime).
    }

    private void StartSearchBridge()
    {
        if (_searchPipeline is not null)
            return;

        _searchPipeline = UiEventPipelines.BindSearchText(SearchBar, _viewModel.Query);
    }

    private void StopSearchBridge()
    {
        _searchPipeline?.Dispose();
        _searchPipeline = null;
    }

    private void OnSearchSubmitted(object? sender, EventArgs e)
    {
        SubmitSearch();
    }

    private void OnSearchClicked(object? sender, EventArgs e)
    {
        SubmitSearch();
    }

    private void OnSearchHubClicked(object? sender, EventArgs e)
    {
        _ = _viewModel.SelectHubCommand.ExecuteAsync(SearchViewModel.SearchHub);
        UpdateHubStyles();
        UpdateTabStyles(_viewModel.SelectedType.Value);
    }

    private void OnReviewRequestedHubClicked(object? sender, EventArgs e)
    {
        _ = _viewModel.SelectHubCommand.ExecuteAsync(SearchViewModel.ReviewRequestedHub);
        UpdateHubStyles();
    }

    private void OnAssignedHubClicked(object? sender, EventArgs e)
    {
        _ = _viewModel.SelectHubCommand.ExecuteAsync(SearchViewModel.AssignedHub);
        UpdateHubStyles();
    }

    private void OnMentionsHubClicked(object? sender, EventArgs e)
    {
        _ = _viewModel.SelectHubCommand.ExecuteAsync(SearchViewModel.MentionsHub);
        UpdateHubStyles();
    }

    private void SubmitSearch()
    {
        var query = SearchBar.Text ?? string.Empty;
        var tooShort = query.Trim().Length < 3;
        QueryFieldError.IsVisible = tooShort;
        if (tooShort)
            return;
        _viewModel.Query.Value = query;
        _ = _viewModel.SearchCommand.ExecuteAsync(null);
    }

    private void OnRepositoriesClicked(object? sender, EventArgs e)
    {
        SelectType(SearchType.Repositories);
    }

    private void OnIssuesClicked(object? sender, EventArgs e)
    {
        SelectType(SearchType.Issues);
    }

    private void OnPullRequestsClicked(object? sender, EventArgs e)
    {
        SelectType(SearchType.PullRequests);
    }

    private void OnCodeClicked(object? sender, EventArgs e)
    {
        SelectType(SearchType.Code);
    }

    private void SelectType(SearchType type)
    {
        _viewModel.SelectTypeCommand.Execute(type);
        UpdateHubStyles();
        UpdateTabStyles(type);
    }

    private void UpdateTabStyles(SearchType active)
    {
        ChromeTabs.Style(RepositoriesTab, active == SearchType.Repositories);
        ChromeTabs.Style(IssuesTab, active == SearchType.Issues);
        ChromeTabs.Style(PullRequestsTab, active == SearchType.PullRequests);
        ChromeTabs.Style(CodeTab, active == SearchType.Code);
    }

    private void UpdateHubStyles()
    {
        ChromeTabs.Style(SearchHubButton, _viewModel.IsSearchHub.Value);
        ChromeTabs.Style(ReviewRequestedHubButton, _viewModel.IsReviewRequestedHub.Value);
        ChromeTabs.Style(AssignedHubButton, _viewModel.IsAssignedHub.Value);
        ChromeTabs.Style(MentionsHubButton, _viewModel.IsMentionsHub.Value);
        UpdateInboxRefreshEnabled();
    }

    private void UpdateInboxRefreshEnabled()
    {
#if ANDROID
        ListRefresh.IsEnabled = _viewModel.IsReviewRequestedHub.Value
            || _viewModel.IsAssignedHub.Value
            || _viewModel.IsMentionsHub.Value;
#else
        PullToRefresh.WindowsOff(ListRefresh);
#endif
    }

    private void RefreshVisibleInbox()
    {
        if (_viewModel.IsReviewRequestedHub.Value)
            _ = _viewModel.SelectHubCommand.ExecuteAsync(SearchViewModel.ReviewRequestedHub);
        else if (_viewModel.IsAssignedHub.Value)
            _ = _viewModel.SelectHubCommand.ExecuteAsync(SearchViewModel.AssignedHub);
        else if (_viewModel.IsMentionsHub.Value)
            _ = _viewModel.SelectHubCommand.ExecuteAsync(SearchViewModel.MentionsHub);
    }

    private async void OnRepositorySelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not Repo repo)
            return;

        ((CollectionView)sender!).SelectedItem = null;
        if (TrySplitFullName(repo.FullName, out var owner, out var name))
        {
            await AppNavigation.GoToAsync(
                $"RepoDetailPage?owner={Uri.EscapeDataString(owner)}"
                + $"&repo={Uri.EscapeDataString(name)}");
        }
    }

    private async void OnIssueSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SearchIssueItem issue)
            return;

        ((CollectionView)sender!).SelectedItem = null;
        if (TryParseRepositoryUrl(issue.RepositoryUrl, out var owner, out var repo))
        {
            await AppNavigation.GoToAsync(
                $"IssueDetailPage?owner={Uri.EscapeDataString(owner)}"
                + $"&repo={Uri.EscapeDataString(repo)}"
                + $"&number={issue.Number}");
        }
    }

    private async void OnPullRequestSelected(object? sender, SelectionChangedEventArgs e)
    {
        await OpenPullRequestAsync(sender, e);
    }

    private async void OnReviewRequestedSelected(object? sender, SelectionChangedEventArgs e)
    {
        await OpenPullRequestAsync(sender, e);
    }

    private async void OnAssignedSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SearchIssueItem item)
            return;

        ((CollectionView)sender!).SelectedItem = null;
        if (!TryParseRepositoryUrl(item.RepositoryUrl, out var owner, out var repo))
            return;

        var page = item.PullRequest is null ? "IssueDetailPage" : "PullRequestDetailPage";
        await AppNavigation.GoToAsync(
            $"{page}?owner={Uri.EscapeDataString(owner)}"
            + $"&repo={Uri.EscapeDataString(repo)}"
            + $"&number={item.Number}");
    }

    private static async Task OpenPullRequestAsync(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SearchIssueItem pullRequest)
            return;

        ((CollectionView)sender!).SelectedItem = null;
        if (TryParseRepositoryUrl(pullRequest.RepositoryUrl, out var owner, out var repo))
        {
            await AppNavigation.GoToAsync(
                $"PullRequestDetailPage?owner={Uri.EscapeDataString(owner)}"
                + $"&repo={Uri.EscapeDataString(repo)}"
                + $"&number={pullRequest.Number}");
        }
    }

    private async void OnCodeSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not CodeSearchItem code)
            return;

        ((CollectionView)sender!).SelectedItem = null;
        if (TrySplitFullName(code.Repository.FullName, out var owner, out var repo))
        {
            await AppNavigation.GoToAsync(
                $"FileEditorPage?owner={Uri.EscapeDataString(owner)}"
                + $"&repo={Uri.EscapeDataString(repo)}"
                + $"&path={Uri.EscapeDataString(code.Path)}"
                + $"&sha={Uri.EscapeDataString(code.Sha)}");
        }
    }

    private static bool TryParseRepositoryUrl(
        string repositoryUrl,
        out string owner,
        out string repo)
    {
        owner = string.Empty;
        repo = string.Empty;

        if (!Uri.TryCreate(repositoryUrl, UriKind.Absolute, out var uri))
            return false;

        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 3
            || !segments[0].Equals("repos", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        owner = Uri.UnescapeDataString(segments[1]);
        repo = Uri.UnescapeDataString(segments[2]);
        return owner.Length > 0 && repo.Length > 0;
    }

    private static bool TrySplitFullName(
        string fullName,
        out string owner,
        out string repo)
    {
        owner = string.Empty;
        repo = string.Empty;

        var parts = fullName.Split('/', 2);
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            return false;

        owner = parts[0];
        repo = parts[1];
        return true;
    }

    private void OnOpenSettingsClicked(object? sender, EventArgs e)
    {
        _ = AppNavigation.GoToAsync("//SettingsPage");
    }

}



