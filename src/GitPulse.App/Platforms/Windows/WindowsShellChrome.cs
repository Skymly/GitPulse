using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinUINavigationView = Microsoft.UI.Xaml.Controls.NavigationView;
using WinUINavigationViewItem = Microsoft.UI.Xaml.Controls.NavigationViewItem;

namespace GitPulse.App.Platforms.Windows;

/// <summary>
/// Fluent top NavigationView for Shell destinations: PaneDisplayMode.Top,
/// Accent selection underline, and an InfoBadge on Notifications.
/// </summary>
internal static class WindowsShellChrome
{
    const string NotificationsTitle = "Notifications";
    static int _unread;

    public static void Apply(Shell shell)
    {
        void Configure()
        {
            var nav = FindNavigationView(shell);
            if (nav is null)
                return;

            nav.PaneDisplayMode = NavigationViewPaneDisplayMode.Top;
            nav.IsSettingsVisible = false;
            nav.IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed;
            nav.IsPaneToggleButtonVisible = false;
            nav.AlwaysShowHeader = false;
            SetUnreadBadge(shell, _unread);
        }

        shell.HandlerChanged += (_, _) => Configure();
        shell.Loaded += (_, _) => Configure();
        if (shell.Handler is not null)
            Configure();
    }

    public static void SetUnreadBadge(Shell shell, int unread)
    {
        _unread = unread;
        var nav = FindNavigationView(shell);
        if (nav is null)
            return;

        foreach (var item in EnumerateItems(nav))
        {
            if (!IsNotifications(item))
                continue;

            item.InfoBadge = unread <= 0
                ? null
                : new InfoBadge { Value = unread > 99 ? 99 : unread };
            return;
        }
    }

    static WinUINavigationView? FindNavigationView(Shell shell)
    {
        if (shell.Handler?.PlatformView is WinUINavigationView nav)
            return nav;
        if (shell.Handler?.PlatformView is DependencyObject root)
            return FindDescendant<WinUINavigationView>(root);
        return null;
    }

    static IEnumerable<WinUINavigationViewItem> EnumerateItems(WinUINavigationView nav)
    {
        foreach (var obj in nav.MenuItems)
        {
            if (obj is WinUINavigationViewItem item)
                yield return item;
        }

        if (nav.MenuItemsSource is System.Collections.IEnumerable source)
        {
            foreach (var obj in source)
            {
                if (obj is WinUINavigationViewItem item)
                    yield return item;
            }
        }
    }

    static bool IsNotifications(WinUINavigationViewItem item)
    {
        var content = item.Content?.ToString() ?? string.Empty;
        return content.StartsWith(NotificationsTitle, StringComparison.OrdinalIgnoreCase);
    }

    static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                return match;
            var nested = FindDescendant<T>(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }
}
