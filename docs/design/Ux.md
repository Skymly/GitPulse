# Design Doc: UX

> **Version**: Unreleased
> **Related ADRs**: [ADR-004](../adr/ADR-004-pat-auth-platform-credential-store.md), [ADR-005](../adr/ADR-005-windows-first-platform-strategy.md), [ADR-010](../adr/ADR-010-windows-tray-presence-and-toast.md), [ADR-011](../adr/ADR-011-android-m11-daily-usable-phone.md), [ADR-012](../adr/ADR-012-v0.1.0-github-release-artifacts.md), [ADR-013](../adr/ADR-013-v0.1.0-windows-only-github-release.md), [ADR-014](../adr/ADR-014-android-emulator-ui-smoke-and-apk-release.md)
> **Related issues**: [Product-level UX spec map](https://github.com/Skymly/GitPulse/issues/446) (decision index); spec body for [Write the product-level UX Design Doc](https://github.com/Skymly/GitPulse/issues/456)

## Overview

GitPulse's product language is **Fluent/WinUI chrome + GitHub domain semantics**. It is not a github.com skin and not the MAUI template (OpenSans, template purple, `dotnet_bot` tab icons). Windows and Android share **rules**, not layouts.

This document is the handoff spec. The map remains the decision index; closed tickets are cited by title wrapping a link. If a cited sentence conflicts with a later ticket, the later resolution wins.

## Scope

Visual language, interaction standard, chrome atoms, ranked Surfaces (structural spec for Daily; inherit for the rest), and dual-platform expressions of every shared rule. Tray Presence, Tray Menu, and Toast are a Windows-only appendix.

The app is unchanged until a later map implements. Distribution stays GitHub Releases. Auth stays PAT only.

## Visual language

Locked in [GitPulse visual language](https://github.com/Skymly/GitPulse/issues/450), grounded in [What Fluent can mean on MAUI 10](https://github.com/Skymly/GitPulse/issues/447) and [GitHub domain visual semantics we must not contradict](https://github.com/Skymly/GitPulse/issues/448).

### Color — three layers

**Accent** — product color for selection, focus ring, links, and primary chrome actions. Fixed Fluent blue: light `#0078D4`, dark a lighter Fluent blue (~`#60CDFF`) so it stays visible on dark Chrome Neutral and does not read as Domain open/success. Does not follow the OS accent. Bans template `#512BD4` and Magenta tab selection.

**Domain Color** — only for GitHub object states. Follow Primer **roles** with GitPulse light/dark tokens; do not copy Primer hexes; do not clone github.com StateLabel pills.

| State | Role |
|-------|------|
| Issue/PR open | success / open (green) |
| Closed-as-completed issue; merged PR | done (purple) |
| Closed unmerged PR | danger (red) |
| Draft | muted gray (not attention/orange) |
| Not-planned closed issue (when `state_reason` exists) | neutral + Skip; visible text still Closed |
| Checks `queued` / `in_progress` | attention |
| Checks `success` | success |
| Checks `failure` / `timed_out` / `startup_failure` / `action_required` | danger |
| Checks `cancelled` / `skipped` / `neutral` / `stale` | muted |

Presentation: **16px Domain Icon + visible text**. Never color alone. Never a Primer pill.

- Per-repo labels: small API-hex swatch + name (name always present).
- Notification reasons: Chrome Neutral text (mention, review requested, subscribed, …) — no reason→hue axis.
- Destructive Confirm actions (Merge / Delete file) use danger, not Accent.
- Page Error / Field Error use danger and are not color-only.

**Chrome Neutral** — surface / text / separator follow **platform surface roles**. Windows ≈ WinUI layers (light: white/light gray; dark: dark gray, **not** pure black). Android follows Material surfaces. Not Primer canvas. Not the template Gray* scale as the language.

Light/dark **follows the OS only**. No in-app theme switcher in this language.

### Type and density

Platform typefaces + one ramp: Windows Segoe UI Variable; Android the system sans. Caption 12 / Body 14 / Section 16 / Page title 20. Regular + Semibold (Android may use Medium for Semibold). Interactive targets ≥ 44×44 inside system insets (status bar, gesture inset). Comfortable density: Windows slightly tighter than Android; neither clones GitHub Desktop's ~29px rows. Row minimum heights are in Dual-platform.

Corner radius: ~4px controls, ~8px surfaces.

### Icons

- **Domain Icon**: Octicons at 16px inline, GitHub meanings locked. Do not invent state glyphs. Do not upscale 16px Octicons as decoration.
- **Chrome Icon**: one shippable family — **Fluent UI System Icons** (MIT). Outline 20–24px; **filled** for the selected tab. Not Segoe Fluent Icons (they cannot ship to Android).
- The four Shell destinations use Chrome Icons (Repos / Notifications / Search / Settings), not `dotnet_bot.png`.

### Materials

Windows: window-level **Mica Base** with Acrylic fallback (already in the app). Custom TitleBar (GitPulse Mark + title) and TabBar sit on the material. Page body is **full-bleed opaque** Chrome Neutral — lists, details, editor, Markdown do not show wallpaper through text. Android: opaque Material surfaces, no fake blur. Android surface roles are in Dual-platform.

### GitPulse Mark

Geometric mark, metaphor **pulse + graph**: four nodes + ring, `currentColor`, legible at 16px Tray. No mascot, no letterform, no cat, no Invertocat, no `.NET` wordmark. Monochrome and invertible with theme. App icon / splash: Accent field + light mark (replaces purple + `.NET`). SVG paths live on the chrome prototype, not this spec.

## Interaction

Locked in [GitPulse interaction standard](https://github.com/Skymly/GitPulse/issues/451). Shared **rules**; platform chrome for banners, confirm order, and pull-to-refresh is in Dual-platform.

### Feedback layers

Two surfaces only:

- **Page Error** — in-page failure for a GitHub request that did not succeed (load, write, timeout, 401/403/404/422/5xx).
- **Field Error** — client-side validation next to the control that must change.

Success is quiet: the page state updates. Never put success in the error banner. **Toast** remains the Windows out-of-app surface for New Notification while the window is hidden; it is not an error surface. No app-wide error modal queue.

### Stay-on-page

Every request failure stays on the current page: first load, Load more, writes, 401, 403, 404, 422, 5xx, timeout. Missing PAT / 401 is a Page Error with a Settings action, not an automatic navigation. Search 403 (rate limit) and 422 (syntax) stay on the query with the dedicated copy; do not invent an immediate Retry for rate limit. User-initiated Back, Shell tab changes, and tapping Settings are not Stay-on-page failures.

### Page Error

One slot per page. A new failure replaces the previous Page Error. Must include a readable reason (not color alone, not a raw exception) and at least one action:

- Retryable failure → Retry (replay the failed request; do not reset already-loaded rows).
- 401 / missing PAT → Settings.
- 404 on a detail → Retry and Back (Back is user-initiated leave).
- Search 403/422 → stay on the query with the dedicated copy.

Load more failure uses the same page-top Page Error; existing rows stay; the Load more control stays visible. Successful regions on a compound page stay visible. Page Error is not dismiss-only without an action.

### Field Error

Client preflight only: empty title, Search shorter than 3 characters, missing commit message for save/delete. HTTP status codes never become Field Error. If a Field Error is present, do not send the request.

### Loading

First load with no data: spinner in the **data region**, list hidden; page chrome (title, filters, Create) and Shell stay. After data exists, refresh / Load more / writes keep content; only the acting control is busy; ignore re-entry; do not queue. Load more has its own busy, not the first-load `IsLoading`. Spinner and control busy are **state**, not motion — they remain when the OS asks to reduce motion. Loading never traps Shell destinations, Back, or Tray Presence.

### Empty State

Successful load or search with zero items: title + reason. Never show Empty State while loading or after a failure.

- The page can create the first item (Issues / PRs lists) → Create is required.
- Valid empty resting state (empty Notifications, empty Commits) and a way to leave already exists → no invented Open-in-browser action.
- Search with zero hits → the SearchBar is the next step; do not offer Create Issue.

### Lists and pagination

When `HasNextPage` is true: remaining-items auto-request **and** a visible Load more fallback. No page numbers. Failed Load more keeps rows and retries the same page. Pull-to-refresh is not a shared rule.

### Destructive Confirm

Required only for **Merge** and **Delete file**. Close issue, Convert to Draft, Ready for Review, Update Branch, Workflow Dispatch, Star Toggle, and Watch Toggle run immediately; failure is Stay-on-page.

Chrome: a **platform confirm dialog** (the only legitimate modal). It names the object (PR number + title, or file path) and the action; Merge includes the already-selected merge method. Two buttons: Cancel and the action name. Delete file: pass the commit-message Field Error first, then confirm; the dialog does not edit the message.

Escape and Android Back cancel. The destructive button is not the default; Enter must not activate Merge/Delete.

### Success navigation

- Writes that update the current object: stay and refresh.
- Create issue / Create PR: pop the Create page, then open the new detail so Back from the detail returns to the list, not an empty form.
- Delete file: pop to the File Browser.
- Fork: keep the existing “may open the new repo” behavior.
- Failures never navigate.

### Access floor

Interactive targets ≥ 44×44 on both platforms, inside system insets. OS title bar and Tray are exempt.

Windows: tab order follows visual order; focus is visible; Enter activates only the focused explicit action; no application-wide shortcut map. Android: system Back pops the stack.

Body text and error copy must stay readable on the surface color. Errors are not color-only.

### Focus (Windows)

- First-load or 404 Page Error: move focus to the primary action (Retry, or Settings when that is the action).
- Write or Load more Page Error: do not steal focus.
- Field Error on submit: focus the first invalid field.
- Destructive Confirm cancel: return focus to Merge / Delete.
- Success: no extra focus rule.

### Motion

No decorative motion. No page-transition choreography. No duration or easing spec. If the OS asks to reduce motion, skip later animations; spinner/busy stay.

## Chrome atoms

Windows expression locked in [Chrome prototype (tabs, list row, detail header)](https://github.com/Skymly/GitPulse/issues/453) **variant W**. Throwaway illustration: [`prototype/ux-chrome`](https://github.com/Skymly/GitPulse/tree/prototype/ux-chrome) (`prototypes/chrome/index.html?variant=W`). Android expressions of the same atoms are in Dual-platform.

Rejected: left command rail; bottom destinations on Windows.

### Tab bar

Custom TitleBar (GitPulse Mark + "GitPulse") and a **top NavigationView**: four Shell destinations (Repos / Notifications / Search / Settings), Chrome Icon 20px + label, **filled** when selected, Accent underline. Sits on Mica Base. Page body stays opaque Chrome Neutral.

On a stacked Surface the TabBar is **hidden**. Back in the detail header returns to the parent Surface; Shell destinations are reached after Back, not as a parallel strip on the detail.

### List row

Two-line **flush** row, hairline separator, comfortable density:

- Unread: Accent leading edge (2px). Not a Domain Color.
- Leading 16px Domain Icon.
- Title: Body 14 Semibold.
- Meta: Domain Icon + visible state text, then Chrome Neutral caption (repo · notification reason). Reason has no hue.
- Trailing `#number` caption.

Interactive height ≥ 44px.

### Detail header

Two bands:

1. Back · parent Surface · identity (`owner/repo #n`).
2. Domain Icon + state text; Page title 20 (wraps); caption (author · date · reason).

Never a Primer pill. Never color alone.

### GitPulse Mark in chrome

TitleBar uses the 16px monochrome mark (`currentColor`). App icon / splash remain Accent field + light mark. SVG: [`prototypes/chrome/mark.svg`](https://github.com/Skymly/GitPulse/blob/prototype/ux-chrome/prototypes/chrome/mark.svg) on the throwaway branch.

## Surfaces

Ranking locked in [Rank surfaces for the UX spec](https://github.com/Skymly/GitPulse/issues/452). Daily Surfaces get a structural spec; Sometimes and Rare inherit this visual language, interaction standard, and chrome atoms.

A Surface is a first-class, user-reachable place. Not Surfaces (do not occupy rank slots): embedded Diff view and Markdown; chrome atoms; Search Inboxes as separate destinations; Conversation vs Files as separate destinations (they are regions of Pull Request).

Do not redo Shell IA.

### Daily — structural spec

Order is daily-driver frequency.

#### 1. Pull Request

Windows expression locked in [Prototype the top-ranked surfaces](https://github.com/Skymly/GitPulse/issues/454) **variant W** (`prototypes/surfaces/index.html?variant=W` on [`prototype/ux-surfaces`](https://github.com/Skymly/GitPulse/tree/prototype/ux-surfaces)). Default region is Conversation. Narrow-width stacking is in Dual-platform.

**Conversation** (default):

- Locked B detail header; TabBar hidden (stacked Surface).
- Lifecycle strip under the header: head→base, mergeable, Merge (danger, Destructive Confirm), Close, Convert to draft. Merge is not buried in a card.
- Main column: body Markdown, submitted reviews + Review Event composer, comments + composer.
- Metadata/checks rail: assignees, labels (hex swatch + name), requested reviewers, Gate Rollup (Domain Icon + conclusion text, never color alone).

**Files**:

- File list | diff split. Diff view lives here.
- Line-comment stub on the focused file. Not accordion stacking. Not a Files-first default.

Destructive Confirm: platform dialog names `#n` + title and the selected merge method; Cancel + Merge; Escape cancels; Merge is not the default.

Rejected: single-column Lifecycle card (Merge below the fold); accordion Files; Files-first as the default region.

#### 2. Search

One Surface with four hubs: typed Search + Review Inbox / Assigned Inbox / Mentions Inbox.

- Shell TabBar visible (Search is a tab, not stacked).
- One-row hubs: Search / Review / Assigned / Mentions. Not a 2×2 grid. Not a count-card landing.
- Typed Search only: SearchBar + submit + type tabs (Repos / Issues / PRs / Code) + flush two-line rows + Load more.
- Inboxes: the list only (same row atom). No SearchBar.
- Empty State: title + reason; next step is the SearchBar; no Create.
- Field Error: query shorter than 3 characters, next to the SearchBar.
- Page Error: Search 403 rate limit stays on the query; dedicated copy; no Retry.

Rejected: 2×2 hub grid; always-on SearchBar on Inboxes; inbox-card landing.

#### 3. Notifications

Shell tab; TabBar visible. Apply the list-row atom, interaction rules, and the Notifications unread badge (Dual-platform). No second prototype.

- Feed of GitHub Notifications as two-line flush rows (unread Accent edge, Domain Icon, title, visible state text, Chrome Neutral caption `repo · reason`, trailing `#number`).
- Remaining-items auto-request and visible Load more.
- Empty State: title + reason; no invented Open-in-browser action.
- Page Error at the page-top slot; Load more failure keeps rows.
- Toast is not this Surface; it is the Windows-only appendix.

#### 4. Repos

Shell tab; TabBar visible. Existing hubs stay: My repos (Recently Pushed Repo List) and Starred Repo List. Do not invent a Search-style hub strip.

- Same two-line flush row atom.
- Remaining-items auto-request and visible Load more.
- Empty State: title + reason.
- Page Error at the page-top slot.
- Android pull-to-refresh on the hubs is in Dual-platform.

### Inherit — Sometimes

5. Repo detail
6. Issue detail
7. Issues list
8. PRs list

Language, interaction, and chrome atoms only. Issues / PRs lists use Empty State with Create as the next step.

### Inherit — Rare

9. Settings
10. Create issue
11. Create PR
12. File Browser
13. File editor
14. Commits
15. Commit detail
16. Check Run Detail
17. Actions
18. Workflow Run

Commit detail inherits list-then-diff; it does not get a Diff structural spec. Markdown on inherit Surfaces (Issue detail, Repo detail, Check Run Detail) follows the visual language with no extra layout spec.

### Windows-only appendix

Not ranked. No Android equivalents ([ADR-011](../adr/ADR-011-android-m11-daily-usable-phone.md)).

- **Tray Presence**: closing the main window hides to the tray; process exit is only via Exit on the Tray Menu.
- **Tray Menu**: Open GitPulse, Notifications, Exit.
- **Toast**: only while the window is hidden; at most one summary toast per poll cycle for New Notification; activation shows the window and opens Notifications.
- Tray icon has no unread badge ([ADR-010](../adr/ADR-010-windows-tray-presence-and-toast.md)).
- Mica/Acrylic stay in Visual language.

## Dual-platform

Locked in [Dual-platform rule application](https://github.com/Skymly/GitPulse/issues/455). Windows and Android share rules, not layouts. No second prototype on the map.

### Shell

TabBar / destination chrome appears only on the four Shell destinations. On a stacked Surface it is **hidden**; Back in the B detail header (and Android system Back) returns to the parent.

| | Windows | Android |
|---|---------|---------|
| Root chrome | Custom TitleBar (GitPulse Mark + title) + top NavigationView (Chrome Icon + label, filled selected, Accent underline) on Mica Base | Material bottom destinations (same Chrome Icon rules) + title-only top app bar — no Mark in the bar (Mark is launcher / splash) |
| Stacked | TabBar hidden; B detail header | Top bar **is** the B detail header (Back · parent Surface · identity) |
| Page body | Full-bleed opaque Chrome Neutral | Opaque Material `surface` |
| Root Back | — | System Back may leave the app; no double-back-to-exit |

### Density

Shared type ramp as in Visual language. Row minimum height: **Windows 48px**, **Android 56px**. Same two-line flush row atom; only padding changes.

### Materials

- **Windows:** TitleBar and TabBar on Mica Base (Acrylic fallback). Page body opaque.
- **Android:** chrome (top app bar, bottom nav) = `surface-container`; page = `surface`. No fake blur. No wallpaper show-through. Light/dark follows the OS.

### Notifications unread badge

Both platforms: unread count badge on the Notifications Chrome Icon (GitHub `unread` on fetched items; `99+` when greater). Opening the tab does not clear it; mark-as-read or a poll that returns `unread: false` does. The badge is chrome metadata, not a Surface, and is not New Notification. Row Accent unread edge and the badge count the same unread.

Android is silent in the background; returning to the foreground updates on the next poll. Toast remains Windows-only.

### Pull-to-refresh

Not a shared rule. **Windows never.** Android only on feed/list Surfaces: Notifications, Repos hubs, Search Inboxes, Issues/PRs lists. Not on details, Settings, editors, or typed Search.

PTR discards the current paging session and reloads page 1 (Notifications: an immediate poll). Ignore re-entry while busy. Failure is Stay-on-page Page Error and keeps already-loaded rows. Does not replace remaining-items + Load more.

### Destructive Confirm order

LTR expected order is Cancel then the action name; follow the platform HIG if it disagrees — do not pixel-lock left/right. The action is not the default.

### Narrow width (content width)

One **width** rule, not an Android-only layout:

- **≥ 840px:** two-pane expression from the Windows surface prototype (Conversation metadata/checks rail; Files list | diff).
- **< 840px:** stack. Conversation: B detail header → lifecycle strip → main column (body / reviews / comments) → rail (assignees / labels / reviewers / Gate Rollup), one scrolling column. Merge stays on the strip. Files: tap a file **pushes a stacked diff page** (still the Files region of Pull Request, not a new Surface); Back returns to the file list. Search hubs stay one row as four equal segments (labels may shorten); not a 2×2 grid.

Windows snapped/narrow windows stack too. Android **required** expression is portrait stacked ([ADR-011](../adr/ADR-011-android-m11-daily-usable-phone.md) DoD). Two-pane on wide Android is allowed by the rule and is not an acceptance target.

### Page Error chrome

Both platforms: one in-page banner under the header / top app bar, at the top of the content region, full width, danger, readable reason + action. Not a snackbar, not a dialog, not Toast.

### IME

Unchanged from [ADR-011](../adr/ADR-011-android-m11-daily-usable-phone.md): composers must keep the primary submit action reachable above the IME.

## Invariants

1. Accent is never Domain Color; Domain Color is never chrome selection, focus, or primary chrome actions.
2. GitHub state is Domain Icon + visible text; color is never the only signal.
3. Every GitHub request failure is Stay-on-page.
4. Two feedback layers only: Page Error and Field Error. Toast is not an error surface.
5. Destructive Confirm is only Merge and Delete file.
6. Destination chrome is hidden on stacked Surfaces; Shell destinations are reached after Back.
7. Light/dark follows the OS; there is no in-app theme switcher.
8. Loading never traps Shell destinations, Back, or Tray Presence.
9. Empty State appears only after a successful zero-item load.

## Tradeoffs

- **Rules shared, layouts not.** Public GitHub clients do not share one chrome ([What product-level chrome looks like in public GitHub clients](https://github.com/Skymly/GitPulse/issues/449)): Desktop is a compact local-Git workspace; Mobile uses platform bottom destinations; domain lists can live inside host chrome. Shared patterns are domain Octicons, actionable empty states, layered errors, and platform-rule parity — not layout clones. GitPulse keeps its Shell IA and does not clone Desktop.
- **Fixed Accent, not OS accent.** An OS accent that is green, red, or purple would collide with Domain Color.
- **Primer roles, not Primer hexes or StateLabel pills.** GitPulse tokens follow owner semantics without becoming a github.com skin.
- **Fluent UI System Icons for chrome, Octicons for domain.** One MIT chrome family ships on both platforms; Segoe Fluent Icons cannot.
- **Daily structural spec, inherit for the rest.** Ranking by daily-driver frequency avoids a page-by-page layout catalog.

## Known limits

- This document is a spec, not an implementation. The app still ships the MAUI template look until a later map implements, Windows-first if needed ([ADR-005](../adr/ADR-005-windows-first-platform-strategy.md)). Today's `Green700` / `Purple700` / `Orange900` usage contradicts several Domain Color roles ([GitHub domain visual semantics we must not contradict](https://github.com/Skymly/GitPulse/issues/448)).
- MAUI 10 on the Windows TFM is WinUI 3 underneath; MAUI XAML is not Fluent XAML. Theming today is `AppThemeBinding` plus Styles, not WinUI `{ThemeResource}` / type ramp ([What Fluent can mean on MAUI 10](https://github.com/Skymly/GitPulse/issues/447)).
- Throwaway prototypes on `prototype/ux-chrome` and `prototype/ux-surfaces` are illustrations. They are not merged; HTML mocks are not the app.
- Mark SVG paths belong to the chrome prototype, not this spec.
- Implementation cataloguing (XAML keys, control catalog) is after the spec.
- Android has no out-of-app notification surface through the current ADR-011 bound.

## Out of scope

- Changing the app, XAML, styles, Shell IA, or any product module in the PR that lands this spec
- Merging throwaway prototype branches
- XAML `x:Key` names and a control catalog beyond the visual language
- Microsoft Store / Google Play / Authenticode / AAB
- GitHub App OAuth
- iOS / MacCatalyst
- Android out-of-app notifications
- GitHub Desktop feature parity or Desktop shortcut maps
- Pixel-identical phone and desktop layouts
- Page-by-page spec for every inherit Surface
- Redoing navigation IA
- A visible demo aesthetic in the UI
- Full WCAG certification and i18n
- New GitHub REST verbs / the next daily-driver feature slice
- A second prototype round
- `/to-tickets` slicing or implementing the look

## References

- [Product-level UX spec map](https://github.com/Skymly/GitPulse/issues/446)
- [What Fluent can mean on MAUI 10](https://github.com/Skymly/GitPulse/issues/447)
- [GitHub domain visual semantics we must not contradict](https://github.com/Skymly/GitPulse/issues/448)
- [What product-level chrome looks like in public GitHub clients](https://github.com/Skymly/GitPulse/issues/449)
- [GitPulse visual language](https://github.com/Skymly/GitPulse/issues/450)
- [GitPulse interaction standard](https://github.com/Skymly/GitPulse/issues/451)
- [Rank surfaces for the UX spec](https://github.com/Skymly/GitPulse/issues/452)
- [Chrome prototype (tabs, list row, detail header)](https://github.com/Skymly/GitPulse/issues/453)
- [Prototype the top-ranked surfaces](https://github.com/Skymly/GitPulse/issues/454)
- [Dual-platform rule application](https://github.com/Skymly/GitPulse/issues/455)
- [Write the product-level UX Design Doc](https://github.com/Skymly/GitPulse/issues/456)
- Throwaway [`prototype/ux-chrome`](https://github.com/Skymly/GitPulse/tree/prototype/ux-chrome)
- Throwaway [`prototype/ux-surfaces`](https://github.com/Skymly/GitPulse/tree/prototype/ux-surfaces)
- [CONTEXT.md](../CONTEXT.md)
