using GitPulse.App.Events;
using GitPulse.Core.Models;
using GitPulse.ViewModels;
using R3;

namespace GitPulse.App.Views;

/// <summary>
/// Workflow runs list — receives owner/repo via Shell query parameters.
/// </summary>
[QueryProperty(nameof(OwnerQuery), "owner")]
[QueryProperty(nameof(RepoQuery), "repo")]
public partial class WorkflowRunsPage : ContentPage
{
    private readonly WorkflowRunsViewModel _viewModel;
    private readonly CompositeDisposable _events = [];
    private string? _appliedQuery;
    private bool _hadParent;
    private bool _viewModelDisposed;

    public WorkflowRunsPage(WorkflowRunsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        _events.Add(UiEventPipelines.BindLoadMore(
            RunsList,
            _viewModel.CanLoadMore,
            () => _viewModel.LoadMoreCommand.ExecuteAsync(null)));
    }

    public string OwnerQuery { get; set; } = string.Empty;
    public string RepoQuery { get; set; } = string.Empty;

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var owner = OwnerQuery;
        var repo = RepoQuery;
        var query = $"{owner}/{repo}";
        if (_appliedQuery == query)
            return;

        _appliedQuery = query;
        if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo))
        {
            _viewModel.Initialize(owner, repo);
            _ = _viewModel.LoadCommand.ExecuteAsync(null);
        }
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        _ = AppNavigation.GoToAsync("..");
    }

    private void OnOpenSettingsClicked(object? sender, EventArgs e)
    {
        _ = AppNavigation.GoToAsync("//SettingsPage");
    }

    private void OnRunSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not WorkflowRun run)
            return;

        ((CollectionView)sender!).SelectedItem = null;
        _ = AppNavigation.GoToAsync(
            $"WorkflowRunDetailPage?owner={Uri.EscapeDataString(_viewModel.Owner.Value)}"
            + $"&repo={Uri.EscapeDataString(_viewModel.RepoName.Value)}"
            + $"&runId={run.Id}");
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        PageViewModelLifetime.OnParentSet(
            this,
            _viewModel,
            ref _hadParent,
            ref _viewModelDisposed,
            () => _events.Dispose());
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Do not dispose here: disappear is tab switch, push, or peek.
        // ViewModel Dispose is Page ViewModel lifetime (leave the back stack).
    }
}
