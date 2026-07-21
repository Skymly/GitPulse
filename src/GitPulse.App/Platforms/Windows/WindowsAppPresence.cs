using System.Drawing;
using GitPulse.Core.Abstractions;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using WinUIControls = Microsoft.UI.Xaml.Controls;

namespace GitPulse.App.Platforms.Windows;

/// <summary>
/// Windows Tray Presence: closing the main window hides to the tray;
/// Open restores the window; Notifications opens the Notifications tab;
/// Exit quits the process. Reports visibility via <see cref="IAppPresence"/>
/// for toast coordination (ADR-010).
/// </summary>
public sealed class WindowsAppPresence : IAppPresence, IDisposable
{
    private const string OpenMenuLabel = "Open GitPulse";
    private const string NotificationsMenuLabel = "Notifications";
    private const string ExitMenuLabel = "Exit";

    private readonly object _gate = new();
    private Microsoft.UI.Xaml.Window? _window;
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
            _window.Closed += OnWindowClosed;
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
        if (!TryGetWindow(out var window, out var dispatcher))
            return;

        if (dispatcher.HasThreadAccess)
        {
            ShowMainWindowCore(window);
            return;
        }

        _ = dispatcher.TryEnqueue(() => ShowMainWindowCore(window));
    }

    /// <summary>
    /// Hide the main window to the tray; mark presence as hidden.
    /// </summary>
    public void HideToTray()
    {
        if (!TryGetWindow(out var window, out var dispatcher))
            return;

        if (dispatcher.HasThreadAccess)
        {
            HideToTrayCore(window);
            return;
        }

        _ = dispatcher.TryEnqueue(() => HideToTrayCore(window));
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

        lock (_gate)
        {
            if (_disposed)
                return;

            _exitRequested = true;
            DisposeTrayIcon();

            var window = _window;
            _window = null;
            _dispatcher = null;

            if (window is not null)
            {
                window.Closed -= OnWindowClosed;
                try
                {
                    window.Close();
                }
                catch (Exception ex)
                {
                    CrashLog.Write("Exit window.Close failed", ex);
                }
            }
        }

        // Always tear down the MAUI host. Close alone can leave a headless
        // process; Environment.Exit covers H.NotifyIcon (#66) edge cases.
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

            if (_window is not null)
            {
                _window.Closed -= OnWindowClosed;
                _window = null;
            }

            _dispatcher = null;
        }
    }

    private bool TryGetWindow(
        out Microsoft.UI.Xaml.Window window,
        out DispatcherQueue dispatcher)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            window = _window!;
            dispatcher = _dispatcher!;
            return _window is not null && _dispatcher is not null;
        }
    }

    private void ShowMainWindowCore(Microsoft.UI.Xaml.Window window)
    {
        try
        {
            // Use plain Show/Activate — H.NotifyIcon efficiency-mode Show has
            // crashed in Microsoft.UI.Windowing.dll (0xe0464645) on restore.
            window.Show();
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

    private void HideToTrayCore(Microsoft.UI.Xaml.Window window)
    {
        var enteredTray = false;

        try
        {
            window.Hide();

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

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (_exitRequested)
            return;

        args.Handled = true;
        try
        {
            HideToTray();
        }
        catch (Exception ex)
        {
            CrashLog.Write("OnWindowClosed/HideToTray failed", ex);
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
                HideToTray();
                await Task.Delay(2000).ConfigureAwait(false);
                ShowMainWindow();
                await Task.Delay(2000).ConfigureAwait(false);
                File.WriteAllText(resultPath, "SURVIVED");
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
}
