using GitPulse.Core.Models;
using GitPulse.ViewModels;

namespace GitPulse.App.Views;

/// <summary>
/// Notifications page — the M4 Events domain showcase.
/// The page starts the notification poller on appear and stops it on
/// disappear. The poller fires <c>NotificationsUpdated</c> on each timer
/// tick, and the ViewModel bridges that to R3 reactive state.
/// </summary>
public partial class NotificationsPage : ContentPage
{
    private readonly NotificationsViewModel _viewModel;

    public NotificationsPage(NotificationsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Start polling when the page is visible.
        _viewModel.StartPollingCommand.Execute(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Stop polling when the page is not visible (conserves API rate limit).
        _viewModel.StopPollingCommand.Execute(null);
        // ViewModels are transient; dispose to release event subscriptions.
        _viewModel.Dispose();
    }

    private async void OnNotificationSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Notification notification)
        {
            ((CollectionView)sender!).SelectedItem = null;

            // Open the notification subject in the browser.
            var url = notification.Subject.LatestCommentUrl ?? notification.Repository.HtmlUrl;
            if (!string.IsNullOrEmpty(url))
                await _viewModel.OpenInBrowserCommand.ExecuteAsync(url);
        }
    }
}
