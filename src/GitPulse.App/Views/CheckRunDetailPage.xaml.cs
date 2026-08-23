using GitPulse.ViewModels;

namespace GitPulse.App.Views;

[QueryProperty(nameof(OwnerQuery), "owner")]
[QueryProperty(nameof(RepoQuery), "repo")]
[QueryProperty(nameof(CheckRunIdQuery), "checkRunId")]
public partial class CheckRunDetailPage : ContentPage
{
    private readonly CheckRunDetailViewModel _viewModel;
    private bool _loaded;

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

        if (_loaded)
            return;

        _loaded = true;
        var owner = Uri.UnescapeDataString(OwnerQuery);
        var repo = Uri.UnescapeDataString(RepoQuery);
        if (long.TryParse(Uri.UnescapeDataString(CheckRunIdQuery), out var id)
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
}
