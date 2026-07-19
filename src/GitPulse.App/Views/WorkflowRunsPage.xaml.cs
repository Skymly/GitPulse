using GitPulse.Core.Models;
using GitPulse.ViewModels;

namespace GitPulse.App.Views;

/// <summary>
/// Workflow runs list — receives owner/repo via Shell query parameters.
/// </summary>
[QueryProperty(nameof(OwnerQuery), "owner")]
[QueryProperty(nameof(RepoQuery), "repo")]
public partial class WorkflowRunsPage : ContentPage
{
    private readonly WorkflowRunsViewModel _viewModel;
    private bool _loaded;

    public WorkflowRunsPage(WorkflowRunsViewModel viewModel)
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

        if (!_loaded)
        {
            _loaded = true;
            var owner = Uri.UnescapeDataString(OwnerQuery);
            var repo = Uri.UnescapeDataString(RepoQuery);
            if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo))
            {
                _viewModel.Initialize(owner, repo);
                _ = _viewModel.LoadCommand.ExecuteAsync(null);
            }
        }
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        _ = Shell.Current.GoToAsync("..");
    }

    private void OnRunSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not WorkflowRun run)
            return;

        ((CollectionView)sender!).SelectedItem = null;
        _ = Shell.Current.GoToAsync(
            $"WorkflowRunDetailPage?owner={Uri.EscapeDataString(_viewModel.Owner.Value)}"
            + $"&repo={Uri.EscapeDataString(_viewModel.RepoName.Value)}"
            + $"&runId={run.Id}");
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Dispose();
    }
}
