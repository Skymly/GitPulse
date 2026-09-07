using GitPulse.ViewModels;

namespace GitPulse.App.Views;

/// <summary>
/// Issue detail page — receives owner/repo/number via Shell query parameters.
/// At or above 840px, labels/assignees sit in a rail; below 840px they stack
/// under the conversation (GitHub Mobile).
/// </summary>
[QueryProperty("OwnerQuery", "owner")]
[QueryProperty("RepoQuery", "repo")]
[QueryProperty("NumberQuery", "number")]
public partial class IssueDetailPage : ContentPage
{
    private const double WideBreakpoint = 840;
    private readonly IssueDetailViewModel _viewModel;
    private bool _loaded;
    private bool _conversationWide;
    private bool _conversationLayoutReady;
    private double _lastWidth;

    public IssueDetailPage(IssueDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
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
        ApplyConversationLayout(width >= WideBreakpoint);
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

    private void ApplyConversationLayout(bool wide)
    {
        if (wide == _conversationWide && _conversationLayoutReady)
            return;

        _conversationWide = wide;
        _conversationLayoutReady = true;

        if (wide)
        {
            IssueBody.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(new GridLength(1, GridUnitType.Star)),
                new(320),
            };
            IssueBody.RowDefinitions = new RowDefinitionCollection { new(GridLength.Auto) };
            Grid.SetRow(IssueMain, 0);
            Grid.SetColumn(IssueMain, 0);
            Grid.SetRow(IssueRail, 0);
            Grid.SetColumn(IssueRail, 1);
            return;
        }

        IssueBody.ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star) };
        IssueBody.RowDefinitions = new RowDefinitionCollection
        {
            new(GridLength.Auto),
            new(GridLength.Auto),
        };
        Grid.SetRow(IssueMain, 0);
        Grid.SetColumn(IssueMain, 0);
        Grid.SetRow(IssueRail, 1);
        Grid.SetColumn(IssueRail, 0);
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        _ = AppNavigation.GoToAsync("..");
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Keep ViewModel(s) alive: pages stay on the navigation stack and are reused on pop.
    }
}
