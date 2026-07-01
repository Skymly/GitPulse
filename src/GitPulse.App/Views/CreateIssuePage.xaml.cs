using GitPulse.ViewModels;

namespace GitPulse.App.Views;

/// <summary>
/// New issue creation page — receives owner/repo via Shell navigation
/// query parameters. On successful issue creation, navigates to the
/// issue detail page for the newly created issue.
/// </summary>
[QueryProperty("OwnerQuery", "owner")]
[QueryProperty("RepoQuery", "repo")]
public partial class CreateIssuePage : ContentPage
{
    private readonly CreateIssueViewModel _viewModel;
    private bool _initialized;

    public CreateIssuePage(CreateIssueViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    public string OwnerQuery { get; set; } = string.Empty;
    public string RepoQuery { get; set; } = string.Empty;

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_initialized)
        {
            _initialized = true;
            var owner = Uri.UnescapeDataString(OwnerQuery);
            var repo = Uri.UnescapeDataString(RepoQuery);
            if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo))
            {
                _viewModel.Initialize(owner, repo);
            }
        }

        // Check if an issue was created since the last appearance.
        if (_viewModel.CreatedIssueNumber.Value is int number)
        {
            _viewModel.CreatedIssueNumber.Value = null;
            await Shell.Current.GoToAsync(
                $"IssueDetailPage?owner={Uri.EscapeDataString(Uri.UnescapeDataString(OwnerQuery))}"
                + $"&repo={Uri.EscapeDataString(Uri.UnescapeDataString(RepoQuery))}"
                + $"&number={number}");
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
