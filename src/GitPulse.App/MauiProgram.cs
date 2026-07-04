using GitPulse.App.Services;
using GitPulse.App.Views;
using GitPulse.Core.Abstractions;
using GitPulse.Services;
using GitPulse.ViewModels;
using Indiko.Maui.Controls.Markdown;
using Microsoft.Extensions.Logging;
using R3;
using R3.Maui;

#if WINDOWS
using GitPulse.App.Platforms.Windows;
#elif ANDROID
using GitPulse.App.Platforms.Android;
#endif

namespace GitPulse.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .UseR3()
            .UseMarkdownView();

        // Platform-specific credential store.
#if WINDOWS
		builder.Services.AddSingleton<ICredentialStore, WindowsCredentialStore>();
#elif ANDROID
        builder.Services.AddSingleton<ICredentialStore, AndroidCredentialStore>();
#endif

        // Application services.
        builder.Services.AddSingleton<IGitHubClientFactory, GitHubClientFactory>();
        builder.Services.AddSingleton<IBrowserLauncher, BrowserLauncher>();
        builder.Services.AddSingleton<INotificationPoller, NotificationPoller>();

        // ViewModels (transient — each page gets a fresh instance).
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<ReposViewModel>();
        builder.Services.AddTransient<IssuesViewModel>();
        builder.Services.AddTransient<IssueDetailViewModel>();
        builder.Services.AddTransient<CreateIssueViewModel>();
        builder.Services.AddTransient<PullRequestsViewModel>();
        builder.Services.AddTransient<PullRequestDetailViewModel>();
        builder.Services.AddTransient<NotificationsViewModel>();

        // Pages (transient — resolved via DI when Shell navigates).
        builder.Services.AddTransient<ReposPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<IssuesPage>();
        builder.Services.AddTransient<IssueDetailPage>();
        builder.Services.AddTransient<CreateIssuePage>();
        builder.Services.AddTransient<PullRequestsPage>();
        builder.Services.AddTransient<PullRequestDetailPage>();
        builder.Services.AddTransient<NotificationsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
