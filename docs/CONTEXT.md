# GitPulse

Personal Windows-first GitHub client used as a real-world showcase for Observables RestAPI and R3.

## Language

**Tray Presence**:
The application remaining available via a Windows system-tray icon after the main window is closed or hidden. Closing the main window hides to the tray; process exit is only via an explicit Exit action on the tray menu.
_Avoid_: background service, daemon, minimize-only

**Toast**:
A Windows system notification surface used to surface new GitHub notifications outside the in-app Notifications tab. Toasts are shown only while the main window is hidden (tray presence); while the window is visible, updates stay in-app. Activating a toast shows the main window and navigates to the Notifications tab. At most one summary toast is shown per poll cycle when multiple new notifications arrive. Android has no Toast (or other out-of-app notification surface) through v0.1.0.
_Avoid_: alert, popup, snackbar, in-app banner, Android notification

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

**Release Artifact**:
An installable build product attached to a GitHub Release for end users. v0.1.0 shipped the Windows publish-folder zip only (ADR-013). From **v0.1.1** (ADR-014) a cut may also attach the CI-signed Android APK once Android Emulator UI Smoke has passed. Not a store listing package.
_Avoid_: AAB (Play upload package), MSIX store package, unsigned CI compile output

**GitHub Release**:
The sole distribution channel for public cuts: a tagged GitHub Releases entry that carries Release Artifacts. Not a store submission.
_Avoid_: Microsoft Store, Google Play, sideload-only untagged build

**Android Emulator UI Smoke**:
A short, repeatable Appium (UiAutomator2) UI pass on a default portrait phone emulator (API 34+), covering launch, main tabs, saving a PAT, and opening first-class pages without crash—aligned with the Windows FlaUI short smoke. It is a **cut checklist** gate for attaching the signed Android APK; it is not a `CiLib` / tag `release` hard gate by default. Physical-device sideload is optional spot-check only.
_Avoid_: device-only smoke (required), full E2E, IME automation, CI UI gate

**UI Test Host**:
The non-Shell `UiTestHostPage` (TabbedPage + NavigationPage) enabled by `GITPULSE_UI_TEST_HOST=1` so UI automation can see page controls. Used for Windows FlaUI and Android Emulator UI Smoke; not the production Shell chrome.
_Avoid_: production Shell, test-only navigation as user-facing design

**Paged GitHub Session**:
A Core type (`PagedGitHubSession`) that owns one paged GitHub HTTP client cycle: `GitHubQueryHandler` page/state injection, current page cursor, `Link`-header `HasNextPage`, and client dispose. List ViewModels call `RestService.For` on its `Client` and map domain items; they do not reassemble handler + Link parsing. Created via `IGitHubClientFactory.CreatePagedSessionAsync`. Does not own auth-timeout error envelope or Search-specific TotalCount / multi-type tables. Search still uses the tuple `CreatePagedClientAsync` until a follow-up.
_Avoid_: generic HTTP session, repository, paging service, call envelope

**Draft PR**:
A pull request opened with GitHub’s create-time draft flag. In the v0.2.0 create/manage-PRs slice this means optional draft-at-create only; toggling draft ↔ ready after create is out of scope.
_Avoid_: ready for review (in-app), draft lifecycle, WIP PR (informal)

**Pull Request Review**:
A submitted GitHub review on a pull request (author, summary body, submitted time). Listed `state` is GitHub’s submitted state (`APPROVED`, `CHANGES_REQUESTED`, `COMMENTED`, …), not the create-time Review Event. Distinct from a Review Comment (M8 line comment on the diff). PENDING reviews are omitted from the Conversation list.
_Avoid_: review comment (the line comment), pending review, verdict, approval (the event is APPROVE)

**Review Event**:
The submit action sent when creating a Pull Request Review: `APPROVE`, `REQUEST_CHANGES`, or `COMMENT`. v0.3.0 always sends an event (immediate submit). Omitting the event creates a pending review, which is out of scope.
_Avoid_: pending, draft review, review status (GitHub’s `state` on a submitted review)
