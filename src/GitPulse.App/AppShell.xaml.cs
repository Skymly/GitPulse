using GitPulse.Core.Abstractions;
using GitPulse.Core.Models;
#if WINDOWS
using GitPulse.App.Platforms.Windows;
#elif ANDROID
using GitPulse.App.Platforms.Android;
#endif

namespace GitPulse.App;

public partial class AppShell : Shell
{
    private readonly INotificationPoller _poller;

    public AppShell(INotificationPoller poller)
    {
        _poller = poller;
        InitializeComponent();
#if WINDOWS
        FlyoutBehavior = FlyoutBehavior.Disabled;
        Shell.SetNavBarIsVisible(this, false);
        WindowsShellChrome.Apply(this);
#elif ANDROID
        AndroidShellChrome.Apply(this);
#endif
        _poller.NotificationsUpdated += OnNotificationsUpdated;
        Navigated += OnShellNavigated;
        UpdateTabIcons();
        UpdateUnreadBadge(_poller.UnreadCount);
    }

    private void OnNotificationsUpdated(Notification[] notifications, int unreadCount)
        => Dispatcher.Dispatch(() => UpdateUnreadBadge(unreadCount));

    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
        => UpdateChrome();

    private void UpdateChrome()
    {
        UpdateTabIcons();
        var current = CurrentItem?.CurrentItem?.CurrentItem;
        var onTab = ReferenceEquals(current, TabRepos)
            || ReferenceEquals(current, TabNotifications)
            || ReferenceEquals(current, TabSearch)
            || ReferenceEquals(current, TabSettings);
        if (CurrentPage is not null)
            Shell.SetTabBarIsVisible(CurrentPage, onTab);
    }

    private void UpdateUnreadBadge(int unread)
    {
#if WINDOWS
        TabNotifications.Title = "Notifications";
        WindowsShellChrome.SetUnreadBadge(this, unread);
#elif ANDROID
        TabNotifications.Title = "Notifications";
        AndroidShellChrome.SetUnreadBadge(this, unread);
#else
        TabNotifications.Title = unread <= 0
            ? "Notifications"
            : unread > 99 ? "Notifications 99+" : $"Notifications ({unread})";
#endif
    }

    private void UpdateTabIcons()
    {
        var current = CurrentItem?.CurrentItem?.CurrentItem;
        TabRepos.Icon = ReferenceEquals(current, TabRepos) ? "tab_repos_filled.png" : "tab_repos.png";
        TabNotifications.Icon = ReferenceEquals(current, TabNotifications)
            ? "tab_notifications_filled.png"
            : "tab_notifications.png";
        TabSearch.Icon = ReferenceEquals(current, TabSearch) ? "tab_search_filled.png" : "tab_search.png";
        TabSettings.Icon = ReferenceEquals(current, TabSettings)
            ? "tab_settings_filled.png"
            : "tab_settings.png";
    }
}
