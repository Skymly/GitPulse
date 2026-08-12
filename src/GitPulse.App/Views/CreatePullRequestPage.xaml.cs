using GitPulse.Core.Models;
using GitPulse.ViewModels;
using R3;

namespace GitPulse.App.Views;

/// <summary>
/// Same-repo Create PR page — receives owner/repo via Shell navigation
/// query parameters. On successful create, navigates to PR detail.
/// </summary>
[QueryProperty("OwnerQuery", "owner")]
[QueryProperty("RepoQuery", "repo")]
public partial class CreatePullRequestPage : ContentPage
{
    private readonly CreatePullRequestViewModel _viewModel;
    private readonly IDisposable _createdSubscription;
    private bool _initialized;
    private bool _navigatingToDetail;

    public CreatePullRequestPage(CreatePullRequestViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        // CreateIssuePage only checks success in OnAppearing, which does not
        // re-fire after create on the same page. Subscribe so success navigates.
        _createdSubscription = _viewModel.CreatedPullRequestNumber
            .Where(n => n is not null)
            .ObserveOnCurrentSynchronizationContext()
            .Subscribe(n => _ = NavigateToCreatedPrAsync(n!.Value));
    }

    public string OwnerQuery { get; set; } = string.Empty;
    public string RepoQuery { get; set; } = string.Empty;

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_initialized)
        {
            _initialized = true;
            var owner = Uri.UnescapeDataString(OwnerQuery);
            var repo = Uri.UnescapeDataString(RepoQuery);
            if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo))
            {
                _viewModel.Initialize(owner, repo);
                await _viewModel.LoadBranchesCommand.ExecuteAsync(null);
            }
        }
    }

    private async Task NavigateToCreatedPrAsync(int number)
    {
        if (_navigatingToDetail)
            return;

        _navigatingToDetail = true;
        _viewModel.CreatedPullRequestNumber.Value = null;
        try
        {
            await AppNavigation.GoToAsync(
                $"PullRequestDetailPage?owner={Uri.EscapeDataString(Uri.UnescapeDataString(OwnerQuery))}"
                + $"&repo={Uri.EscapeDataString(Uri.UnescapeDataString(RepoQuery))}"
                + $"&number={number}");
        }
        finally
        {
            _navigatingToDetail = false;
        }
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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Keep ViewModel alive: pages stay on the navigation stack and are reused on pop.
    }
}
