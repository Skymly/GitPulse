# Research: create/manage PRs — small-milestone fit

| Field | Value |
|-------|-------|
| **Ticket** | [#92](https://github.com/Skymly/GitPulse/issues/92) (part of [#88](https://github.com/Skymly/GitPulse/issues/88)) |
| **Theme (decided)** | **create/manage PRs** — [#89](https://github.com/Skymly/GitPulse/issues/89) resolution |
| **Date** | 2026-08-10 |
| **Purpose** | Constrain a ~1–3 related-feature small milestone via GitHub API surface + existing GitPulse coverage. **Does not** pick the final feature list (deferred to [#90](https://github.com/Skymly/GitPulse/issues/90)). |

## Method

Primary sources only:

- GitPulse code: `IGitHubReposApi`, Core models, PR/Issue ViewModels & pages
- Design docs / ADRs: `docs/ROADMAP.md`, `docs/CONTEXT.md`, `docs/design/{Architecture,RestApi,Events}.md`, ADR-004/005/011/014
- GitHub REST (and GraphQL where REST lacks draft toggle): official docs under `docs.github.com`

Claims below cite owning sources.

---

## 1. Existing GitPulse coverage vs missing surface

### 1.1 Already present (PR lifecycle “consume + close the loop” without Create)

ROADMAP archives M2–M3 / M6 / M8 as Issue/PR list+detail, Issue CRUD (incl. open/close via Issues API), PR merge, and PR diff + inline review comments ([`docs/ROADMAP.md`](../ROADMAP.md) completed table).

| Capability | GitPulse declaration / consumer | Source |
|------------|----------------------------------|--------|
| List PRs (paged) | `GET /repos/{owner}/{repo}/pulls` → `ListPullRequestsPaged` | [`IGitHubReposApi.cs`](../../src/GitPulse.GitHubApi/IGitHubReposApi.cs); [`PullRequestsViewModel`](../../src/GitPulse.ViewModels/PullRequestsViewModel.cs) |
| Get PR | `GetPullRequest` | Same API; [`PullRequestDetailViewModel`](../../src/GitPulse.ViewModels/PullRequestDetailViewModel.cs) |
| Conversation comments | Issue comments endpoints reused for PRs | API + detail VM remarks; GitHub: PRs are issues for shared actions ([REST pulls “About”](https://docs.github.com/en/rest/pulls/pulls?apiVersion=2022-11-28#about-pull-requests)) |
| Open / close PR | `UpdateIssue` (`PATCH .../issues/{number}`) with `state` | Detail VM; [`IssueUpdateRequest`](../../src/GitPulse.Core/Models/IssueRequests.cs) |
| Labels | `ListIssueLabels` / `ReplaceIssueLabels` | `IGitHubReposApi` (issue number = PR number) |
| Merge (merge/squash/rebase) | `MergePullRequest` + `MergeRequest` / `MergeResponse` | API; [`PullRequest.cs`](../../src/GitPulse.Core/Models/PullRequest.cs); detail VM |
| Diff files + inline review comments | `ListPullRequestFiles`, `ListReviewComments`, `CreateReviewComment` | API; [`PrDiffViewModel`](../../src/GitPulse.ViewModels/PrDiffViewModel.cs); RestApi M8 ([`RestApi.md`](../design/RestApi.md)) |
| Branch list (create-PR picker input) | `ListBranches` | `IGitHubReposApi`; [`Branch`](../../src/GitPulse.Core/Models/Branch.cs) |
| Search PRs | `IGitHubSearchApi.SearchPullRequests` | [`IGitHubSearchApi.cs`](../../src/GitPulse.GitHubApi/IGitHubSearchApi.cs); navigates to detail |
| Draft **display** / merge gating | `PullRequest.Draft`; UI badge; `CanMerge` false when draft | Model + [`PullRequestDetailPage.xaml`](../../src/GitPulse.App/Views/PullRequestDetailPage.xaml); detail VM `UpdateMergeStatus` |
| Create Issue (UX analog only) | `CreateIssue` + `CreateIssuePage` / `CreateIssueViewModel` | Pattern for a future create-PR page; **not** PR create |

**Models present:** `PullRequest`, `PullRequestHead`, merge DTOs, `DiffEntry`, `ReviewComment`, `ReviewCommentRequest` ([`PullRequest.cs`](../../src/GitPulse.Core/Models/PullRequest.cs), related Core model files).

**UI surface present:** `PullRequestsPage`, `PullRequestDetailPage` (Conversation + Files tabs), Search → PR detail. **No** “New pull request” affordance on the PR list ([`PullRequestsPage.xaml`](../../src/GitPulse.App/Views/PullRequestsPage.xaml) has no Create control, unlike Issues → `CreateIssuePage`).

**Pagination / filter:** `GitHubQueryHandler` injects `page` / `per_page` / `state` for Issues/PRs ([`GitHubQueryHandler.cs`](../../src/GitPulse.Core/Http/GitHubQueryHandler.cs); ADR-006 / RestApi).

### 1.2 Missing relative to “create/manage PRs” (REST)

Official pulls REST catalog: list / create / get / update / commits / files / merge (+ async merge) / update-branch ([REST pulls](https://docs.github.com/en/rest/pulls/pulls?apiVersion=2022-11-28)). Related: [review requests](https://docs.github.com/en/rest/pulls/review-requests?apiVersion=2022-11-28), [reviews](https://docs.github.com/en/rest/pulls/reviews?apiVersion=2022-11-28), [review comments](https://docs.github.com/en/rest/pulls/comments) (partially covered).

| Endpoint / concern | In GitPulse? | Notes |
|--------------------|--------------|-------|
| `POST /repos/{owner}/{repo}/pulls` | **Missing** | Create: required `head` + `base`; optional `title`/`body`/`draft`/`issue`/cross-repo fields ([Create a pull request](https://docs.github.com/en/rest/pulls/pulls?apiVersion=2022-11-28#create-a-pull-request)). No `PullRequestCreateRequest` DTO. |
| `PATCH /repos/{owner}/{repo}/pulls/{pull_number}` | **Missing** | Update title/body/`state`/`base`/`maintainer_can_modify` ([Update a pull request](https://docs.github.com/en/rest/pulls/pulls?apiVersion=2022-11-28#update-a-pull-request)). **Partial substitute:** title/body/`state` already possible via existing `UpdateIssue` (GitHub: shared issue actions on PRs — [About pull requests](https://docs.github.com/en/rest/pulls/pulls?apiVersion=2022-11-28#about-pull-requests)). **Not** substitutable: change **base** branch; PR-only fields. Detail VM today only toggles `state`, does not edit title/body. |
| Formal reviews `.../pulls/{n}/reviews` | **Missing** | List / create (`APPROVE` / `REQUEST_CHANGES` / `COMMENT` / pending) / submit / dismiss ([Reviews](https://docs.github.com/en/rest/pulls/reviews?apiVersion=2022-11-28)). Inline `CreateReviewComment` ≠ submitted review. |
| Review requests `.../requested_reviewers` | **Missing** | GET / POST / DELETE ([Review requests](https://docs.github.com/en/rest/pulls/review-requests?apiVersion=2022-11-28)). No `requested_reviewers` on Core `PullRequest` model. |
| `GET .../pulls/{n}/commits` | **Missing** | Parked theme “commit history” in #89; listing PR commits is adjacent but separate. |
| `PUT .../pulls/{n}/update-branch` | **Missing** | Update PR branch from base ([Update a pull request branch](https://docs.github.com/en/rest/pulls/pulls?apiVersion=2022-11-28#update-a-pull-request-branch)). |
| Async merge / stacks | **Missing** | Newer REST; outsized vs daily create/manage. |
| Checks / commit statuses on PR head | **Missing** (Actions runs exist repo-scoped) | Parked as separate theme in #89; `IGitHubActionsApi` is workflow runs, not PR check-runs / combined status. |

### 1.3 Draft after create: GraphQL gap (forces size or design exception)

- **Create as draft:** supported on REST `POST .../pulls` body `draft` ([Create a pull request](https://docs.github.com/en/rest/pulls/pulls?apiVersion=2022-11-28#create-a-pull-request)).
- **Mark ready for review / convert to draft after create:** GraphQL mutations `markPullRequestReadyForReview` and `convertPullRequestToDraft` ([GraphQL pulls reference](https://docs.github.com/en/graphql/reference/pulls)). Official REST **Update** body documents `title`/`body`/`state`/`base`/`maintainer_can_modify` only — not draft toggle ([Update a pull request](https://docs.github.com/en/rest/pulls/pulls?apiVersion=2022-11-28#update-a-pull-request)).
- GitPulse RestApi design explicitly lists **GraphQL out of scope** ([`RestApi.md` “不在范围内”](../design/RestApi.md)).
- Core `PullRequest` has no `node_id` / GraphQL id field (needed for those mutations).

Implication for #90: treating “ready for review” as a first-class manage action either (a) pulls GraphQL into scope (ADR/design change), or (b) stays create-time `draft:true` only / browser deep-link, without claiming full draft lifecycle in-app.

### 1.4 Model / deserialization caveats (small but real)

`PullRequest.HeadRef` / `BaseRef` lack `[JsonPropertyName]` and do not match GitHub’s nested `head`/`base` objects; nested `Head` (`PullRequestHead` with `Ref`/`Sha`) is mapped ([`PullRequest.cs`](../../src/GitPulse.Core/Models/PullRequest.cs)). Create/manage UIs should prefer `Head` / `ListBranches` rather than assuming flat `*Ref` strings are populated from JSON.

### 1.5 Auth constraint (PAT-only)

- ADR-004: PAT only; OAuth parked ([`ADR-004`](../adr/ADR-004-pat-auth-platform-credential-store.md)).
- Fine-grained PAT: create/update PR and review-request writes require repository **Pull requests: write** ([permissions table — Pull requests](https://docs.github.com/en/rest/authentication/permissions-required-for-fine-grained-personal-access-tokens?apiVersion=2022-11-28#repository-permissions-for-pull-requests); includes `POST /pulls`, `PATCH /pulls/{n}`, review-request and reviews POST).
- Does **not** by itself require superseding ADR-004 for this theme; users may need a token with PR write (classic `repo` or fine-grained Pull requests write). Documenting required scopes in Settings/help is product copy, not OAuth.

---

## 2. Platform risk (Windows-first + Android best-effort)

| Concern | Risk | Source |
|---------|------|--------|
| Windows as ship bar | **Low–medium** for form-style create/edit mirroring `CreateIssue*`; merge/diff already ship on Windows | ADR-005 ([`ADR-005`](../adr/ADR-005-windows-first-platform-strategy.md)); Architecture platform section ([`Architecture.md`](../design/Architecture.md)) |
| Android daily use | **Medium** for new create/edit forms: ADR-011 requires soft keyboard not to block submit on Issue/PR detail and **Create Issue**; any Create PR page inherits that bar | [`ADR-011`](../adr/ADR-011-android-m11-daily-usable-phone.md) |
| Diff / merge on Android | Already “挤可用,” not polish-required | ADR-011 workflow row |
| Emulator smoke / APK cut | New first-class page should open without crash if attached to a cut; Diff/IME automation **not** required by ADR-014 | [`ADR-014`](../adr/ADR-014-android-emulator-ui-smoke-and-apk-release.md); CONTEXT “Android Emulator UI Smoke” ([`CONTEXT.md`](../CONTEXT.md)) |
| Branch pickers | **Low** — `ListBranches` + Picker/ListView patterns; avoid inventing second Shell | Architecture: same XAML, no dual views by default |
| Tray/Toast | N/A to PR create/manage (Windows-only presence) | ADR-010 / Events |

Observables showcase is **tie-breaker only** for theme choice (#89); RestAPI path+`[Body]` for `POST /pulls` is already proven by `CreateIssue` (OBS3004 fixed in 0.1.5 — RestApi / AGENTS).

---

## 3. What would force the slice too large

Do **not** interpret as a feature ranking — only as size hazards for #90:

1. **GraphQL draft lifecycle** (ready-for-review / convert-to-draft) while RestApi forbids GraphQL — needs ADR/design supersede + new client path + `node_id` on models.
2. **Formal review submission UX** (pending review, APPROVE / REQUEST_CHANGES, multi-comment batch) — full Reviews API state machine on top of existing inline comments.
3. **Cross-fork / `head_repo` / `user:branch` create** — branch enumeration across forks, permissions, and validation (422) beyond same-repo `ListBranches`.
4. **Bundling Checks / PR gate status** — explicitly parked in #89; Actions runs ≠ check-runs; new status API + UI on detail.
5. **Reviewer/team pickers** needing collaborators/teams APIs + search UX (review-request POST itself is small; discovery UI is not).
6. **Update-branch + branch-protection error surfacing** as a required manage path — protection/rules messaging easily sprawls.
7. **Process multiplier:** Architecture one-module-per-PR rule means even a thin Create PR spans Core (DTO) → GitHubApi → ViewModels → App → Tests as **multiple** delivery PRs ([`Architecture.md`](../design/Architecture.md)); stacking several of the above into one “milestone” compounds calendar size.
8. **Async merge / stacked PRs** — new REST surface, niche vs author’s daily create/manage.

Smaller relative gaps (still not a selection): REST create PR; edit title/body via existing `UpdateIssue` or new pulls PATCH; request reviewers with login strings only; create-time `draft:true` without post-create GraphQL.

---

## 4. Pointers (docs & code)

| Area | Path / URL |
|------|------------|
| Theme decision | [#89](https://github.com/Skymly/GitPulse/issues/89) |
| Boundaries grilling (next) | [#90](https://github.com/Skymly/GitPulse/issues/90) |
| Roadmap archive (M2–M8 PR stack) | [`docs/ROADMAP.md`](../ROADMAP.md) |
| RestAPI design + GraphQL OOS | [`docs/design/RestApi.md`](../design/RestApi.md) |
| Module / PR boundaries | [`docs/design/Architecture.md`](../design/Architecture.md) |
| Windows-first | [`docs/adr/ADR-005-windows-first-platform-strategy.md`](../adr/ADR-005-windows-first-platform-strategy.md) |
| PAT-only | [`docs/adr/ADR-004-pat-auth-platform-credential-store.md`](../adr/ADR-004-pat-auth-platform-credential-store.md) |
| Android IME / CRUD bar | [`docs/adr/ADR-011-android-m11-daily-usable-phone.md`](../adr/ADR-011-android-m11-daily-usable-phone.md) |
| API interface | [`src/GitPulse.GitHubApi/IGitHubReposApi.cs`](../../src/GitPulse.GitHubApi/IGitHubReposApi.cs) |
| PR model / merge DTOs | [`src/GitPulse.Core/Models/PullRequest.cs`](../../src/GitPulse.Core/Models/PullRequest.cs) |
| Create-Issue analog | [`CreateIssueViewModel.cs`](../../src/GitPulse.ViewModels/CreateIssueViewModel.cs), App `CreateIssuePage` |
| PR detail / merge / state | [`PullRequestDetailViewModel.cs`](../../src/GitPulse.ViewModels/PullRequestDetailViewModel.cs) |
| Diff + inline comments | [`PrDiffViewModel.cs`](../../src/GitPulse.ViewModels/PrDiffViewModel.cs) |
| GitHub REST pulls | https://docs.github.com/en/rest/pulls/pulls?apiVersion=2022-11-28 |
| Review requests | https://docs.github.com/en/rest/pulls/review-requests?apiVersion=2022-11-28 |
| Reviews | https://docs.github.com/en/rest/pulls/reviews?apiVersion=2022-11-28 |
| GraphQL draft mutations | https://docs.github.com/en/graphql/reference/pulls |
| Fine-grained PR permissions | https://docs.github.com/en/rest/authentication/permissions-required-for-fine-grained-personal-access-tokens?apiVersion=2022-11-28#repository-permissions-for-pull-requests |

---

## 5. Resolution gist (for issue comment; no feature pick)

Winning theme **create/manage PRs** sits on a large **already-shipped** consume stack (list/detail, comments, open/close via Issues API, labels, merge, diff + inline review comments, branch list, PR search) and a clear **Create** hole (`POST /pulls` + DTOs + UI; Issues have `CreateIssue*`, PRs do not).

**Manage** is partially covered (state, merge, conversation, inline comments). Gaps that still fit REST without GraphQL include pulls PATCH (esp. base), review requests, and formal reviews. **Post-create draft ↔ ready** is GraphQL-only and conflicts with RestApi’s GraphQL exclusion unless design is superseded; create-time `draft:true` remains REST-feasible.

**Auth:** PAT with Pull requests write is enough; ADR-004 need not change for this theme.

**Platform:** Windows-first form work is low–medium risk; Android inherits ADR-011 IME rules for create/submit; Diff/merge stay best-effort.

**Size hazards for #90:** GraphQL draft lifecycle, full review-approval UX, cross-fork create, Checks bundling, rich reviewer discovery, update-branch+protection messaging, and multi-module delivery overhead.

Leave concrete 1–3 features and acceptance boundaries to [#90](https://github.com/Skymly/GitPulse/issues/90).
