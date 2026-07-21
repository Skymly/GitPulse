using GitPulse.App.Services;

#if WINDOWS
using GitPulse.App.Platforms.Windows;
#endif

namespace GitPulse.App;

public partial class App : Application
{
    private readonly NotificationToastHost _toastHost;

#if WINDOWS
    private readonly WindowsAppPresence _windowsPresence;
    private readonly WindowsToastNotifier _toastNotifier;
    private readonly NotificationsNavigator _navigator;

    public App(
        NotificationToastHost toastHost,
        WindowsAppPresence windowsPresence,
        WindowsToastNotifier toastNotifier,
        NotificationsNavigator navigator)
#else
    public App(NotificationToastHost toastHost)
#endif
    {
        InitializeComponent();
        _toastHost = toastHost;
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
        var window = new Window(new AppShell());

#if WINDOWS
        window.HandlerChanged += OnWindowHandlerChanged;
#endif

        return window;
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
            window.HandlerChanged -= OnWindowHandlerChanged;
        }
    }
#endif
}
