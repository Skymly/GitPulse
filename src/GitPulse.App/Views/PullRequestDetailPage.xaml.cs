using GitPulse.Core.Models;
using GitPulse.ViewModels;

namespace GitPulse.App.Views;

/// <summary>
/// Pull request detail page — receives owner/repo/number via Shell query
/// parameters. Shows the PR with two tabs:
/// <list type="bullet">
/// <item><b>Conversation</b>: PR description, merge controls, and issue comments.</item>
/// <item><b>Files</b>: Changed files with diff rendering (WebView) and inline
/// review comments. Supports posting new comments and replies.</item>
/// </list>
/// Below 840px, Files is a list that pushes a stacked diff (GitHub Mobile);
/// at or above 840px it is a list|diff split.
/// </summary>
[QueryProperty("OwnerQuery", "owner")]
[QueryProperty("RepoQuery", "repo")]
[QueryProperty("NumberQuery", "number")]
public partial class PullRequestDetailPage : ContentPage
{
    private const double WideBreakpoint = 840;
    private readonly PullRequestDetailViewModel _viewModel;
    private readonly PrDiffViewModel _diffViewModel;
    private bool _loaded;
    private bool _diffLoaded;
    private bool _filesStacked;
    private bool _showingStackedDiff;
    private bool _filesLayoutReady;
    private bool _conversationWide;
    private bool _conversationLayoutReady;
    private double _lastWidth;

    public static readonly BindableProperty SelectedDiffFileProperty =
        BindableProperty.Create(
            nameof(SelectedDiffFile),
            typeof(DiffEntry),
            typeof(PullRequestDetailPage));

    public DiffEntry? SelectedDiffFile
    {
        get => (DiffEntry?)GetValue(SelectedDiffFileProperty);
        set => SetValue(SelectedDiffFileProperty, value);
    }

    public PrDiffViewModel DiffViewModel => _diffViewModel;

    public PullRequestDetailPage(PullRequestDetailViewModel viewModel, PrDiffViewModel diffViewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _diffViewModel = diffViewModel;
        BindingContext = _viewModel;
    }

    public string OwnerQuery { get; set; } = string.Empty;
    public string RepoQuery { get; set; } = string.Empty;
    public string NumberQuery { get; set; } = string.Empty;

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || Math.Abs(width - _lastWidth) < 0.5)
            return;
        _lastWidth = width;
        var wide = width >= WideBreakpoint;
        ApplyFilesLayout(wide);
        ApplyConversationLayout(wide);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_loaded)
        {
            _loaded = true;
            var owner = Uri.UnescapeDataString(OwnerQuery);
            var repo = Uri.UnescapeDataString(RepoQuery);
            if (int.TryParse(NumberQuery, out var number))
            {
                _viewModel.Initialize(owner, repo, number);
                _ = _viewModel.LoadCommand.ExecuteAsync(null);
            }
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
        if (!FilesSection.IsVisible || !_showingStackedDiff)
            return false;
        ShowStackedList();
        return true;
    }

    private async void OnMergeClicked(object? sender, EventArgs e)
    {
        var pr = _viewModel.PullRequest.Value;
        if (pr is null)
            return;

        // Cancel is the accept/default so Enter does not merge.
        var cancelled = await DisplayAlertAsync(
            "Merge pull request?",
            $"#{pr.Number} {pr.Title}\nMethod: {_viewModel.MergeMethod.Value}",
            "Cancel",
            "Merge");
        if (!cancelled)
            await _viewModel.MergeCommand.ExecuteAsync(null);
    }

    private void OnConversationTabClicked(object? sender, EventArgs e)
    {
        ShowTab("conversation");
    }

    private void OnFilesTabClicked(object? sender, EventArgs e)
    {
        ShowTab("files");

        // Lazy-load diff data on first Files tab activation.
        if (!_diffLoaded && _viewModel.PullRequest.Value is not null)
        {
            _diffLoaded = true;
            var pr = _viewModel.PullRequest.Value;
            var headSha = pr.Head?.Sha ?? string.Empty;
            _diffViewModel.Initialize(
                _viewModel.Owner.Value,
                _viewModel.RepoName.Value,
                pr.Number,
                headSha);
            _ = _diffViewModel.LoadCommand.ExecuteAsync(null);
        }
    }

    private void ShowTab(string tab)
    {
        ConversationSection.IsVisible = tab == "conversation";
        FilesSection.IsVisible = tab == "files";

        ChromeTabs.Style(ConversationTab, tab == "conversation");
        ChromeTabs.Style(FilesTab, tab == "files");
    }

    private void ApplyConversationLayout(bool wide)
    {
        if (wide == _conversationWide && _conversationLayoutReady)
            return;

        _conversationWide = wide;
        _conversationLayoutReady = true;

        if (wide)
        {
            ConversationBody.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(new GridLength(1, GridUnitType.Star)),
                new(320),
            };
            ConversationBody.RowDefinitions = new RowDefinitionCollection { new(GridLength.Auto) };
            Grid.SetRow(ConversationMain, 0);
            Grid.SetColumn(ConversationMain, 0);
            Grid.SetRow(ConversationRail, 0);
            Grid.SetColumn(ConversationRail, 1);
            return;
        }

        ConversationBody.ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star) };
        ConversationBody.RowDefinitions = new RowDefinitionCollection
        {
            new(GridLength.Auto),
            new(GridLength.Auto),
        };
        Grid.SetRow(ConversationMain, 0);
        Grid.SetColumn(ConversationMain, 0);
        Grid.SetRow(ConversationRail, 1);
        Grid.SetColumn(ConversationRail, 0);
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
            FilesSection.RowDefinitions = new RowDefinitionCollection { new(GridLength.Star) };
            Grid.SetRow(FilesList, 0);
            Grid.SetColumn(FilesList, 0);
            Grid.SetColumnSpan(FilesList, 1);
            Grid.SetRow(DiffPane, 0);
            Grid.SetColumn(DiffPane, 1);
            Grid.SetColumnSpan(DiffPane, 1);
            Grid.SetColumn(FilesEmpty, 0);
            Grid.SetColumnSpan(FilesEmpty, 2);
            return;
        }

        FilesSection.ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star) };
        FilesSection.RowDefinitions = new RowDefinitionCollection { new(GridLength.Star) };
        Grid.SetRow(FilesList, 0);
        Grid.SetColumn(FilesList, 0);
        Grid.SetColumnSpan(FilesList, 1);
        Grid.SetRow(DiffPane, 0);
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

    private void OnCommentClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string filePath)
        {
            // Start a new comment on this file (line 0 = file-level comment).
            _diffViewModel.StartCommentCommand.Execute(new CommentTarget(filePath, 0));
        }
    }

    private void OnReplyClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is long commentId)
        {
            _diffViewModel.StartReplyCommand.Execute(commentId);
        }
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

        var owner = Uri.EscapeDataString(_viewModel.Owner.Value);
        var repo = Uri.EscapeDataString(_viewModel.RepoName.Value);
        await AppNavigation.GoToAsync(
            $"CheckRunDetailPage?owner={owner}&repo={repo}&checkRunId={run.Id}");
    }
}
