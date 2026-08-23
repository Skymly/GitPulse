using System.Reflection;

using GitPulse.App.Views;

namespace GitPulse.App;

/// <summary>
///   Navigation that prefers Shell, and falls back to <see cref="NavigationPage"/>
///   push/pop when running under <see cref="UiTestHostPage"/> (no Shell).
/// </summary>
public static class AppNavigation
{
    static IServiceProvider? _services;

    public static void Configure(IServiceProvider services) => _services = services;

    public static Task GoToAsync(string route)
    {
        if (Shell.Current is not null)
            return Shell.Current.GoToAsync(route);

        return GoToWithoutShellAsync(route);
    }

    static async Task GoToWithoutShellAsync(string route)
    {
        if (_services is null)
            throw new InvalidOperationException("AppNavigation.Configure was not called.");

        if (route is ".." or "../")
        {
            INavigation? nav = GetActiveNavigation();
            if (nav is not null && nav.NavigationStack.Count > 1)
                await nav.PopAsync();
            return;
        }

        // Absolute Shell tab routes (e.g. //NotificationsPage) → select host tab.
        if (route.StartsWith("//", StringComparison.Ordinal))
        {
            SelectHostTab(route.TrimStart('/'));
            return;
        }

        int q = route.IndexOf('?', StringComparison.Ordinal);
        string pageName = q >= 0 ? route[..q] : route;
        string query = q >= 0 ? route[(q + 1)..] : string.Empty;

        Page page = ResolvePage(pageName);
        ApplyQueryProperties(page, query);

        INavigation navigation = GetActiveNavigation()
            ?? throw new InvalidOperationException("No active NavigationPage for UiTestHost.");

        await navigation.PushAsync(page);
    }

    static Page ResolvePage(string pageName) => pageName switch
    {
        "RepoDetailPage" => _services!.GetRequiredService<RepoDetailPage>(),
        "IssuesPage" => _services!.GetRequiredService<IssuesPage>(),
        "IssueDetailPage" => _services!.GetRequiredService<IssueDetailPage>(),
        "CreateIssuePage" => _services!.GetRequiredService<CreateIssuePage>(),
        "CreatePullRequestPage" => _services!.GetRequiredService<CreatePullRequestPage>(),
        "PullRequestsPage" => _services!.GetRequiredService<PullRequestsPage>(),
        "PullRequestDetailPage" => _services!.GetRequiredService<PullRequestDetailPage>(),
        "FileBrowserPage" => _services!.GetRequiredService<FileBrowserPage>(),
        "FileEditorPage" => _services!.GetRequiredService<FileEditorPage>(),
        "CommitsPage" => _services!.GetRequiredService<CommitsPage>(),
        "CommitDetailPage" => _services!.GetRequiredService<CommitDetailPage>(),
        "CheckRunDetailPage" => _services!.GetRequiredService<CheckRunDetailPage>(),
        "WorkflowRunsPage" => _services!.GetRequiredService<WorkflowRunsPage>(),
        "WorkflowRunDetailPage" => _services!.GetRequiredService<WorkflowRunDetailPage>(),
        _ => throw new InvalidOperationException($"Unknown UiTestHost route '{pageName}'."),
    };

    static void ApplyQueryProperties(Page page, string query)
    {
        if (string.IsNullOrEmpty(query))
            return;

        Dictionary<string, string> values = ParseQuery(query);
        foreach (QueryPropertyAttribute attr in page.GetType().GetCustomAttributes<QueryPropertyAttribute>())
        {
            if (!values.TryGetValue(attr.QueryId, out string? raw))
                continue;

            PropertyInfo? prop = page.GetType().GetProperty(attr.Name);
            if (prop?.CanWrite == true && prop.PropertyType == typeof(string))
                prop.SetValue(page, raw);
        }
    }

    static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string part in query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0)
                continue;

            values[part[..eq]] = part[(eq + 1)..];
        }

        return values;
    }

    static void SelectHostTab(string routeName)
    {
        if (GetRootPage() is not TabbedPage tabs)
            return;

        string title = routeName switch
        {
            "ReposPage" => "Repos",
            "NotificationsPage" => "Notifications",
            "SearchPage" => "Search",
            "SettingsPage" => "Settings",
            _ => routeName,
        };

        Page? match = tabs.Children.FirstOrDefault(c =>
            string.Equals(c.Title, title, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
            tabs.CurrentPage = match;
    }

    static INavigation? GetActiveNavigation()
    {
        Page? page = GetCurrentPage();
        return page?.Navigation;
    }

    static Page? GetRootPage() =>
        Application.Current?.Windows.FirstOrDefault()?.Page;

    static Page? GetCurrentPage()
    {
        Page? page = GetRootPage();
        while (page is not null)
        {
            switch (page)
            {
                case FlyoutPage flyout:
                    page = flyout.Detail;
                    continue;
                case TabbedPage tabs:
                    page = tabs.CurrentPage;
                    continue;
                case NavigationPage nav:
                    page = nav.CurrentPage;
                    continue;
                default:
                    return page;
            }
        }

        return null;
    }
}
