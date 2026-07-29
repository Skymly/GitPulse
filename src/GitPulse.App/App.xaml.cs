using GitPulse.App.Services;
using GitPulse.App.Views;

#if WINDOWS
using GitPulse.App.Platforms.Windows;
#endif

namespace GitPulse.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly NotificationToastHost _toastHost;

#if WINDOWS
    private readonly WindowsAppPresence _windowsPresence;
    private readonly WindowsToastNotifier _toastNotifier;
    private readonly NotificationsNavigator _navigator;

    public App(
        IServiceProvider services,
        NotificationToastHost toastHost,
        WindowsAppPresence windowsPresence,
        WindowsToastNotifier toastNotifier,
        NotificationsNavigator navigator)
#else
    public App(IServiceProvider services, NotificationToastHost toastHost)
#endif
    {
        InitializeComponent();
        _services = services;
        _toastHost = toastHost;
        AppNavigation.Configure(services);
#if WINDOWS
        _windowsPresence = windowsPresence;
        _toastNotifier = toastNotifier;
        _navigator = navigator;

        _windowsPresence.EnteredTrayPresence += _toastHost.OnEnteredTrayPresence;
        _windowsPresence.NotificationsRequested += _navigator.OpenNotifications;
        _windowsPresence.Exiting += _toastHost.Dispose;
        _toastNotifier.Activated += _navigator.OpenNotifications;
#endif
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        bool uiTestHost = string.Equals(
            Environment.GetEnvironmentVariable("GITPULSE_UI_TEST_HOST"),
            "1",
            StringComparison.Ordinal);

        Page root = uiTestHost
            ? new ContentPage
            {
                Title = "GitPulse",
                Content = new Label
                {
                    Text = "UI test host loading…",
                    AutomationId = "UiTestHostLoading",
                },
            }
            : new AppShell();

        var window = new Window(root);

        if (uiTestHost)
        {
            // Defer real pages until after CreateWindow — constructing SettingsPage
            // during CreateWindow crashes WinUI (STATUS_STOWED_EXCEPTION).
            // Window.Created is unreliable on WinUI; swap on first Appearing.
            root.Appearing += OnUiTestHostPlaceholderAppearing;
        }

#if WINDOWS
        window.HandlerChanged += OnWindowHandlerChanged;
#endif

        return window;
    }

    void OnUiTestHostPlaceholderAppearing(object? sender, EventArgs e)
    {
        if (sender is not Page placeholder)
            return;

        placeholder.Appearing -= OnUiTestHostPlaceholderAppearing;

        Window? window = placeholder.Window;
        if (window is null)
            return;

        // Dispatch so we are not replacing Page mid-Appearing.
        placeholder.Dispatcher.Dispatch(() =>
        {
            try
            {
                window.Page = new UiTestHostPage(_services);
            }
            catch (Exception ex)
            {
                window.Page = new ContentPage
                {
                    Title = "GitPulse",
                    Content = new Label
                    {
                        Text = $"UiTestHost failed: {ex}",
                        Margin = new Thickness(16),
                        AutomationId = "UiTestHostError",
                    },
                };
            }
        });
    }

#if WINDOWS
    private void OnWindowHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is not Window window)
            return;

        if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
        {
            nativeWindow.TryMicaOrAcrylic();
            _windowsPresence.Attach(nativeWindow);
            _toastNotifier.EnsureInitialized();
            window.HandlerChanged -= OnWindowHandlerChanged;
        }
    }
#endif
}
