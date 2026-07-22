using GitPulse.Core.Models;
using GitPulse.ViewModels;

namespace GitPulse.App.Views;

/// <summary>
/// Notifications page — the M4 Events domain showcase.
/// The Notification Poller is owned by <c>NotificationToastHost</c> for the
/// process lifetime (ADR-010: continue while in Tray Presence; stop on Exit).
/// This page ensures polling is started on appear and bridges updates via the
/// ViewModel; it does not stop the poller on disappear.
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
        // Idempotent: host already starts the poller; this covers early navigation.
        _viewModel.StartPollingCommand.Execute(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Do not stop the poller (ADR-010 Tray Presence) and do not dispose the
        // ViewModel: Shell tab pages are reused; toast/tray navigation expects a
        // live BindingContext when returning to Notifications.
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
