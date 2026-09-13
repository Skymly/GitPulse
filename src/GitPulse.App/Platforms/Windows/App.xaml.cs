using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GitPulse.App.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
    // HResults that mean the window or COM host behind a WinUI callback is already gone:
    // RPC_E_DISCONNECTED, RPC_S_SERVER_UNAVAILABLE, ERROR_INVALID_WINDOW_HANDLE.
    private const int RpcDisconnected = unchecked((int)0x80010108);
    private const int RpcServerUnavailable = unchecked((int)0x800706BA);
    private const int InvalidWindowHandle = unchecked((int)0x80070578);

    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        this.InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    private static void OnFirstChanceException(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
    {
        // Log only windowing / tray related exceptions to keep noise down.
        var typeName = e.Exception.GetType().FullName ?? "";
        var message = e.Exception.Message ?? "";
        if (typeName.Contains("COM", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Window", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("AppWindow", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Dispatcher", StringComparison.OrdinalIgnoreCase))
        {
            GitPulse.App.Platforms.Windows.CrashLog.Write("FirstChance", e.Exception);
        }
    }
    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        GitPulse.App.Platforms.Windows.CrashLog.Write("WinUI UnhandledException", e.Exception);
        e.Handled = IsSurvivable(e.Exception);
    }

    /// <summary>
    /// Keeps the tray alive only for faults that originate outside our own code and leave the app
    /// usable. Anything else terminates: surviving a defect turns it into a feature that silently
    /// stops working, which is harder to diagnose than an exit with a crash log.
    /// </summary>
    private static bool IsSurvivable(Exception exception) => exception switch
    {
        // A cancelled navigation or shutdown is not a fault.
        OperationCanceledException => true,
        // The window or tray icon went away while a WinUI callback was still in flight.
        // RPC_E_WRONG_THREAD (0x8001010E) is absent on purpose: cross-thread UI mutation is a
        // defect and has to surface.
        System.Runtime.InteropServices.COMException com =>
            com.HResult is RpcDisconnected or RpcServerUnavailable or InvalidWindowHandle,
        _ => false,
    };

    private static void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        GitPulse.App.Platforms.Windows.CrashLog.Write(
            "AppDomain UnhandledException",
            e.ExceptionObject as Exception);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        GitPulse.App.Platforms.Windows.CrashLog.Write("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }
}
