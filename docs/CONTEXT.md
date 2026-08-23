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
A Core type (`PagedGitHubSession`) that owns one paged GitHub HTTP client cycle: `GitHubQueryHandler` page/state injection, current page cursor, `Link`-header `HasNextPage`, and client dispose. List ViewModels call `RestService.For` on its `Client` and map domain items; they do not reassemble handler + Link parsing. Created via `IGitHubClientFactory.CreatePagedSessionAsync`. Does not own auth-timeout error envelope or Search-specific TotalCount / multi-type tables. Search, Review inbox, and Assigned inbox use the same session.
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

**Check Run**:
A GitHub Checks API run on a commit (name, `status`, `conclusion`). GitHub Actions produces Check Runs, not Commit Statuses. Listed from `GET /repos/{owner}/{repo}/commits/{ref}/check-runs`. Distinct from a Workflow Run (M10 repo-scoped Actions list) and from a Commit Status.
_Avoid_: status check (ambiguous), CI, Actions run (the M10 workflow run)

**Commit Status**:
A classic status on a commit (`context` + `state` + optional `target_url`). Combined status (`GET /commits/{ref}/status`) aggregates only these. Empty `statuses` yields GitHub combined state `pending`, which is not a Gate Rollup pending by itself.
_Avoid_: check run, combined status (the wrapper), CI

**Gate Rollup**:
A client-side summary of the latest Check Runs plus Commit Statuses on the pull request head SHA: pending, success, failure, or no checks. Not GitHub GraphQL `statusCheckRollup`. Empty combined statuses do not force pending when Check Runs exist or when both lists are empty.
_Avoid_: mergeable_state (opaque GitHub merge-box hint), required checks, status check rollup (GraphQL)

**Starred Repo List**:
The authenticated user's starred repositories from `GET /user/starred`, shown as the Starred hub on the existing Repos tab. Paged with `PagedGitHubSession`. Distinct from My repos (`GET /user/repos`) and from GitHub Search.
_Avoid_: watching / subscriptions, recently viewed, star toggle (write)

**Git Commit**:
A commit on a repository from `GET /repos/{owner}/{repo}/commits` and `GET /repos/{owner}/{repo}/commits/{ref}` (SHA, message, author, date, `html_url`). List payloads omit stats and files; Get-a-commit fills optional `stats` (additions / deletions / total) and `files` (diff-entry shape, optional patch). Distinct from a Check Run, a FileCommitResponse (Contents API write), and a PR head SHA.
_Avoid_: commit comment, compare, blame

**Review Inbox**:
Open pull requests whose review is requested from the authenticated user (directly or via a team), listed from GitHub Search review-requested:@me on the Search tab. Distinct from the Notifications feed (reason=review_requested is mixed and not the full open set) and from requesting reviewers on a single PR.
_Avoid_: notification reason filter, review request (the write), mentioned hub

**Assigned Inbox**:
Open issues and pull requests assigned to the authenticated user, listed from GitHub Search assignee:@me on the Search tab. Distinct from the Review Inbox and from the Notifications feed.
_Avoid_: mentioned hub, created hub, notification reason filter

**Review Request**:
A pending request for a user or team to review a pull request, from `GET/POST/DELETE .../requested_reviewers`. Distinct from a submitted Pull Request Review and from the Review Inbox (cross-repo Search).
_Avoid_: review event, submitted review, review inbox

**Check Run Detail**:
The in-app page for one Check Run from `GET /repos/{owner}/{repo}/check-runs/{check_run_id}` (name, status, conclusion, output). Opened from the PR Gate Rollup. Distinct from a Workflow Run detail and from Gate Rollup (the list summary).
_Avoid_: actions run, check-suite rerequest

**Verified PAT**:
A Personal Access Token that Settings persisted only after `GET /user` returned an authenticated login. Distinct from a typed-but-unsaved token and from OAuth.
_Avoid_: OAuth, GitHub App, device flow

**Issue Assignee**:
A user assigned to an issue from the issue payload and `POST/DELETE /issues/{number}/assignees`. Distinct from a requested reviewer and from the Assigned inbox (cross-repo Search).
_Avoid_: suggested assignees, team assignees

**Fork**:
A user-owned copy of a repository created with `POST /repos/{owner}/{repo}/forks`. Distinct from starring and from watching.
_Avoid_: organization destination, fork network, default_branch_only

**Clone URL**:
The HTTPS `clone_url` from GET repository, shown on repo detail and copied with the platform clipboard. Distinct from `html_url` and from SSH `ssh_url`.
_Avoid_: fork, zipball, git protocol

**Star Toggle**:
Starring or unstarring a repository from repo detail via `PUT`/`DELETE /user/starred/{owner}/{repo}`. Distinct from the Starred Repo List (read-only hub).
_Avoid_: sort=pushed, recents

**Watch Toggle**:
Watching or unwatching a repository from repo detail via `GET`/`PUT`/`DELETE /repos/{owner}/{repo}/subscription`. Watching is subscribed and not ignored. Distinct from starring and from a notification thread subscription.
_Avoid_: watching list, ignore, releases-only

**Recently Pushed Repo List**:
The My repos hub ordered by GitHub `sort=pushed` on `GET /user/repos`. Distinct from Starred and from a local recents store.
_Avoid_: affiliation change, recently viewed

**Check Run Annotation**:
A file/line note on a Check Run (`path`, `start_line`, `annotation_level`, `message`) from the annotations endpoint. Distinct from a Review Comment and from Check Run output summary. Tapping opens the file at the Check Run head SHA via Contents `?ref=` (read-only).
_Avoid_: review comment, line scroll

**Contents Ref**:
The GitHub Contents `ref` query (commit SHA / branch / tag) used to read a file blob at that revision. Distinct from the Contents blob SHA required to update a file.
_Avoid_: treating commit SHA as blob SHA

