using GitPulse.Core.Models;
using GitPulse.ViewModels;
using R3;

namespace GitPulse.App.Views;

public partial class SearchPage : ContentPage
{
    private readonly SearchViewModel _viewModel;
    private Subject<string>? _searchSubject;
    private IDisposable? _searchSubscription;

    public SearchPage(SearchViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        SearchBar.Text = _viewModel.Query.Value;
        StartSearchBridge();
        UpdateTabStyles(_viewModel.SelectedType.Value);
    }

    protected override void OnDisappearing()
    {
        StopSearchBridge();
        base.OnDisappearing();
    }

    private void StartSearchBridge()
    {
        if (_searchSubject is not null)
            return;

        _searchSubject = new Subject<string>();
        _searchSubscription = _searchSubject
            .Debounce(TimeSpan.FromMilliseconds(300), TimeProvider.System)
            .DistinctUntilChanged()
            .ObserveOnCurrentSynchronizationContext()
            .Subscribe(text =>
            {
                if (_viewModel.Query.Value != text)
                    _viewModel.Query.Value = text;
            });
        SearchBar.TextChanged += OnSearchBarTextChanged;
    }

    private void StopSearchBridge()
    {
        SearchBar.TextChanged -= OnSearchBarTextChanged;
        _searchSubscription?.Dispose();
        _searchSubscription = null;
        _searchSubject?.Dispose();
        _searchSubject = null;
    }

    private void OnSearchBarTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchSubject?.OnNext(e.NewTextValue ?? string.Empty);
    }

    private void OnSearchSubmitted(object? sender, EventArgs e)
    {
        SubmitSearch();
    }

    private void OnSearchClicked(object? sender, EventArgs e)
    {
        SubmitSearch();
    }

    private void SubmitSearch()
    {
        _viewModel.Query.Value = SearchBar.Text ?? string.Empty;
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
        _viewModel.SelectedType.Value = type;
        UpdateTabStyles(type);
    }

    private void UpdateTabStyles(SearchType active)
    {
        var primary = Application.Current?.Resources["Primary"] as Color;
        var gray = Application.Current?.Resources["Gray200"] as Color;

        StyleTab(RepositoriesTab, active == SearchType.Repositories, primary, gray);
        StyleTab(IssuesTab, active == SearchType.Issues, primary, gray);
        StyleTab(PullRequestsTab, active == SearchType.PullRequests, primary, gray);
        StyleTab(CodeTab, active == SearchType.Code, primary, gray);
    }

    private static void StyleTab(Button button, bool active, Color? primary, Color? gray)
    {
        button.BackgroundColor = active ? primary : gray;
        button.TextColor = active ? Colors.White : Colors.Black;
    }

    private async void OnRepositorySelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not Repo repo)
            return;

        ((CollectionView)sender!).SelectedItem = null;
        if (TrySplitFullName(repo.FullName, out var owner, out var name))
        {
            await Shell.Current.GoToAsync(
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
            await Shell.Current.GoToAsync(
                $"IssueDetailPage?owner={Uri.EscapeDataString(owner)}"
                + $"&repo={Uri.EscapeDataString(repo)}"
                + $"&number={issue.Number}");
        }
    }

    private async void OnPullRequestSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SearchIssueItem pullRequest)
            return;

        ((CollectionView)sender!).SelectedItem = null;
        if (TryParseRepositoryUrl(pullRequest.RepositoryUrl, out var owner, out var repo))
        {
            await Shell.Current.GoToAsync(
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
            await Shell.Current.GoToAsync(
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
}
