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
/// Open restores the window; Exit quits the process. Reports visibility
/// via <see cref="IAppPresence"/> for toast coordination (ADR-010).
/// </summary>
public sealed class WindowsAppPresence : IAppPresence, IDisposable
{
    private const string OpenMenuLabel = "Open GitPulse";
    private const string ExitMenuLabel = "Exit";

    private readonly object _gate = new();
    private Microsoft.UI.Xaml.Window? _window;
    private TaskbarIcon? _trayIcon;
    private bool _exitRequested;
    private bool _isMainWindowVisible = true;
    private bool _disposed;

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
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_window is null)
                return;

            _window.Hide();
            _isMainWindowVisible = false;
        }
    }

    /// <summary>
    /// Dispose the tray icon and fully quit the process.
    /// </summary>
    public void Exit()
    {
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

        var exitCommand = new XamlUICommand { Label = ExitMenuLabel };
        exitCommand.ExecuteRequested += (_, _) => Exit();

        var openItem = new WinUIControls.MenuFlyoutItem { Text = OpenMenuLabel, Command = openCommand };
        var exitItem = new WinUIControls.MenuFlyoutItem { Text = ExitMenuLabel, Command = exitCommand };

        var flyout = new WinUIControls.MenuFlyout();
        flyout.Items.Add(openItem);
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
