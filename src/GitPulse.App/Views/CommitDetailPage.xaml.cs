using GitPulse.Core.Models;
using GitPulse.ViewModels;

namespace GitPulse.App.Views;

/// <summary>
/// In-app Git Commit — receives owner/repo/sha via Shell query parameters.
/// Files inherit list-then-diff: stacked below 840px, list|diff split at or above.
/// </summary>
[QueryProperty(nameof(OwnerQuery), "owner")]
[QueryProperty(nameof(RepoQuery), "repo")]
[QueryProperty(nameof(ShaQuery), "sha")]
public partial class CommitDetailPage : ContentPage
{
    private const double WideBreakpoint = 840;
    private readonly CommitDetailViewModel _viewModel;
    private string? _appliedQuery;
    private bool _filesStacked;
    private bool _showingStackedDiff;
    private bool _filesLayoutReady;
    private double _lastWidth;

    public static readonly BindableProperty SelectedDiffFileProperty =
        BindableProperty.Create(
            nameof(SelectedDiffFile),
            typeof(DiffEntry),
            typeof(CommitDetailPage));

    public DiffEntry? SelectedDiffFile
    {
        get => (DiffEntry?)GetValue(SelectedDiffFileProperty);
        set => SetValue(SelectedDiffFileProperty, value);
    }

    public CommitDetailPage(CommitDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    public string OwnerQuery { get; set; } = string.Empty;
    public string RepoQuery { get; set; } = string.Empty;
    public string ShaQuery { get; set; } = string.Empty;

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || Math.Abs(width - _lastWidth) < 0.5)
            return;
        _lastWidth = width;
        ApplyFilesLayout(width >= WideBreakpoint);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var owner = OwnerQuery;
        var repo = RepoQuery;
        var sha = ShaQuery;
        var query = $"{owner}/{repo}/{sha}";
        if (_appliedQuery == query)
            return;

        _appliedQuery = query;
        if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo) && !string.IsNullOrEmpty(sha))
        {
            _viewModel.Initialize(owner, repo, sha);
            _ = _viewModel.LoadCommand.ExecuteAsync(null);
        }
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        if (LeaveStackedDiff())
            return;
        LeavePage();
    }

    private void OnLeavePageClicked(object? sender, EventArgs e) => LeavePage();

    private void OnBackToFilesClicked(object? sender, EventArgs e) => ShowStackedList();

    protected override bool OnBackButtonPressed()
        => LeaveStackedDiff() || base.OnBackButtonPressed();

    private void LeavePage() => _ = AppNavigation.GoToAsync("..");

    private bool LeaveStackedDiff()
    {
        if (!_showingStackedDiff)
            return false;
        ShowStackedList();
        return true;
    }

    private void ApplyFilesLayout(bool wide)
    {
        var stacked = !wide;
        if (stacked == _filesStacked && _filesLayoutReady)
            return;

        _filesStacked = stacked;
        _filesLayoutReady = true;

        if (wide)
        {
            _showingStackedDiff = false;
            BackToFilesButton.IsVisible = false;
            FilesList.IsVisible = true;
            DiffPane.IsVisible = true;
            FilesSection.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(new GridLength(0.38, GridUnitType.Star)),
                new(new GridLength(0.62, GridUnitType.Star)),
            };
            Grid.SetColumn(FilesList, 0);
            Grid.SetColumnSpan(FilesList, 1);
            Grid.SetColumn(DiffPane, 1);
            Grid.SetColumnSpan(DiffPane, 1);
            Grid.SetColumn(FilesEmpty, 0);
            Grid.SetColumnSpan(FilesEmpty, 2);
            return;
        }

        FilesSection.ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star) };
        Grid.SetColumn(FilesList, 0);
        Grid.SetColumnSpan(FilesList, 1);
        Grid.SetColumn(DiffPane, 0);
        Grid.SetColumnSpan(DiffPane, 1);
        Grid.SetColumn(FilesEmpty, 0);
        Grid.SetColumnSpan(FilesEmpty, 1);
        if (SelectedDiffFile is not null)
            ShowStackedDiff();
        else
            ShowStackedList();
    }

    private void ShowStackedList()
    {
        _showingStackedDiff = false;
        FilesList.IsVisible = true;
        DiffPane.IsVisible = false;
        BackToFilesButton.IsVisible = false;
    }

    private void ShowStackedDiff()
    {
        _showingStackedDiff = true;
        FilesList.IsVisible = false;
        DiffPane.IsVisible = true;
        BackToFilesButton.IsVisible = true;
    }

    private void OnFileSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not DiffEntry file)
            return;

        SelectedDiffFile = file;
        if (_filesStacked)
            ShowStackedDiff();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Keep ViewModel(s) alive: pages stay on the navigation stack and are reused on pop.
    }

    private async void OnCheckRunOpened(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: CheckRun run } || run.Id <= 0)
            return;

        await AppNavigation.GoToAsync(
            $"CheckRunDetailPage?owner={Uri.EscapeDataString(OwnerQuery)}"
            + $"&repo={Uri.EscapeDataString(RepoQuery)}"
            + $"&checkRunId={run.Id}");
    }
}
