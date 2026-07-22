using System.Drawing;
using System.Runtime.InteropServices;
using GitPulse.Core.Abstractions;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using WinRT.Interop;
using WinUIControls = Microsoft.UI.Xaml.Controls;

namespace GitPulse.App.Platforms.Windows;

/// <summary>
/// Windows Tray Presence: closing the main window hides to the tray;
/// Open restores the window; Notifications opens the Notifications tab;
/// Exit quits the process. Reports visibility via <see cref="IAppPresence"/>
/// for toast coordination (ADR-010).
/// </summary>
/// <remarks>
/// Must cancel <see cref="AppWindow.Closing"/> — not only handle
/// <see cref="Microsoft.UI.Xaml.Window.Closed"/>. Cancelling too late disposes
/// MAUI page content, so restore shows a blank solid-color window and can
/// later fault in Microsoft.UI.Windowing.dll.
/// </remarks>
public sealed class WindowsAppPresence : IAppPresence, IDisposable
{
    private const string OpenMenuLabel = "Open GitPulse";
    private const string NotificationsMenuLabel = "Notifications";
    private const string ExitMenuLabel = "Exit";

    private readonly object _gate = new();
    private Microsoft.UI.Xaml.Window? _window;
    private AppWindow? _appWindow;
    private DispatcherQueue? _dispatcher;
    private TaskbarIcon? _trayIcon;
    private bool _exitRequested;
    private bool _isMainWindowVisible = true;
    private bool _disposed;

    /// <summary>
    /// Raised when the main window is hidden to the tray (not on Exit).
    /// </summary>
    public event Action? EnteredTrayPresence;

    /// <summary>
    /// Raised when the user chooses Notifications from the tray menu.
    /// </summary>
    public event Action? NotificationsRequested;

    /// <summary>
    /// Raised at the start of <see cref="Exit"/> so hosts can stop polling
    /// before the process terminates.
    /// </summary>
    public event Action? Exiting;

    /// <inheritdoc />
    public bool IsMainWindowVisible
    {
        get
        {
            lock (_gate)
                return _isMainWindowVisible;
        }
    }

    /// <summary>
    /// Hook the native WinUI window once its handler is ready.
    /// Idempotent: subsequent calls are ignored.
    /// </summary>
    public void Attach(Microsoft.UI.Xaml.Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_window is not null)
                return;

            _window = window;
            _dispatcher = window.DispatcherQueue;

            var hwnd = WindowNative.GetWindowHandle(window);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);
            _appWindow.Closing += OnAppWindowClosing;

            EnsureTrayIcon();
        }

        MaybeStartTraySmoke();
    }

    /// <summary>
    /// Show and activate the main window; mark presence as visible.
    /// Marshals to the window dispatcher — tray commands may not run on the UI thread.
    /// </summary>
    public void ShowMainWindow()
    {
        if (!TryGetWindow(out var window, out var appWindow, out var dispatcher))
            return;

        if (dispatcher.HasThreadAccess)
        {
            ShowMainWindowCore(window, appWindow);
            return;
        }

        _ = dispatcher.TryEnqueue(() => ShowMainWindowCore(window, appWindow));
    }

    /// <summary>
    /// Hide the main window to the tray; mark presence as hidden.
    /// </summary>
    public void HideToTray()
    {
        if (!TryGetWindow(out var window, out var appWindow, out var dispatcher))
            return;

        if (dispatcher.HasThreadAccess)
        {
            HideToTrayCore(window, appWindow);
            return;
        }

        _ = dispatcher.TryEnqueue(() => HideToTrayCore(window, appWindow));
    }

    /// <summary>
    /// Dispose the tray icon and fully quit the process.
    /// </summary>
    public void Exit()
    {
        try
        {
            Exiting?.Invoke();
        }
        catch (Exception ex)
        {
            CrashLog.Write("Exiting handler failed", ex);
        }

        AppWindow? appWindow;
        Microsoft.UI.Xaml.Window? window;

        lock (_gate)
        {
            if (_disposed)
                return;

            _exitRequested = true;
            DisposeTrayIcon();

            appWindow = _appWindow;
            window = _window;
            _window = null;
            _appWindow = null;
            _dispatcher = null;

            if (appWindow is not null)
                appWindow.Closing -= OnAppWindowClosing;
        }

        try
        {
            // Allow Closing to proceed, then tear down.
            appWindow?.Hide();
            window?.Close();
        }
        catch (Exception ex)
        {
            CrashLog.Write("Exit close failed", ex);
        }

        Microsoft.Maui.Controls.Application.Current?.Quit();
        Environment.Exit(0);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            _exitRequested = true;
            DisposeTrayIcon();

            if (_appWindow is not null)
            {
                _appWindow.Closing -= OnAppWindowClosing;
                _appWindow = null;
            }

            _window = null;
            _dispatcher = null;
        }
    }

    private bool TryGetWindow(
        out Microsoft.UI.Xaml.Window window,
        out AppWindow appWindow,
        out DispatcherQueue dispatcher)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            window = _window!;
            appWindow = _appWindow!;
            dispatcher = _dispatcher!;
            return _window is not null && _appWindow is not null && _dispatcher is not null;
        }
    }

    private void ShowMainWindowCore(Microsoft.UI.Xaml.Window window, AppWindow appWindow)
    {
        try
        {
            appWindow.IsShownInSwitchers = true;
            appWindow.Show();
            window.Activate();

            lock (_gate)
                _isMainWindowVisible = true;
        }
        catch (Exception ex)
        {
            CrashLog.Write("ShowMainWindowCore failed", ex);
            throw;
        }
    }

    private void HideToTrayCore(Microsoft.UI.Xaml.Window window, AppWindow appWindow)
    {
        _ = window;
        var enteredTray = false;

        try
        {
            appWindow.Hide();
            appWindow.IsShownInSwitchers = false;

            lock (_gate)
            {
                if (_isMainWindowVisible)
                {
                    _isMainWindowVisible = false;
                    enteredTray = true;
                }
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write("HideToTrayCore failed", ex);
            throw;
        }

        if (!enteredTray)
            return;

        try
        {
            EnteredTrayPresence?.Invoke();
        }
        catch (Exception ex)
        {
            CrashLog.Write("EnteredTrayPresence handler failed", ex);
        }
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_exitRequested)
            return;

        // Cancel before WinUI/MAUI disposes page content.
        args.Cancel = true;
        try
        {
            HideToTray();
        }
        catch (Exception ex)
        {
            CrashLog.Write("OnAppWindowClosing/HideToTray failed", ex);
        }
    }

    private void EnsureTrayIcon()
    {
        if (_trayIcon is not null)
            return;

        var openCommand = new XamlUICommand { Label = OpenMenuLabel };
        openCommand.ExecuteRequested += (_, _) => ShowMainWindow();

        var notificationsCommand = new XamlUICommand { Label = NotificationsMenuLabel };
        notificationsCommand.ExecuteRequested += (_, _) =>
        {
            try
            {
                NotificationsRequested?.Invoke();
            }
            catch (Exception ex)
            {
                CrashLog.Write("NotificationsRequested failed", ex);
            }
        };

        var exitCommand = new XamlUICommand { Label = ExitMenuLabel };
        exitCommand.ExecuteRequested += (_, _) => Exit();

        var openItem = new WinUIControls.MenuFlyoutItem { Text = OpenMenuLabel, Command = openCommand };
        var notificationsItem = new WinUIControls.MenuFlyoutItem
        {
            Text = NotificationsMenuLabel,
            Command = notificationsCommand,
        };
        var exitItem = new WinUIControls.MenuFlyoutItem { Text = ExitMenuLabel, Command = exitCommand };

        var flyout = new WinUIControls.MenuFlyout();
        flyout.Items.Add(openItem);
        flyout.Items.Add(notificationsItem);
        flyout.Items.Add(exitItem);

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "GitPulse",
            ContextFlyout = flyout,
            MenuActivation = PopupActivationMode.RightClick,
            LeftClickCommand = openCommand,
            NoLeftClickDelay = true,
        };

        var iconPath = Path.Combine(AppContext.BaseDirectory, "tray.ico");
        if (File.Exists(iconPath))
        {
            _trayIcon.Icon = new Icon(iconPath);
        }

        _trayIcon.ForceCreate(enablesEfficiencyMode: false);
    }

    private void DisposeTrayIcon()
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
    }

    /// <summary>
    /// Agent/manual smoke: set GITPULSE_TRAY_SMOKE=1 to auto hide then show.
    /// Writes %TEMP%\GitPulse-tray-smoke.txt with SURVIVED or the exception.
    /// </summary>
    private void MaybeStartTraySmoke()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("GITPULSE_TRAY_SMOKE"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        var resultPath = Path.Combine(Path.GetTempPath(), "GitPulse-tray-smoke.txt");
        try
        {
            File.Delete(resultPath);
        }
        catch
        {
            // ignore
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(2500).ConfigureAwait(false);

                // Simulate clicking the title-bar X (AppWindow.Closing path),
                // not a direct Hide — that was the blank-content bug.
                var hwnd = IntPtr.Zero;
                if (_dispatcher is not null)
                {
                    var tcsHwnd = new TaskCompletionSource<IntPtr>();
                    _ = _dispatcher.TryEnqueue(() =>
                    {
                        try
                        {
                            tcsHwnd.TrySetResult(
                                _window is null
                                    ? IntPtr.Zero
                                    : WindowNative.GetWindowHandle(_window));
                        }
                        catch (Exception ex)
                        {
                            tcsHwnd.TrySetException(ex);
                        }
                    });
                    hwnd = await tcsHwnd.Task.ConfigureAwait(false);
                }

                if (hwnd != IntPtr.Zero)
                    _ = PostMessage(hwnd, WmClose, IntPtr.Zero, IntPtr.Zero);

                await Task.Delay(2000).ConfigureAwait(false);
                ShowMainWindow();
                await Task.Delay(3000).ConfigureAwait(false);

                // Content still present? Blank restore was the user-facing bug.
                var hasContent = false;
                if (_dispatcher is not null)
                {
                    var tcs = new TaskCompletionSource<bool>();
                    _ = _dispatcher.TryEnqueue(() =>
                    {
                        try
                        {
                            hasContent = _window?.Content is not null;
                            tcs.TrySetResult(hasContent);
                        }
                        catch (Exception ex)
                        {
                            tcs.TrySetException(ex);
                        }
                    });
                    hasContent = await tcs.Task.ConfigureAwait(false);
                }

                File.WriteAllText(
                    resultPath,
                    hasContent ? "SURVIVED_WITH_CONTENT" : "SURVIVED_BLANK_CONTENT");
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(resultPath, "FAILED: " + ex);
                }
                catch
                {
                    // ignore
                }

                CrashLog.Write("Tray smoke failed", ex);
            }
        });
    }

    private const uint WmClose = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
