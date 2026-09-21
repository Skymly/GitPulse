using GitPulse.App.Events;
using GitPulse.ViewModels;
using R3;

namespace GitPulse.App.Views;

/// <summary>
/// New issue creation page — receives owner/repo via Shell navigation
/// query parameters. On successful issue creation, pops this page then
/// opens the new issue detail so Back returns to the list.
/// </summary>
[QueryProperty("OwnerQuery", "owner")]
[QueryProperty("RepoQuery", "repo")]
public partial class CreateIssuePage : ContentPage
{
    private readonly CreateIssueViewModel _viewModel;
    private IDisposable? _createdSubscription;
    private string? _appliedQuery;
    private bool _navigatingToDetail;
    private bool _hadParent;
    private bool _viewModelDisposed;

    public CreateIssuePage(CreateIssueViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    public string OwnerQuery { get; set; } = string.Empty;
    public string RepoQuery { get; set; } = string.Empty;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _createdSubscription ??= _viewModel.CreatedIssueNumber
            .Where(n => n is not null)
            .ObserveOnCurrentSynchronizationContext()
            .Subscribe(n => _ = NavigateToCreatedIssueAsync(n!.Value));

        var owner = OwnerQuery;
        var repo = RepoQuery;
        var query = $"{owner}/{repo}";
        if (_appliedQuery == query)
            return;

        _appliedQuery = query;
        IdentityLabel.Text = $"{owner}/{repo}";
        if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo))
        {
            _viewModel.Initialize(owner, repo);
        }
    }

    private async Task NavigateToCreatedIssueAsync(int number)
    {
        if (_navigatingToDetail)
            return;

        _navigatingToDetail = true;
        _viewModel.CreatedIssueNumber.Value = null;
        ClearCreateForm();
        var owner = OwnerQuery;
        var repo = RepoQuery;
        RepoListStale.Send(RepoListKind.Issues, owner, repo);
        try
        {
            await AppNavigation.PopThenGoToAsync(
                $"IssueDetailPage?owner={Uri.EscapeDataString(owner)}"
                + $"&repo={Uri.EscapeDataString(repo)}"
                + $"&number={number}");
        }
        finally
        {
            _navigatingToDetail = false;
        }
    }

    private void ClearCreateForm()
    {
        TitleFieldError.IsVisible = false;
        _viewModel.TitleInput.Value = string.Empty;
        _viewModel.BodyInput.Value = string.Empty;
        _viewModel.LabelsInput.Value = string.Empty;
        _viewModel.ErrorMessage.Value = string.Empty;
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        _ = AppNavigation.GoToAsync("..");
    }

    private void OnOpenSettingsClicked(object? sender, EventArgs e)
    {
        _ = AppNavigation.GoToAsync("//SettingsPage");
    }

    private async void OnCreateClicked(object? sender, EventArgs e)
    {
        var empty = string.IsNullOrWhiteSpace(_viewModel.TitleInput.Value);
        TitleFieldError.IsVisible = empty;
        if (empty)
            return;
        await _viewModel.CreateCommand.ExecuteAsync(null);
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        PageViewModelLifetime.OnParentSet(
            this,
            _viewModel,
            ref _hadParent,
            ref _viewModelDisposed,
            () =>
            {
                _createdSubscription?.Dispose();
                _createdSubscription = null;
            });
    }

    protected override void OnDisappearing()
    {
        _createdSubscription?.Dispose();
        _createdSubscription = null;
        base.OnDisappearing();
    }
}
