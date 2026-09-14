using GitPulse.App.Views;
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
    private static bool s_detailRoutesRegistered;

    static AppShell() => RegisterDetailRoutes();

    public AppShell(INotificationPoller poller)
    {
        _poller = poller;
        InitializeComponent();
        FlyoutBehavior = FlyoutBehavior.Disabled;
        Shell.SetNavBarIsVisible(this, false);
#if WINDOWS
        WindowsShellChrome.Apply(this);
#elif ANDROID
        AndroidShellChrome.Apply(this);
#endif
        _poller.NotificationsUpdated += OnNotificationsUpdated;
        Navigated += OnShellNavigated;
        UpdateTabIcons();
        UpdateUnreadBadge(_poller.UnreadCount);
    }

    static void RegisterDetailRoutes()
    {
        if (s_detailRoutesRegistered)
            return;

        s_detailRoutesRegistered = true;
        Routing.RegisterRoute(nameof(RepoDetailPage), typeof(RepoDetailPage));
        Routing.RegisterRoute(nameof(IssuesPage), typeof(IssuesPage));
        Routing.RegisterRoute(nameof(IssueDetailPage), typeof(IssueDetailPage));
        Routing.RegisterRoute(nameof(CreateIssuePage), typeof(CreateIssuePage));
        Routing.RegisterRoute(nameof(PullRequestsPage), typeof(PullRequestsPage));
        Routing.RegisterRoute(nameof(CreatePullRequestPage), typeof(CreatePullRequestPage));
        Routing.RegisterRoute(nameof(PullRequestDetailPage), typeof(PullRequestDetailPage));
        Routing.RegisterRoute(nameof(FileBrowserPage), typeof(FileBrowserPage));
        Routing.RegisterRoute(nameof(FileEditorPage), typeof(FileEditorPage));
        Routing.RegisterRoute(nameof(CommitsPage), typeof(CommitsPage));
        Routing.RegisterRoute(nameof(CommitDetailPage), typeof(CommitDetailPage));
        Routing.RegisterRoute(nameof(CheckRunDetailPage), typeof(CheckRunDetailPage));
        Routing.RegisterRoute(nameof(WorkflowRunsPage), typeof(WorkflowRunsPage));
        Routing.RegisterRoute(nameof(WorkflowRunDetailPage), typeof(WorkflowRunDetailPage));
    }

    private void OnNotificationsUpdated(Notification[] notifications, int unreadCount)
        => Dispatcher.Dispatch(() => UpdateUnreadBadge(unreadCount));

    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
        => UpdateChrome();

    private void UpdateChrome()
    {
        UpdateTabIcons();
        if (CurrentPage is not null)
            Shell.SetTabBarIsVisible(CurrentPage, IsRootTabPage(CurrentPage));
    }

    static bool IsRootTabPage(Page page) =>
        page is ReposPage or NotificationsPage or SearchPage or SettingsPage;

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
