using System.Drawing;
using GitPulse.Core.Abstractions;
using H.NotifyIcon;
using H.NotifyIcon.Core;
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
            _window.Closed += OnWindowClosed;
            EnsureTrayIcon();
        }
    }

    /// <summary>
    /// Show and activate the main window; mark presence as visible.
    /// </summary>
    public void ShowMainWindow()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_window is null)
                return;

            _window.Show();
            _window.Activate();
            _isMainWindowVisible = true;
        }
    }

    /// <summary>
    /// Hide the main window to the tray; mark presence as hidden.
    /// </summary>
    public void HideToTray()
    {
        var enteredTray = false;

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_window is null)
                return;

            _window.Hide();
            if (_isMainWindowVisible)
            {
                _isMainWindowVisible = false;
                enteredTray = true;
            }
        }

        if (enteredTray)
            EnteredTrayPresence?.Invoke();
    }

    /// <summary>
    /// Dispose the tray icon and fully quit the process.
    /// </summary>
    public void Exit()
    {
        Exiting?.Invoke();

        lock (_gate)
        {
            if (_disposed)
                return;

            _exitRequested = true;
            DisposeTrayIcon();

            var window = _window;
            _window = null;

            if (window is not null)
            {
                window.Closed -= OnWindowClosed;
                window.Close();
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
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (_exitRequested)
            return;

        args.Handled = true;
        HideToTray();
    }

    private void EnsureTrayIcon()
    {
        if (_trayIcon is not null)
            return;

        var openCommand = new XamlUICommand { Label = OpenMenuLabel };
        openCommand.ExecuteRequested += (_, _) => ShowMainWindow();

        var notificationsCommand = new XamlUICommand { Label = NotificationsMenuLabel };
        notificationsCommand.ExecuteRequested += (_, _) => NotificationsRequested?.Invoke();

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

        _trayIcon.ForceCreate();
    }

    private void DisposeTrayIcon()
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
    }
}
