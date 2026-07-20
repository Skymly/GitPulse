# GitPulse

Personal Windows-first GitHub client used as a real-world showcase for Observables RestAPI and R3.

## Language

**Tray Presence**:
The application remaining available via a Windows system-tray icon after the main window is closed or hidden. Closing the main window hides to the tray; process exit is only via an explicit Exit action on the tray menu.
_Avoid_: background service, daemon, minimize-only

**Toast**:
A Windows system notification surface used to surface new GitHub notifications outside the in-app Notifications tab. Toasts are shown only while the main window is hidden (tray presence); while the window is visible, updates stay in-app. Activating a toast shows the main window and navigates to the Notifications tab. At most one summary toast is shown per poll cycle when multiple new notifications arrive.
_Avoid_: alert, popup, snackbar, in-app banner

**GitHub Notification**:
A notification item from GitHub’s Notifications API, already polled by the existing notification poller.
_Avoid_: toast (the OS surface), alert

**Tray Menu**:
The context menu on the tray icon. For this slice it contains Open GitPulse, Notifications, and Exit.
_Avoid_: jump list, taskbar thumbnail menu

**New Notification**:
A GitHub Notification whose id was not present in the previous poll snapshot. The first snapshot after startup (or after enabling tray presence) establishes the baseline and does not produce toasts.
_Avoid_: unread (a notification can be unread without being new to this session)

**Notification Poller**:
The service that periodically fetches GitHub Notifications. While tray presence is active, polling continues; it stops only when the process exits.
_Avoid_: background sync (vague), push (GitPulse does not use push)
