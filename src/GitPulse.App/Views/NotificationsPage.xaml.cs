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
        PullToRefresh.WindowsOff(ListRefresh);
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
        if (e.CurrentSelection.FirstOrDefault() is not Notification notification)
            return;

        ((CollectionView)sender!).SelectedItem = null;

        if (TryGetInAppRoute(notification, out var route))
        {
            await AppNavigation.GoToAsync(route);
            return;
        }

        var url = notification.Subject.LatestCommentUrl ?? notification.Repository.HtmlUrl;
        if (!string.IsNullOrEmpty(url))
            await _viewModel.OpenInBrowserCommand.ExecuteAsync(url);
    }

    private static bool TryGetInAppRoute(Notification notification, out string route)
    {
        route = string.Empty;
        var parts = notification.Repository.FullName.Split('/', 2);
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            return false;

        var owner = Uri.EscapeDataString(parts[0]);
        var repo = Uri.EscapeDataString(parts[1]);
        var type = notification.Subject.Type;
        var tail = notification.Subject.Url.TrimEnd('/').Split('/')[^1];
        if (string.IsNullOrEmpty(tail))
            return false;

        if (type.Equals("Issue", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(tail, out var issueNumber))
        {
            route = $"IssueDetailPage?owner={owner}&repo={repo}&number={issueNumber}";
            return true;
        }

        if (type.Equals("PullRequest", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(tail, out var prNumber))
        {
            route = $"PullRequestDetailPage?owner={owner}&repo={repo}&number={prNumber}";
            return true;
        }

        if (type.Equals("Commit", StringComparison.OrdinalIgnoreCase)
            && tail.Length >= 7)
        {
            route = $"CommitDetailPage?owner={owner}&repo={repo}&sha={Uri.EscapeDataString(tail)}";
            return true;
        }

        return false;
    }

    private void OnOpenSettingsClicked(object? sender, EventArgs e)
    {
        _ = AppNavigation.GoToAsync("//SettingsPage");
    }
}
