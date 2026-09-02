# Research: public GitHub client chrome

| Field | Value |
|-------|-------|
| **Ticket** | [#449](https://github.com/Skymly/GitPulse/issues/449) (part of [#446](https://github.com/Skymly/GitPulse/issues/446)) |
| **Date** | 2026-09-02 |
| **Purpose** | Facts that inform public-facing product-level chrome. **Does not** pick GitPulse visual language, surface ranking, or Shell IA. **Does not** clone GitHub Desktop. |

## Method

Primary sources only. Every claim below follows to an owner (GitHub Desktop source/docs, first-party GitHub Mobile docs/changelog, Primer/Octicons, or a vendor-owned GitHub client). No review blogs, no third-party Android clones.

| Source | Why it counts |
|--------|----------------|
| [desktop/desktop](https://github.com/desktop/desktop) (`development`) + [GitHub Desktop docs](https://docs.github.com/en/desktop) | First-party Windows (and macOS) Git client; UI source is public |
| [GitHub Mobile docs](https://docs.github.com/en/get-started/using-github/github-mobile) + [GitHub Changelog](https://github.blog/changelog/) | First-party Android/iOS app; **no public UI source** — do not invent tab counts or row heights |
| [Primer](https://primer.style/product/getting-started/foundations/layout/) / [Octicons](https://primer.style/octicons/usage-guidelines/) | GitHub-owned product UI guidelines (written for github.com; still the owner’s rules for domain chrome) |
| [microsoft/vscode-pull-request-github](https://github.com/microsoft/vscode-pull-request-github) | Vendor-owned GitHub client that lives **inside host chrome**, not a standalone shell |

Out of scope for this note: FastHub / OpenHub / other unofficial clients; App Store screenshots; pixel-matching GitPulse to any of the above.

---

## 1. What these clients are (and are not)

Public GitHub clients do **not** share one shell. They share **GitHub domain objects** (repo, issue, PR, notification, check) and then put those objects in **platform chrome**.

**GitHub Desktop** is a local-Git workspace that *extends GitHub*, not a github.com clone and not an agnostic Git client ([`docs/process/what-is-desktop.md`](https://github.com/desktop/desktop/blob/development/docs/process/what-is-desktop.md)). Its chrome is Electron/React with Primer color tokens ([`app/styles/_variables.scss`](https://github.com/desktop/desktop/blob/development/app/styles/_variables.scss) imports `primer-support` color-system). It is **not** WinUI/Fluent.

**GitHub Mobile** is a first-party Android/iOS client for triage, collaboration, search, and push ([GitHub Mobile](https://docs.github.com/en/get-started/using-github/github-mobile)). Closed source. Chrome is **platform-native** (bottom destinations on phone), not Desktop’s toolbar.

**VS Code GitHub Pull Requests** is not a product shell. It adds a GitHub PR/issue **activity-bar viewlet** inside VS Code ([README](https://github.com/microsoft/vscode-pull-request-github/blob/main/README.md)). Useful as “GitHub domain lists can live in host chrome,” not as an IA to copy.

Implication for a public-facing GitPulse spec: keep GitPulse’s Shell IA (Repos / Notifications / Search / Settings). Take **rules** (empty/error, domain glyphs, platform chrome conventions, density that matches the host), not Desktop’s Changes/History workspace or Mobile’s Home/Inbox/Copilot destinations.

---

## 2. Navigation chrome

### 2.1 Two models: persistent destinations vs contextual workspace

| Client | Primary navigation | What it is for |
|--------|--------------------|----------------|
| **GitHub Mobile** | Bottom destinations named in first-party docs/changelog | Jump between **product areas** (Home, Inbox, Profile, and on Android a Copilot tab) |
| **GitHub Desktop** | Custom title bar + **repository bar** + left sidebar tabs | Operate on the **current local repository** |
| **VS Code GitHub PR** | VS Code activity bar viewlet + configurable query groups | Lists of PRs/issues **inside the editor** |

These are different jobs. Copying any one IA would be cloning, not informing.

### 2.2 Desktop: frameless Windows chrome + repository workspace

Root chrome (`id="desktop-app-chrome"`): TitleBar, then Welcome **or** App. App = Toolbar + Banner + Repository view + popups ([`app/src/ui/app.tsx`](https://github.com/desktop/desktop/blob/development/app/src/ui/app.tsx)).

**Windows title bar** ([`windows-menu-bar.md`](https://github.com/desktop/desktop/blob/development/docs/technical/windows-menu-bar.md), [`_title-bar.scss`](https://github.com/desktop/desktop/blob/development/app/styles/ui/window/_title-bar.scss), [`title-bar.tsx`](https://github.com/desktop/desktop/blob/development/app/src/ui/window/title-bar.tsx)):

- Frameless window; height `--win32-title-bar-height: 28px`; background `--win32-title-bar-background-color` (`gray-900`).
- Invertocat (`markGithub`) when not in welcome.
- Custom **File menu bar in the title bar** (WIN32). Alt access keys; Alt then letter opens a menu. Familiar menu-bar UX beat a hamburger after they tried both.
- 3px top/left resize handles because `-webkit-app-region: drag` otherwise blocks edge resize.
- Full-screen on Windows still shows the title bar **only while the menu is active**.

**Repository bar** (product docs call it the repository bar; code: `#desktop-app-toolbar`, `--toolbar-height: 50px`): Current repository | optional Worktree | Current branch | Push/Pull ([Creating your first repository](https://docs.github.com/en/desktop/overview/creating-your-first-repository-using-github-desktop); [`app.tsx` `renderToolbar`](https://github.com/desktop/desktop/blob/development/app/src/ui/app.tsx)). **Hidden** in the no-repositories blank slate.

The repository switcher is a **foldout list**, not a persistent hub tab. Filter + grouped list: Recent / GitHub.com owner / enterprise host / Other ([`group-repositories.ts`](https://github.com/desktop/desktop/blob/development/app/src/ui/repositories-list/group-repositories.ts)). Row height **29px**. Item: octicon + truncated name + trailing ahead/behind pill + uncommitted `dotFill` ([`repository-list-item.tsx`](https://github.com/desktop/desktop/blob/development/app/src/ui/repositories-list/repository-list-item.tsx)). Add button with `triangleDown`. `Ctrl+T` shows the list ([keyboard shortcuts](https://docs.github.com/en/desktop/overview/github-desktop-keyboard-shortcuts)).

**Repo workspace:** resizable **left sidebar** + main. Sidebar `TabBar` **Changes | History** (`--tab-bar-height: 29px`). Selected tab: **inset 3px blue underline**. Changes can show a count badge ([`repository.tsx` `renderTabs`](https://github.com/desktop/desktop/blob/development/app/src/ui/repository.tsx); [`_tab-bar.scss`](https://github.com/desktop/desktop/blob/development/app/styles/ui/_tab-bar.scss)). `Ctrl+1` Changes, `Ctrl+2` History.

`TabBar` types ([`tab-bar-type.ts`](https://github.com/desktop/desktop/blob/development/app/src/ui/tab-bar-type.ts)): `Tabs` (underline), `Switch` (filled selected), `Vertical` (preferences sidebar).

### 2.3 Mobile: bottom destinations, Settings one level down

First-party docs name **Profile** in “the bottom of the app” / “bottom menu.” Long-press Profile opens the account switcher; Settings is Profile → gear ([GitHub Mobile](https://docs.github.com/en/get-started/using-github/github-mobile); [multi-account changelog](https://github.blog/changelog/2023-08-15-log-in-with-multiple-github-accounts-on-github-mobile/)).

Changelog-named destinations (not a complete tab inventory — source is closed):

- **Home**, **Inbox** ([Android nav refresh](https://github.blog/changelog/2026-03-20-a-smoother-navigation-experience-in-github-mobile-for-android/)).
- **Copilot** as an Android **navigation-bar tab** ([Copilot tab](https://github.blog/changelog/2026-04-01-github-mobile-stay-in-flow-with-a-refreshed-copilot-tab-and-native-session-logs/)).
- Create repository: Android from **Home** or Profile **Repositories**; iOS from Home or Profile `+` ([create repositories](https://github.blog/changelog/2026-05-11-create-repositories-on-the-go-with-github-mobile/)).

Android nav refresh (2026-03-20): bottom navigation “more consistently available **where it matters**”; tabs preserve place. That is an explicit **not-always-on-every-nested-screen** rule, without publishing a per-page map.

Inbox **syncs with the web** notifications inbox ([Configuring notifications](https://docs.github.com/en/subscriptions-and-notifications/get-started/configuring-notifications)). Push kinds: mentions, assignments, review requests, deployment approvals. **Focused** filter on Inbox ([October 2024 mobile update](https://github.blog/changelog/2024-10-14-whats-new-in-mobile-october-update/)). Double-tap the active Android tab icon to jump to top ([September 2024](https://github.blog/changelog/2024-09-16-whats-new-in-mobile-september-update/)).

### 2.4 Primer (github.com rules that still describe “share rules, change layout”)

[Layout](https://primer.style/product/getting-started/foundations/layout/): `narrow` &lt;768px = 1 column (“mobile”); `regular` ≥768 = up to 2 columns; `wide` ≥1400 = up to 3. Multi-column pages **break into multiple views** on narrow. App header is **not** sticky. Context region is **not** a full breadcrumb (`owner/repo`, not `owner/repo/Issues`).

[Navigation](https://primer.style/product/ui-patterns/navigation/): UnderlineNav if the URL changes; UnderlinePanels if it does not. NavList for parent-detail sidebars. Streamlined choices. Narrow sidebar → index page + back, or overflow into ActionMenu / bottom sheet.

### 2.5 Host-chrome GitHub lists (VS Code)

Default PR tree queries: **Waiting For My Review**, **Assigned To Me**, **Created By Me** (`githubPullRequests.queries` in the [extension README](https://github.com/microsoft/vscode-pull-request-github/blob/main/README.md)). The chrome (activity bar, viewlet, tree) is VS Code’s, not GitHub Desktop’s and not Fluent.

---

## 3. List density

Public clients do **not** share a row height. They share “lists of GitHub objects with a leading glyph + title + trailing status.”

**Desktop (compact desktop, not 44px mobile):**

| Token / measure | Value | Where |
|-----------------|-------|--------|
| `--font-size` | 12px | [`_variables.scss`](https://github.com/desktop/desktop/blob/development/app/styles/_variables.scss) |
| `--spacing` | 10px | same |
| `--button-height` | 25px | same |
| Repository list `RowHeight` | 29px | [`repositories-list.tsx`](https://github.com/desktop/desktop/blob/development/app/src/ui/repositories-list/repositories-list.tsx) |
| `--tab-bar-height` | 29px | variables + sidebar tabs |
| `--toolbar-height` | 50px | toolbar |
| `--win32-title-bar-height` | 28px | title bar |
| Default Octicon height | 16px | [`octicon.tsx`](https://github.com/desktop/desktop/blob/development/app/src/ui/octicons/octicon.tsx) |

File-status list uses color + octicon, not a second density scale: red removed, yellow modified, green added ([Committing and reviewing changes](https://docs.github.com/en/desktop/making-changes-in-a-branch/committing-and-reviewing-changes-to-your-project-in-github-desktop)).

**Primer ActionList** (web, but owner-owned list chrome): leading/trailing visuals, group headings, size `medium` \| `large`, danger variant, inactive item replaces the visual with an alert icon ([ActionList](https://primer.style/product/components/action-list/)).

**Mobile:** no public row-height tokens. Changelog talks about **filters designed for mobile** (Copilot session filters) and Dynamic Type / large-font work, not a 29px desktop row. Do not invent a phone density from Desktop.

Pattern that informs without cloning: **desktop chrome is dense; phone chrome is platform-sized; the same domain object (repo, PR, notification) changes density with the host.**

---

## 4. Empty / error

### 4.1 Empty is a heading + why + actions

**Primer Blankslate:** heading, description, optional visual, primary action, secondary action; sizes small/medium/large ([Blankslate](https://primer.style/product/components/blankslate/)).

**Desktop no-repositories blank slate** ([`no-repositories-view.tsx`](https://github.com/desktop/desktop/blob/development/app/src/ui/no-repositories/no-repositories-view.tsx)):

- Heading: “Let's get started!”
- Why: “Add a repository to GitHub Desktop to start collaborating”
- Cloneable-repo list when signed in
- Icon buttons: tutorial `mortarBoard`, clone `repoClone`, create `plus`, add existing `fileDirectory`
- `lightBulb` ProTip (drag-and-drop)

Toolbar and banners are **omitted** in this state (`renderToolbar` / `renderBanner` return null).

**Filter miss** in the repo foldout: `empty-no-repo.svg` + “Sorry, I can't find that repository” + ProTip with `Ctrl+O` / `Ctrl+Shift+O`.

**No local changes** ([`no-changes.tsx`](https://github.com/desktop/desktop/blob/development/app/src/ui/changes/no-changes.tsx)): paper-stack illustration + heading “No local changes” + “friendly suggestions for what to do next.” Primary suggested actions (publish / push / pull / open PR) plus secondary (editor, file manager, View on GitHub). Each action shows **menu path + keyboard shortcut** — empty is a next-step surface, not a void.

**Mobile:** when a workflow-run list has no runs, “an empty state displays on the screen” ([October 2024](https://github.blog/changelog/2024-10-14-whats-new-in-mobile-october-update/)). No public copy deck beyond that.

### 4.2 Errors: inline when possible, queued modal for app-wide, banner for non-blocking

Desktop dialogs ([`dialogs.md`](https://github.com/desktop/desktop/blob/development/docs/technical/dialogs.md)):

- Prefer **inline** errors from the action that failed (`DialogError` must be the **first child** of `Dialog`).
- Copy: “Avoid using the term 'Error' inside the text” — the chrome already says it.
- `DialogError` pairs copy with a **stop** octicon, `role="alert"` ([`error.tsx`](https://github.com/desktop/desktop/blob/development/app/src/ui/dialog/error.tsx)).

App-wide errors ([`app-error.tsx`](https://github.com/desktop/desktop/blob/development/app/src/ui/app-error.tsx)): one modal at a time from a queue; `type="error"`; `role="alertdialog"`; titles like “Clone failed” / “Failed to push”; retry when a retry action exists; auth failures offer **Open options**. Backdrop not dismissable.

**Missing resource** ([`missing-repository.tsx`](https://github.com/desktop/desktop/blob/development/app/src/ui/missing-repository.tsx)): “Can't find …” + last path + Check again, then **Locate… / Clone Again / Remove**. Unsafe path is a different recovery (Trust repository).

**Banners** sit between toolbar and content, one at a time, `role="alert"`; dismiss control is `octicons.x` labelled “Dismiss this message” ([`banner.tsx`](https://github.com/desktop/desktop/blob/development/app/src/ui/banners/banner.tsx)). None on the blank slate.

Mobile changelog notes clearer error messages on the check-log / Actions log screens; it does not publish a modal-vs-inline taxonomy.

---

## 5. Iconography

**Octicons usage guidelines** ([primer.style/octicons](https://primer.style/octicons/usage-guidelines/)):

- Sizes **12 / 16 / 24 only**. Do not resize (stroke weight breaks).
- Supplement text; do not replace it when meaning is not obvious.
- Functional colors: `info` → accent, `check` → success, `x` → danger, `alert` → attention.
- **Locked domain icons** (do not recast meaning): `issue-opened` / `issue-closed` / `issue-reopened` / `issue-draft`; `git-pull-request` / `git-pull-request-closed` / `git-pull-request-draft` / `git-merge`; `repo` vs `repo-locked` (not `lock`); `git-commit`; Invertocat / wordmark per brand guidelines.
- Outline vs fill **pairs**: star/star-fill, bell/bell-slash, eye/eye-closed, bookmark/bookmark-slash, check-circle / x-circle-fill (pass/fail). Do not invent pairs.
- Decorative icons hidden from AT; meaningful icons `role="img"` + name; 3:1 contrast unless decorative.

**Desktop applies those glyphs to local-Git states** (same library, local meaning):

`iconForRepository` ([`repository.ts`](https://github.com/desktop/desktop/blob/development/app/src/ui/octicons/repository.ts)): cloning `desktopDownload`; missing `alert`; local-only `deviceDesktop`; private `lock`; fork `repoForked`; else `repo`.

`iconForStatus` ([`status.ts`](https://github.com/desktop/desktop/blob/development/app/src/ui/octicons/status.ts)): `diffAdded` / `diffModified` / `diffRemoved` / `diffRenamed`; conflicted `alert` or `check` when markers are resolved.

Octicon component: `aria-hidden` unless `title`; default height 16; pick closest natural height.

Desktop also uses Primer-ish **status colors** in CSS (`--pr-open-icon-color` green, `--pr-draft-icon-color` gray, `--status-success-color` / `--status-error-color`) — domain color, not a unique icon set.

---

## 6. Platform rules, not layout clones

The public clients already do **rule parity without layout parity**. That matches the map destination (Windows/Android share rules, not layouts; Tray/Toast/Mica stay Windows-only and are **GitPulse** constraints, not Desktop features).

**Desktop Win vs Mac (same app, different chrome details):**

| Rule | Windows | macOS |
|------|---------|--------|
| Dialog button order | Affirmative then dismiss (OK, Cancel) | Dismiss then affirmative (Cancel, OK) ([`button-order.md`](https://github.com/desktop/desktop/blob/development/docs/technical/button-order.md)) |
| Destructive default | Safest button is default on **both**; never default the destroy action | same |
| Preferences chrome | File → **Options…**; dialog title **Options** | App menu **Settings…**; dialog title **Settings** ([`preferences.tsx`](https://github.com/desktop/desktop/blob/development/app/src/ui/preferences/preferences.tsx), [`build-default-menu.ts`](https://github.com/desktop/desktop/blob/development/app/src/main-process/menu/build-default-menu.ts)) |
| Menu access | In-title-bar menu + Alt access keys | System menu bar |
| Keyboard | `Ctrl+1` / `Ctrl+2`, `Ctrl+,` → Options | `Cmd+1` / `Cmd+2`, `Cmd+,` → Settings |

**Mobile iOS vs Android (same product areas, different chrome):**

- iOS 26: **Liquid Glass**, refined tab bar, system-native visuals ([iOS 26 changelog](https://github.blog/changelog/2025-09-14-github-mobile-now-supports-ios-26-with-refined-visuals-and-smoother-navigation/)) — **iOS-only**.
- Android: its **own** bottom-nav refresh months later ([2026-03-20](https://github.blog/changelog/2026-03-20-a-smoother-navigation-experience-in-github-mobile-for-android/)); Copilot as a nav-bar tab on Android.
- Create-repo **entry points** differ (iOS `+` vs Android Home / Profile Repositories) while the **create fields** (name, visibility, README, gitignore, license) are the same.
- Shared **rules**: Inbox syncs with web; Profile is the account/settings root; push kinds are the same.

**Primer narrow vs regular:** same page **rules**, layout becomes stacked pages / bottom sheet / vertical stack. Do not keep a two-column list-detail on a 1-column viewport.

---

## 7. What this does *not* decide for GitPulse

This note does not pick Fluent tokens, rank surfaces, or change Shell IA. It also does not recommend cloning:

- Desktop’s Changes/History local-Git workspace, repository foldout, or Electron title bar
- Mobile’s Home / Inbox / Copilot destinations or iOS Liquid Glass
- VS Code’s activity-bar viewlet
- Primer’s github.com app header as a MAUI shell

Facts that **can** feed a later product-level spec without becoming a clone list:

1. **Persistent primary destinations** (GitPulse already has four Shell tabs) vs **contextual workspace chrome** (toolbars/tabs that appear only inside a selected repo). Desktop hides the repository bar when there is no repo; Mobile keeps bottom destinations “where it matters.”
2. **Density follows the host**: ~29px / 12px / 16px octicons on a dense desktop Git client; phone uses platform bottom nav and platform tap sizes. Same domain object, different density.
3. **Empty = heading + reason + primary/secondary actions** (optionally menu path / shortcut). Not a blank list.
4. **Error layers**: inline in the form; queued app-wide modal (`alertdialog`) with recovery; non-blocking banner with dismiss; missing-resource recovery (locate / retry / remove).
5. **Domain octicons + functional color**; do not invent GitHub state glyphs or resize 16px icons to 24px.
6. **Platform chrome conventions** (Windows menu/button order/Options naming; Android bottom navigation) without pixel-matching layouts. Windows-only presence (tray, toast, Mica) is a GitPulse platform rule, not something Desktop demonstrates.

---

## Pointers

| Kind | Where |
|------|--------|
| Desktop product purpose | [what-is-desktop.md](https://github.com/desktop/desktop/blob/development/docs/process/what-is-desktop.md) |
| Desktop Windows menu bar | [windows-menu-bar.md](https://github.com/desktop/desktop/blob/development/docs/technical/windows-menu-bar.md) |
| Desktop dialogs / button order | [dialogs.md](https://github.com/desktop/desktop/blob/development/docs/technical/dialogs.md), [button-order.md](https://github.com/desktop/desktop/blob/development/docs/technical/button-order.md) |
| Desktop repository bar / Changes+History | [Creating your first repository](https://docs.github.com/en/desktop/overview/creating-your-first-repository-using-github-desktop), [Committing and reviewing changes](https://docs.github.com/en/desktop/making-changes-in-a-branch/committing-and-reviewing-changes-to-your-project-in-github-desktop), [Keyboard shortcuts](https://docs.github.com/en/desktop/overview/github-desktop-keyboard-shortcuts) |
| Desktop chrome source | [`app.tsx`](https://github.com/desktop/desktop/blob/development/app/src/ui/app.tsx), [`_variables.scss`](https://github.com/desktop/desktop/blob/development/app/styles/_variables.scss) |
| GitHub Mobile | [docs](https://docs.github.com/en/get-started/using-github/github-mobile), [notifications](https://docs.github.com/en/subscriptions-and-notifications/get-started/configuring-notifications#managing-your-notification-settings-with-github-mobile), [Android nav](https://github.blog/changelog/2026-03-20-a-smoother-navigation-experience-in-github-mobile-for-android/), [iOS 26](https://github.blog/changelog/2025-09-14-github-mobile-now-supports-ios-26-with-refined-visuals-and-smoother-navigation/) |
| Primer / Octicons | [Layout](https://primer.style/product/getting-started/foundations/layout/), [Navigation](https://primer.style/product/ui-patterns/navigation/), [Blankslate](https://primer.style/product/components/blankslate/), [ActionList](https://primer.style/product/components/action-list/), [Octicons usage](https://primer.style/octicons/usage-guidelines/) |
| VS Code GitHub PR | [README](https://github.com/microsoft/vscode-pull-request-github/blob/main/README.md) |
