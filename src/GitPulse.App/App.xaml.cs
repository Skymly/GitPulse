#if WINDOWS
using GitPulse.App.Platforms.Windows;
#endif

namespace GitPulse.App;

public partial class App : Application
{
#if WINDOWS
    private readonly WindowsAppPresence _windowsPresence;

    public App(WindowsAppPresence windowsPresence)
#else
    public App()
#endif
    {
        InitializeComponent();
#if WINDOWS
        _windowsPresence = windowsPresence;
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
