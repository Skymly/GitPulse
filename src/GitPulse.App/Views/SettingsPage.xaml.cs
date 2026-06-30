using GitPulse.App.ViewModels;

namespace GitPulse.App.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // ViewModels are transient; dispose to release R3 subscriptions.
        _viewModel.Dispose();
    }
}
