using GitPulse.App.ViewModels;

namespace GitPulse.App.Views;

/// <summary>
/// Issue detail page — receives owner/repo/number via Shell query parameters.
/// </summary>
[QueryProperty("OwnerQuery", "owner")]
[QueryProperty("RepoQuery", "repo")]
[QueryProperty("NumberQuery", "number")]
public partial class IssueDetailPage : ContentPage
{
    private readonly IssueDetailViewModel _viewModel;
    private bool _loaded;

    public IssueDetailPage(IssueDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Dispose();
    }
}
