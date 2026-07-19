using GitPulse.ViewModels;

namespace GitPulse.App.Views;

/// <summary>
/// Workflow run detail — receives owner/repo/runId via Shell query parameters.
/// </summary>
[QueryProperty(nameof(OwnerQuery), "owner")]
[QueryProperty(nameof(RepoQuery), "repo")]
[QueryProperty(nameof(RunIdQuery), "runId")]
public partial class WorkflowRunDetailPage : ContentPage
{
    private readonly WorkflowRunDetailViewModel _viewModel;
    private bool _loaded;

    public WorkflowRunDetailPage(WorkflowRunDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    public string OwnerQuery { get; set; } = string.Empty;
    public string RepoQuery { get; set; } = string.Empty;
    public string RunIdQuery { get; set; } = string.Empty;

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_loaded)
        {
            _loaded = true;
            var owner = Uri.UnescapeDataString(OwnerQuery);
            var repo = Uri.UnescapeDataString(RepoQuery);
            if (long.TryParse(RunIdQuery, out var runId)
                && !string.IsNullOrEmpty(owner)
                && !string.IsNullOrEmpty(repo))
            {
                _viewModel.Initialize(owner, repo, runId);
                _ = _viewModel.LoadCommand.ExecuteAsync(null);
            }
        }
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        _ = Shell.Current.GoToAsync("..");
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Dispose();
    }
}
