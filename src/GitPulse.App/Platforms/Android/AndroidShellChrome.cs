using AndroidView = Android.Views.View;
using Google.Android.Material.BottomNavigation;
using ViewGroup = Android.Views.ViewGroup;

namespace GitPulse.App.Platforms.Android;

/// <summary>
/// Material badge on the Notifications Shell tab (GitHub Mobile unread chrome).
/// </summary>
internal static class AndroidShellChrome
{
    const int NotificationsTabIndex = 1;
    static int _unread;

    public static void Apply(Shell shell)
    {
        void Configure() => SetUnreadBadge(shell, _unread);

        shell.HandlerChanged += (_, _) => Configure();
        shell.Loaded += (_, _) => Configure();
        shell.Navigated += (_, _) => Configure();
        if (shell.Handler is not null)
            Configure();
    }

    public static void SetUnreadBadge(Shell shell, int unread)
    {
        _unread = unread;
        if (shell.Handler?.PlatformView is not AndroidView root)
            return;

        var bottom = FindBottomNav(root);
        if (bottom is null || bottom.Menu.Size() <= NotificationsTabIndex)
            return;

        var id = bottom.Menu.GetItem(NotificationsTabIndex)!.ItemId;
        if (unread <= 0)
        {
            bottom.RemoveBadge(id);
            return;
        }

        var badge = bottom.GetOrCreateBadge(id);
        badge.MaxNumber = 99;
        badge.Number = unread;
        badge.BackgroundColor = new global::Android.Graphics.Color(0x00, 0x78, 0xD4);
        badge.BadgeTextColor = global::Android.Graphics.Color.White;
        badge.SetVisible(true);
    }

    static BottomNavigationView? FindBottomNav(AndroidView? root)
    {
        if (root is BottomNavigationView nav)
            return nav;
        if (root is not ViewGroup group)
            return null;

        for (var i = 0; i < group.ChildCount; i++)
        {
            var found = FindBottomNav(group.GetChildAt(i));
            if (found is not null)
                return found;
        }

        return null;
    }
}
