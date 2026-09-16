using GitPulse.App.Events;
using GitPulse.Core.Models;
using GitPulse.ViewModels;
using R3;

namespace GitPulse.App.Views;

/// <summary>
/// Same-repo Create PR page — receives owner/repo via Shell navigation
/// query parameters. On successful create, pops this page then opens
/// PR detail so Back returns to the list.
/// </summary>
[QueryProperty("OwnerQuery", "owner")]
[QueryProperty("RepoQuery", "repo")]
public partial class CreatePullRequestPage : ContentPage
{
    private readonly CreatePullRequestViewModel _viewModel;
    private IDisposable? _createdSubscription;
    private string? _appliedQuery;
    private bool _navigatingToDetail;

    public CreatePullRequestPage(CreatePullRequestViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    public string OwnerQuery { get; set; } = string.Empty;
    public string RepoQuery { get; set; } = string.Empty;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _createdSubscription ??= _viewModel.CreatedPullRequestNumber
            .Where(n => n is not null)
            .ObserveOnCurrentSynchronizationContext()
            .Subscribe(n => _ = NavigateToCreatedPrAsync(n!.Value));

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
            await _viewModel.LoadBranchesCommand.ExecuteAsync(null);
        }
    }

    private async Task NavigateToCreatedPrAsync(int number)
    {
        if (_navigatingToDetail)
            return;

        _navigatingToDetail = true;
        _viewModel.CreatedPullRequestNumber.Value = null;
        ClearCreateForm();
        var owner = OwnerQuery;
        var repo = RepoQuery;
        RepoListStale.Send(RepoListKind.PullRequests, owner, repo);
        try
        {
            await AppNavigation.PopThenGoToAsync(
                $"PullRequestDetailPage?owner={Uri.EscapeDataString(owner)}"
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
        _viewModel.IsDraft.Value = false;
        _viewModel.ErrorMessage.Value = string.Empty;
    }

    private void OnHeadPickerChanged(object? sender, EventArgs e)
        => ApplyPickerBranch(sender, _viewModel.HeadInput);

    private void OnBasePickerChanged(object? sender, EventArgs e)
        => ApplyPickerBranch(sender, _viewModel.BaseInput);

    private static void ApplyPickerBranch(object? sender, BindableReactiveProperty<string> target)
    {
        if (sender is Picker { SelectedItem: Branch branch })
            target.Value = branch.Name;
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

    protected override void OnDisappearing()
    {
        _createdSubscription?.Dispose();
        _createdSubscription = null;
        base.OnDisappearing();
    }
}
