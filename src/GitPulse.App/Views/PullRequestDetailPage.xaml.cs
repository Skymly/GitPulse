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
/// </summary>
[QueryProperty("OwnerQuery", "owner")]
[QueryProperty("RepoQuery", "repo")]
[QueryProperty("NumberQuery", "number")]
public partial class PullRequestDetailPage : ContentPage
{
    private readonly PullRequestDetailViewModel _viewModel;
    private readonly PrDiffViewModel _diffViewModel;
    private bool _loaded;
    private bool _diffLoaded;

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
        _ = Shell.Current.GoToAsync("..");
    }

    // ── Tab switching ──────────────────────────────────────────────

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
        var primary = Application.Current?.Resources["Primary"] as Color;
        var gray = Application.Current?.Resources["Gray200"] as Color;

        ConversationSection.IsVisible = tab == "conversation";
        FilesSection.IsVisible = tab == "files";

        ConversationTab.BackgroundColor = tab == "conversation" ? primary : gray;
        ConversationTab.TextColor = tab == "conversation" ? Colors.White : Colors.Black;

        FilesTab.BackgroundColor = tab == "files" ? primary : gray;
        FilesTab.TextColor = tab == "files" ? Colors.White : Colors.Black;
    }

    // ── File expand/collapse ───────────────────────────────────────

    private void OnFileToggled(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string filename)
        {
            // Toggle the visibility oc
            // The diff section is found by name pattern.
            var sectionName = $"Diff_{filename.GetHashCode():x}";
            // Use the parent layout to find and toggle.
            // MAUI doesn't support FindByName with dynamic names, so we
            // use the BindingContext trick: each file card has an
            // IsExpanded flag in a wrapper. For simplicity, toggle via
            // the button's parent.
            if (btn.Parent is VerticalStackLayout card)
            {
                // The diff section is the 2nd child (index 1) after the header.
                if (card.Children.Count > 1 && card.Children[1] is VisualElement diffSection)
                {
                    diffSection.IsVisible = !diffSection.IsVisible;
                    btn.Text = diffSection.IsVisible ? "▼" : "▶";
                }
            }
        }
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
        _viewModel.Dispose();
        _diffViewModel.Dispose();
    }
}
