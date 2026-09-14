using GitPulse.App.Events;
using GitPulse.Core.Models;
using GitPulse.ViewModels;
using R3;

namespace GitPulse.App.Views;

/// <summary>
/// Repository commit list — receives owner/repo via Shell query parameters.
/// </summary>
[QueryProperty(nameof(OwnerQuery), "owner")]
[QueryProperty(nameof(RepoQuery), "repo")]
public partial class CommitsPage : ContentPage
{
    private readonly CommitsViewModel _viewModel;
    private readonly CompositeDisposable _events = [];
    private string? _appliedQuery;

    public CommitsPage(CommitsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        _events.Add(UiEventPipelines.BindLoadMore(
            CommitsList,
            _viewModel.CanLoadMore,
            () => _viewModel.LoadMoreCommand.ExecuteAsync(null)));
    }

    public string OwnerQuery { get; set; } = string.Empty;
    public string RepoQuery { get; set; } = string.Empty;

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var owner = Uri.UnescapeDataString(OwnerQuery);
        var repo = Uri.UnescapeDataString(RepoQuery);
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

    private void OnCommitTapped(object? sender, TappedEventArgs e)
    {
        var commit = e.Parameter as GitCommit
            ?? (sender as Element)?.BindingContext as GitCommit;
        if (commit is null || string.IsNullOrEmpty(commit.Sha))
            return;

        _ = AppNavigation.GoToAsync(
            $"CommitDetailPage?owner={Uri.EscapeDataString(_viewModel.Owner.Value)}"
            + $"&repo={Uri.EscapeDataString(_viewModel.RepoName.Value)}"
            + $"&sha={Uri.EscapeDataString(commit.Sha)}");
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        _ = AppNavigation.GoToAsync("..");
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }
}
