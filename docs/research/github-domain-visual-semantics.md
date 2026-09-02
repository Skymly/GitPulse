# Research: GitHub domain visual semantics

| Field | Value |
|-------|-------|
| **Ticket** | [#448](https://github.com/Skymly/GitPulse/issues/448) (part of [#446](https://github.com/Skymly/GitPulse/issues/446)) |
| **Date** | 2026-09-02 |
| **Purpose** | Facts that a later product-level UX spec must not contradict for Issue / PR / Draft / merged, Checks, notification reasons, and labels. **Does not** pick GitPulse accent, type, chrome, or token names. |

## Method

Primary sources only. Every claim follows to an owner (Primer, GitHub Docs, github.blog changelog, or Primer React / ViewComponents source GitHub owns) or to GitPulse files on this checkout. No third-party palettes, no blog roundups.

| Source | Why it counts |
|--------|----------------|
| [Primer color usage](https://primer.style/product/getting-started/foundations/color-usage/) + [color primitives](https://primer.style/product/primitives/color/) | Owner color roles and theme-dependent CSS outputs |
| [Octicons usage](https://primer.style/octicons/usage-guidelines/) | Owner icon + color pairing for Issue / PR / pass-fail |
| [Primer React `StateLabel`](https://github.com/primer/react/blob/main/packages/react/src/StateLabel/StateLabel.tsx) + [CSS](https://github.com/primer/react/blob/main/packages/react/src/StateLabel/StateLabel.module.css) + [stories](https://github.com/primer/react/blob/main/packages/react/src/StateLabel/StateLabel.features.stories.tsx) | Owner state badge statuses, icons, backgrounds, visible labels |
| [Primer ViewComponents State](https://primer.style/view-components/lookbook/inspect/primer/beta/state/states) | Owner Rails `State` schemes (default/draft, open, closed, merged) |
| [Primer Label](https://primer.style/product/components/label/) | Owner **metadata** Label (not per-repo issue labels) |
| [GitHub Docs](https://docs.github.com/) REST / GraphQL / product pages cited below | Owner meanings for states, checks, notification reasons, default labels |
| [GitHub Changelog 2021-10-26](https://github.blog/changelog/2021-10-26-updates-to-our-issue-status-icons-and-colors/) | Owner rationale for closed-issue purple |
| GitPulse [`Colors.xaml`](../../src/GitPulse.App/Resources/Styles/Colors.xaml) and the pages listed in §6 | What this app already hardcodes |

Out of scope: picking GitPulse visual language; cloning github.com chrome; inventing a Checks conclusion → hex table.

---

## 1. Color roles (Primer)

[Color usage](https://primer.style/product/getting-started/foundations/color-usage/) ties semantic color to a **role**. Base tokens “should never be used directly in code or design.” Functional tokens change with color mode.

Documented roles:

| Role | Usage (verbatim sense) |
|------|------------------------|
| `accent` | Links, selected, active, and focus states, and neutral information |
| `success` | Primary buttons, positive messaging and successful states |
| `attention` | Warning states, active processes such as **queued PRs and tests in progress** |
| `danger` | Danger buttons and error states |
| `open` | Open tasks, PRs or workflows |
| `closed` | Closed tasks, PRs or workflows |
| `done` | Completed tasks, PRs or workflows |
| `sponsors` | Text and icons related to GitHub Sponsors |

`draft` is **not** in that role table. Primer primitives still ship `--fgColor-draft`, `--bgColor-draft-emphasis`, `--borderColor-draft-emphasis`.

Accessibility: do not rely on color alone to show state ([Color considerations](https://primer.style/accessibility/design-guidance/color-considerations/); WCAG SC 1.4.1). Octicons: “Certain Octicons are designed for specific use cases and their meaning should not be changed.” Predefined color variables “should not be changed unless placed on colorful/dark backgrounds.”

### 1.1 Light-theme token *outputs* (not GitPulse tokens)

[Primitives color](https://primer.style/product/primitives/color/) documents CSS variables and the **site’s active theme** output. Values change by theme. The table below is the light-theme snapshot on that page; it is **not** a GitPulse palette.

| Role | `--fgColor-*` | `--bgColor-*-emphasis` | Same-as in this theme |
|------|---------------|------------------------|------------------------|
| open | `#1a7f37` | `#1f883d` | same fg/bg family as **success** |
| success | `#1a7f37` | `#1f883d` | |
| closed | `#d1242f` | `#cf222e` | same family as **danger** |
| danger | `#d1242f` | `#cf222e` | |
| done | `#8250df` | `#8250df` | purple |
| draft | `#59636e` | `#59636e` | same as muted / neutral |
| attention | `#9a6700` | `#9a6700` | |
| muted / neutral | `#59636e` | `#59636e` (neutral emphasis) | |

---

## 2. Issue / PR / Draft / merged

### 2.1 Octicons (meaning + color)

From [Octicons usage — specific use cases](https://primer.style/octicons/usage-guidelines/):

| Icon | Context | Color variable | Usage |
|------|---------|----------------|-------|
| `issue-opened` | Issue | `fg.success` | Newly opened issue that needs attention |
| `issue-closed` | Issue | **`fg.done`** | A done/closed issue |
| `issue-reopened` | Issue | `fg.success` | Reopened issue |
| `issue-draft` | Issue | `fg.muted` | Draft issue |
| `git-pull-request` | Pull request | `fg.success` | Unmerged PR that needs attention |
| `git-pull-request-closed` | Pull request | **`fg.danger`** | Closed PR that **wasn’t merged** |
| `git-pull-request-draft` | Pull request | `fg.muted` | Draft PR |
| `git-merge` | Pull request | `fg.done` | Merged request |
| `check` | (generic) | `fg.success` | Successful, passing, or positive result |
| `x` | (generic) | `fg.danger` | Error, danger, or negative result |
| `alert` | (generic) | `fg.attention` | Warning |
| `check-circle` / `x-circle-fill` | pair | — | **Pass / Fail** (predefined pair; do not substitute `x`) |

Owner rationale for closed-**issue** purple: [Updates to our issue status icons and colors](https://github.blog/changelog/2021-10-26-updates-to-our-issue-status-icons-and-colors/) (2021-10-26). Closed issue icon rolled from red to purple because red implied error; “a bunch of closed issues is usually a good thing.” Closed **unmerged** PRs stay danger/red in the Octicons table above.

### 2.2 Primer State vs StateLabel

**ViewComponents `Primer::Beta::State`** ([lookbook + CSS](https://primer.style/view-components/lookbook/inspect/primer/beta/state/states)): schemes default (shared with `--draft`), `open`, `closed`, `merged`.

| Scheme | Background token |
|--------|------------------|
| default / `State--draft` | `bgColor-draft-emphasis` (fallback `bgColor-neutral-emphasis`) |
| `State--open` | `bgColor-open-emphasis` |
| `State--merged` | `bgColor-done-emphasis` |
| `State--closed` | `bgColor-closed-emphasis` (**red** in light theme — PR-closed, not closed-issue) |

**Primer React `StateLabel`** is the finer badge. `status` drives icon + background; **children text** in owner stories is title case (`Open` / `Closed` / `Merged` / `Draft` / `Queued`), not REST `open`.

| `status` | Icon | `aria-label` / `labelMap` | Background |
|----------|------|---------------------------|------------|
| `issueOpened` / `pullOpened` / `open` | open icons (`open` has none) | Issue / Pull request / (generic empty) | `bgColor-open-emphasis` |
| `issueClosed` | `IssueClosed` | Issue | **`bgColor-done-emphasis` (purple)** |
| `issueClosedNotPlanned` | `SkipIcon` | **Issue, not planned** | **`bgColor-neutral-emphasis` (gray)** |
| `pullClosed` | `GitPullRequestClosed` | Pull request | **`bgColor-closed-emphasis` (red)** |
| `pullMerged` | `GitMerge` | Pull request | `bgColor-done-emphasis` |
| `draft` / `issueDraft` | draft icons | Pull request / Issue | `bgColor-draft-emphasis` (gray) |
| `pullQueued` | `GitMergeQueue` | Pull request | `bgColor-attention-emphasis` |
| `closed` (generic) | none | empty | `bgColor-done-emphasis` (issue-style purple) |

Stories: `issueClosedNotPlanned` still **shows** children `"Closed"`; the Skip icon’s accessible name is `"Issue, not planned"`. Generic `closed` is purple (issue-style). ViewComponents `State--closed` is red (PR-style). Those two “closed” schemes are not the same object.

### 2.3 Product / API meaning (labels, not colors)

**Issues.** Close when bugs are fixed, feedback is acted on, **or work is not planned** ([Closing an issue](https://docs.github.com/en/issues/tracking-your-work-with-issues/administering-issues/closing-an-issue)). REST update-issue `state` is `open` \| `closed`. `state_reason` (ignored unless `state` changes): `completed`, `not_planned`, `duplicate`, `reopened`, or `null` ([Update an issue](https://docs.github.com/en/rest/issues/issues?apiVersion=2022-11-28#update-an-issue)).

GraphQL `IssueState`: `OPEN`, `CLOSED`. `IssueStateReason`: `COMPLETED`, `DUPLICATE`, `NOT_PLANNED`, `REOPENED`. `IssueClosedStateReason`: `COMPLETED`, `DUPLICATE`, `NOT_PLANNED` ([Issues GraphQL](https://docs.github.com/en/graphql/reference/issues)).

**Pull requests.** REST update: “State of this Pull Request. Either `open` or `closed`.” Separate booleans `draft` and `merged` on the payload ([Update a pull request](https://docs.github.com/en/rest/pulls/pulls?apiVersion=2022-11-28#update-a-pull-request), [Get a pull request](https://docs.github.com/en/rest/pulls/pulls?apiVersion=2022-11-28#get-a-pull-request)). GraphQL `PullRequestState` ([Pull requests GraphQL](https://docs.github.com/en/graphql/reference/pulls#enum-pullrequeststate)):

| Value | Description |
|-------|-------------|
| `OPEN` | Still open |
| `CLOSED` | Closed **without** being merged |
| `MERGED` | Closed **by** being merged |

**Draft PR.** “No one can merge the pull request until you mark the pull request as ready for review again.” ([Changing the stage of a pull request](https://docs.github.com/en/pull-requests/how-tos/create-pull-requests/changing-the-stage-of-a-pull-request)).

---

## 3. Checks — documented meaning; no official color table

[Status checks](https://docs.github.com/en/pull-requests/reference/status-checks) and [Using the REST API to interact with checks](https://docs.github.com/en/rest/guides/using-the-rest-api-to-interact-with-checks) list statuses and conclusions. **Neither page maps them to Primer roles or hex.**

Statuses (check run / suite): `completed`, `expected`, `failure`, `in_progress`, `pending`, `queued`, `requested`, `startup_failure`, `waiting`. Some are GitHub Actions–only.

Conclusions when status is `completed`:

| Conclusion | Docs meaning |
|------------|----------------|
| `success` | Completed successfully. A successful conclusion usually does not block merging. |
| `failure` | Failed. Failure / timeout / action-required usually need review before merge. |
| `timed_out` | Timed out. |
| `action_required` | Provided required actions on completion. |
| `cancelled` | Cancelled before completion. |
| `neutral` | Neutral result; treated as success for **dependent** GitHub Actions checks. |
| `skipped` | Skipped; treated as success for dependent Actions checks. A skipped **job** reports status **“Success”** and does not block merge even if required. |
| `stale` | Incomplete longer than 14 days; **only GitHub** can set. Docs: “appears on GitHub as stale with .” — the icon is not named in the prose. |
| `startup_failure` | Listed on check **suites** (Actions). |

Annotation `annotation_level`: `notice`, `warning`, `failure`. No color table.

What **can** be inferred without inventing a conclusion palette: Primer `success` / Octicon `check` / pass pair for passing; `danger` / `x` / fail pair for failure; `attention` for queued / in progress; `fg.muted` for draft-like incomplete. That is role inference, not an owner table.

---

## 4. Notification reasons — meanings; no official colors

REST `reason` on notification threads ([About notification reasons](https://docs.github.com/en/rest/activity/notifications?apiVersion=2022-11-28#about-notification-reasons)):

| Reason | Description |
|--------|-------------|
| `approval_requested` | Requested to review and approve a deployment |
| `assign` | Assigned to the issue |
| `author` | Created the thread |
| `ci_activity` | A GitHub Actions workflow run you triggered completed |
| `comment` | Commented on the thread |
| `invitation` | Accepted an invitation to contribute |
| `manual` | Subscribed to the thread |
| `member_feature_requested` | Org members requested to enable a feature |
| `mention` | Specifically @mentioned |
| `review_requested` | You or a team you are on were requested to review a PR |
| `security_advisory_credit` | Credited on a security advisory |
| `security_alert` | Dependabot / vulnerability alert |
| `state_change` | Changed thread state (close issue / merge PR) |
| `subscribed` | Watching the repository |
| `team_mention` | On a team that was mentioned |

`reason` is per-thread and can change (example: `author` → `mention` after an @mention).

Inbox UI: “Your inbox shows the `reason` you're receiving a notification **as a label**, such as `mention`, `subscribed`, or `review requested`.” Filter queries use hyphens (`reason:review-requested`) vs REST snake_case ([About notifications](https://docs.github.com/en/subscriptions-and-notifications/concepts/about-notifications)).

**No Primer mapping of reason → color.** Primer [Label](https://primer.style/product/components/label/) is a **metadata** chip (`default` / `primary` / `secondary` / `accent` / `success` / `attention` / `severe` / `danger` / `done` / `sponsors`), distinct from per-repo issue labels and from notification reasons.

---

## 5. Labels (per-repo issue/PR labels)

Default **names and descriptions only** — no colors in the product table ([Managing labels](https://docs.github.com/en/issues/using-labels-and-milestones-to-track-work/managing-labels)):

| Label | Description |
|-------|-------------|
| `accessibility` | Barrier affecting people with disabilities |
| `bug` | Unexpected problem or unintended behavior |
| `documentation` | Need for improvements or additions to documentation |
| `duplicate` | Similar issues, pull requests, or discussions |
| `enhancement` | New feature requests |
| `good first issue` | Good issue for first-time contributors |
| `help wanted` | Maintainer wants help |
| `invalid` | No longer relevant |
| `question` | Needs more information |
| `wontfix` | Work will not continue |

Creating/editing: customize the hexadecimal number or pick a random color. REST create-label: “The name and color parameters are required.” Color is a hex code **without** leading `#` ([Create a label](https://docs.github.com/en/rest/issues/labels?apiVersion=2022-11-28#create-a-label)). Example payloads (`bug` `f29513`, `enhancement` `a2eeef`) are **examples**, not a documented default palette. Do not treat third-party “GitHub default label colors” lists as owner docs.

---

## 6. What GitPulse already hardcodes

Comment in [`Colors.xaml`](../../src/GitPulse.App/Resources/Styles/Colors.xaml): “Semantic state colors (issue/PR state badges, error banners, success).”

| Key | Hex | Used as |
|-----|-----|---------|
| `Primary` | `#512BD4` | Chrome / template purple — **not** a GitHub domain role |
| `Green100` | `#DCFCE7` | Success banners (Settings) |
| `Green700` | `#15803D` | Open / merge / “success-looking” text |
| `Green900` | `#14532D` | Success banner text |
| `Purple700` | `#7E22CE` | Closed issue, closed PR, merged, Close buttons, pre-release |
| `Orange900` | `#9A3412` | Draft PR label |
| `Red100` / `Red900` | `#FEE2E2` / `#991B1B` | Error banners; `Red900` also deletions |
| `Gray500` / `Gray600` | `#6E6E6E` / `#404040` | Meta text, notification reason, Search state, annotation level |

Android [`colors.xml`](../../src/GitPulse.App/Platforms/Android/Resources/values/colors.xml): `colorPrimary` / `colorPrimaryDark` / `colorAccent` purple only — **no** domain state colors.

| Surface | Hardcoded behavior |
|---------|--------------------|
| Issues list + detail | REST `state` string as the visible label. `open` → `Green700`; `closed` → `Purple700`. Close button `Purple700`; Reopen `Green700`. No draft-issue UI. [`Issue`](../../src/GitPulse.Core/Models/Issue.cs) has **no** `state_reason`. |
| PRs list + detail | REST `state` `open` → `Green700`, `closed` → `Purple700` **even when merged**. Extra labels: `"draft"` italic `Orange900` if `Draft`; `"merged"` `Purple700` if `Merged`. Closed-unmerged stays purple. Close `Purple700`; Reopen `Green700`; Merge `Green700`; “✓ Merged successfully” `Green700`. |
| Merge status copy | [`PullRequestLifecycle`](../../src/GitPulse.ViewModels/PullRequestLifecycle.cs): `"Merged"`, `"Closed"`, `"Draft — needs to be marked ready for review"`, mergeable strings. Rendered as **`Primary` text**, not state colors. |
| Checks / Actions | Check Run detail: `Status:` / `Conclusion:` **no semantic color**. Workflow Runs list + Workflow Run job `Conclusion` always `Green700`. Gate Rollup on PR/commit detail is `Primary` (`No checks` / `Pending` / `Success` / `Failure`). [`HeadGateRollup`](../../src/GitPulse.ViewModels/HeadGateRollup.cs) treats `failure` / `timed_out` / `cancelled` / `startup_failure` / `action_required` as failing — **logic only**. Annotations: `AnnotationLevel` `Gray600`. |
| Notifications | `Reason` shown as raw REST string `Gray500`. Unread ellipse `Primary`. `Subject.Type` `Primary`. Polling ellipse `Green700` (not a domain state). |
| Issue/PR labels | [`Label.Color`](../../src/GitPulse.Core/Models/Label.cs) exists. UI is comma-separated **names** in an Entry (`Placeholder="bug, enhancement, ..."`). Fill hex is unused. |
| Search | Issue/PR `State` is `Gray500` (not Green/Purple). |
| Diff / commit stats | `Green700` additions / `Red900` deletions (not issue/PR state). |
| Settings | Credential success `Green100` / `Green900` (not domain). Errors `Red100` / `Red900` everywhere. |
| Releases (repo detail) | `"draft"` `Gray500`; `"pre-release"` `Purple700` — **release**, not issue/PR. |

---

## 7. Observed mismatches (facts, not a language pick)

These are contradictions or gaps vs owner semantics. They are **not** a recommendation for GitPulse tokens.

1. Closed **unmerged** PR is `Purple700`, same as merged and closed issue. GitHub: danger/red for unmerged close; done/purple for merge and for closed-as-completed issue.
2. Draft PR is `Orange900`. GitHub draft is muted/neutral gray (`fgColor-draft` / `fg.muted`), not `attention`.
3. PR `state=closed` is shown as REST `"closed"` in purple even when `Merged=true` (merged is an extra label). GraphQL would be `MERGED` with visible label `"Merged"`.
4. Closed-as-not-planned is a distinct GitHub badge (neutral + Skip icon + accessible name “Issue, not planned”). GitPulse has no `state_reason`.
5. Checks conclusions are uncolored except Workflow Runs `Conclusion`, which is always `Green700` (including failure). Gate Rollup is `Primary`, not success/danger/attention.
6. Notification reasons have **no** GitHub colors. GitPulse shows the raw REST reason as `Gray500`, not an inbox-style reason label.
7. Per-repo label hex from the API is unused; only names are shown.
8. GitPulse `Green700` `#15803D` and `Purple700` `#7E22CE` are not Primer light-theme `--fgColor-open` `#1a7f37` / `--fgColor-done` `#8250df`. Hex mismatch only — this note does not pick replacements.

---

## What this does *not* decide for GitPulse

This note does not pick Fluent/WinUI chrome, accent, type ramp, or XAML token names. It does not rank surfaces. It does not require cloning github.com StateLabel chrome. Later UX work can keep GitPulse chrome while still avoiding the owner semantics in §2–§5 (closed-unmerged ≠ merged, draft ≠ attention, not-planned ≠ completed, checks conclusions are not all success).

---

## Pointers

| Kind | Where |
|------|--------|
| Color roles | [Color usage](https://primer.style/product/getting-started/foundations/color-usage/) |
| Token outputs (theme-dependent) | [Primitives color](https://primer.style/product/primitives/color/) |
| Color + state a11y | [Color considerations](https://primer.style/accessibility/design-guidance/color-considerations/) |
| Octicons | [Usage guidelines](https://primer.style/octicons/usage-guidelines/) |
| Closed-issue purple | [Changelog 2021-10-26](https://github.blog/changelog/2021-10-26-updates-to-our-issue-status-icons-and-colors/) |
| StateLabel | [`StateLabel.tsx`](https://github.com/primer/react/blob/main/packages/react/src/StateLabel/StateLabel.tsx), [`StateLabel.module.css`](https://github.com/primer/react/blob/main/packages/react/src/StateLabel/StateLabel.module.css), [stories](https://github.com/primer/react/blob/main/packages/react/src/StateLabel/StateLabel.features.stories.tsx) |
| ViewComponents State | [Lookbook](https://primer.style/view-components/lookbook/inspect/primer/beta/state/states) |
| Primer metadata Label | [Label](https://primer.style/product/components/label/) |
| Issue close / reasons | [Closing an issue](https://docs.github.com/en/issues/tracking-your-work-with-issues/administering-issues/closing-an-issue), [Update an issue](https://docs.github.com/en/rest/issues/issues?apiVersion=2022-11-28#update-an-issue), [GraphQL issues](https://docs.github.com/en/graphql/reference/issues) |
| PR state / draft | [Update a pull request](https://docs.github.com/en/rest/pulls/pulls?apiVersion=2022-11-28#update-a-pull-request), [PullRequestState](https://docs.github.com/en/graphql/reference/pulls#enum-pullrequeststate), [Changing the stage](https://docs.github.com/en/pull-requests/how-tos/create-pull-requests/changing-the-stage-of-a-pull-request) |
| Checks | [Status checks](https://docs.github.com/en/pull-requests/reference/status-checks), [REST checks guide](https://docs.github.com/en/rest/guides/using-the-rest-api-to-interact-with-checks) |
| Notifications | [REST notifications](https://docs.github.com/en/rest/activity/notifications?apiVersion=2022-11-28), [About notifications](https://docs.github.com/en/subscriptions-and-notifications/concepts/about-notifications) |
| Labels | [Managing labels](https://docs.github.com/en/issues/using-labels-and-milestones-to-track-work/managing-labels), [REST labels](https://docs.github.com/en/rest/issues/labels?apiVersion=2022-11-28) |
| GitPulse colors | [`Colors.xaml`](../../src/GitPulse.App/Resources/Styles/Colors.xaml) |
