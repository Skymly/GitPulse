using Microsoft.Extensions.DependencyInjection;

#if WINDOWS
using GitPulse.App.Platforms.Windows;
#endif

namespace GitPulse.App;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
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
    private static void OnWindowHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is not Window window)
            return;

        if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
        {
            nativeWindow.TryMicaOrAcrylic();
            window.HandlerChanged -= OnWindowHandlerChanged;
        }
    }
#endif
}
