using GitPulse.Core.Models;
using GitPulse.ViewModels;

namespace GitPulse.App.Views;

[QueryProperty(nameof(OwnerQuery), "owner")]
[QueryProperty(nameof(RepoQuery), "repo")]
[QueryProperty(nameof(CheckRunIdQuery), "checkRunId")]
public partial class CheckRunDetailPage : ContentPage
{
    private readonly CheckRunDetailViewModel _viewModel;
    private string? _appliedQuery;
    private bool _hadParent;
    private bool _viewModelDisposed;

    public CheckRunDetailPage(CheckRunDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    public string OwnerQuery { get; set; } = string.Empty;
    public string RepoQuery { get; set; } = string.Empty;
    public string CheckRunIdQuery { get; set; } = string.Empty;

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var owner = OwnerQuery;
        var repo = RepoQuery;
        var checkRunId = CheckRunIdQuery;
        var query = $"{owner}/{repo}/{checkRunId}";
        if (_appliedQuery == query)
            return;

        _appliedQuery = query;
        if (long.TryParse(checkRunId, out var id)
            && !string.IsNullOrEmpty(owner)
            && !string.IsNullOrEmpty(repo)
            && id > 0)
        {
            _viewModel.Initialize(owner, repo, id);
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

    private async void OnAnnotationTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not BindableObject bindable
            || bindable.BindingContext is not CheckRunAnnotation annotation
            || string.IsNullOrEmpty(annotation.Path))
        {
            return;
        }

        var headSha = _viewModel.CheckRun.Value?.HeadSha;
        if (string.IsNullOrEmpty(headSha))
            return;

        var owner = OwnerQuery;
        var repo = RepoQuery;
        await AppNavigation.GoToAsync(
            $"FileEditorPage?owner={Uri.EscapeDataString(owner)}"
            + $"&repo={Uri.EscapeDataString(repo)}"
            + $"&path={Uri.EscapeDataString(annotation.Path)}"
            + $"&ref={Uri.EscapeDataString(headSha)}");
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        PageViewModelLifetime.OnParentSet(
            this,
            _viewModel,
            ref _hadParent,
            ref _viewModelDisposed);
    }

}
