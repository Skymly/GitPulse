using GitPulse.App.Views;

namespace GitPulse.App;

/// <summary>
///   Non-Shell host for Windows UI automation. MAUI Shell's NavigationView does
///   not expose ContentPage bodies to UIA (FlaUI/Appium), so UITests launch with
///   GITPULSE_UI_TEST_HOST=1 to use a TabbedPage that keeps page controls visible.
/// </summary>
/// <remarks>
///   Page construction must be deferred until after Window.CreateWindow (see App);
///   eager SettingsPage inflate during CreateWindow crashes WinUI.
///   Each tab is wrapped in <see cref="NavigationPage"/> so detail routes can
///   PushAsync when <see cref="Shell.Current"/> is null.
/// </remarks>
public sealed class UiTestHostPage : TabbedPage
{
    public UiTestHostPage(IServiceProvider services)
    {
        Title = "GitPulse";

        Children.Add(Wrap("Repos", services.GetRequiredService<ReposPage>()));
        Children.Add(Wrap("Notifications", services.GetRequiredService<NotificationsPage>()));
        Children.Add(Wrap("Search", services.GetRequiredService<SearchPage>()));
        Children.Add(Wrap("Settings", services.GetRequiredService<SettingsPage>()));
    }

    static NavigationPage Wrap(string title, Page page)
    {
        page.Title = title;
        return new NavigationPage(page)
        {
            Title = title,
        };
    }
}
