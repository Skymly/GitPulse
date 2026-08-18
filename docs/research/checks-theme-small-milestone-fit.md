# Research: Checks / PR gate status — small-milestone fit

| Field | Value |
|-------|-------|
| **Ticket** | [#129](https://github.com/Skymly/GitPulse/issues/129) (part of [#128](https://github.com/Skymly/GitPulse/issues/128)) |
| **Theme (decided)** | **Checks / PR gate status** — locked on [v0.4.0 daily-driver feature slice](https://github.com/Skymly/GitPulse/issues/128) |
| **Date** | 2026-08-18 |
| **Purpose** | Constrain a ~1–3 related-feature small milestone via GitHub API surface + existing GitPulse coverage. **Does not** pick the final feature list (deferred to [Define the v0.4.0 feature boundaries](https://github.com/Skymly/GitPulse/issues/130)). |

## Method

Primary sources only:

- GitPulse code: `IGitHubReposApi`, `IGitHubActionsApi`, Core PR/Actions models, PR detail ViewModel & page
- Design docs / ADRs: `docs/ROADMAP.md`, `docs/CONTEXT.md`, `docs/design/{Architecture,RestApi,Events}.md`, ADR-004/005/009/011/014
- GitHub REST and product docs under `docs.github.com`

Claims below cite owning sources.

---

## 1. Two GitHub surfaces that look like “CI on a PR”

Official product docs distinguish two **status check** types ([Status checks](https://docs.github.com/en/pull-requests/reference/status-checks)):

| Type | Detail | Who creates it |
|------|--------|----------------|
| **Checks** (check runs / check suites) | Detailed output, annotations, Checks tab | GitHub Apps, **including GitHub Actions** |
| **Commit statuses** | Simple `error` / `failure` / `pending` / `success` plus `context` + `target_url` | External services / older CI |

GitHub Actions **generates checks, not commit statuses** when workflows run ([Status checks](https://docs.github.com/en/pull-requests/reference/status-checks)).

Implication for a daily-driver GitPulse slice: a modern repo that only uses Actions will have **check runs on the PR head SHA** and often **zero commit statuses**. Any slice that only calls the combined-status endpoint will look empty or misleading.

---

## 2. Existing GitPulse coverage vs missing surface

### 2.1 Already present (consume adjacent, not a PR gate)

ROADMAP archives M6 (merge), M10 (Actions workflow runs), M14/M15 (create PR + submit review) ([`docs/ROADMAP.md`](../ROADMAP.md)).

| Capability | GitPulse declaration / consumer | Source |
|------------|----------------------------------|--------|
| Get PR + `Head.Sha` | `GetPullRequest`; `PullRequest.Head` mapped from nested `head` | [`IGitHubReposApi`](../../src/GitPulse.GitHubApi/IGitHubReposApi.cs); [`PullRequest.cs`](../../src/GitPulse.Core/Models/PullRequest.cs) |
| Mergeability blob | `mergeable`, `mergeable_state` on `PullRequest`; `UpdateMergeStatus` shows Merged / Closed / Draft / Mergeable / Conflicts / Checking | Model comment lists `"clean"`, `"dirty"`, `"unstable"`, `"blocked"`; [`PullRequestDetailViewModel`](../../src/GitPulse.ViewModels/PullRequestDetailViewModel.cs) |
| Merge (merge/squash/rebase) | `MergePullRequest` | Same VM; gated on `mergeable` and not draft |
| Repo-scoped Actions | `IGitHubActionsApi` list/get runs, jobs, rerun, logs | [`IGitHubActionsApi.cs`](../../src/GitPulse.GitHubApi/IGitHubActionsApi.cs); ADR-009 |
| PR detail chrome | Conversation + Files tabs only | [`PullRequestDetailPage.xaml`](../../src/GitPulse.App/Views/PullRequestDetailPage.xaml) |
| Actions entry | Repo detail → workflow runs (not PR-head filtered) | [`RepoDetailPage.xaml`](../../src/GitPulse.App/Views/RepoDetailPage.xaml) |

**Not present:** check-run models, commit-status models, any `GET /commits/{ref}/check-runs` or `GET /commits/{ref}/status` method, any PR-detail Checks UI, any client rollup of CI on `Head.Sha`.

`mergeable_state` is an opaque GitHub merge-box hint (conflicts, reviews, required checks, behind, etc.). GitPulse already **displays** a coarse string when `mergeable` is true (`Mergeable` vs `Mergeable (blocked)`). It does **not** list which checks/statuses produced that state. That is the daily gap.

### 2.2 REST that actually answers “did this PR’s head pass?”

| Endpoint | In GitPulse? | Notes |
|----------|--------------|-------|
| `GET /repos/{owner}/{repo}/commits/{ref}/check-runs` | **Missing** | List check runs for a SHA / branch / tag ([List check runs for a Git reference](https://docs.github.com/en/rest/checks/runs?apiVersion=2022-11-28#list-check-runs-for-a-git-reference)). Wrapper: `total_count` + `check_runs[]`. Query `filter=latest` returns the most recent run per name (avoids listing every rerun). Path `{ref}` = PR `Head.Sha`. |
| `GET /repos/{owner}/{repo}/check-runs/{check_run_id}` | **Missing** | Single run; needed only if the slice drills into output/annotations. |
| `GET /repos/{owner}/{repo}/commits/{ref}/check-suites` | **Missing** | Suite rollup per app; extra hop if the slice wants suite-level rerequest. |
| `GET /repos/{owner}/{repo}/commits/{ref}/status` | **Missing** | **Combined commit status only** ([Get the combined status for a specific reference](https://docs.github.com/en/rest/commits/statuses?apiVersion=2022-11-28#get-the-combined-status-for-a-specific-reference)). Combined `state`: `failure` if any context is error/failure; `pending` if there are **no statuses** or any context is pending; `success` if every latest context is success. Does **not** include Actions check runs. |
| `GET /repos/{owner}/{repo}/commits/{ref}/statuses` | **Missing** | History of commit statuses, newest first. Combined endpoint is enough for a first-page rollup. |
| `GET /repos/{owner}/{repo}/actions/runs?head_sha=` | Partial | List-runs exists but GitPulse does not pass `head_sha`. Official list-runs docs allow `head_sha` (capped at 1,000 matches) ([Workflow runs](https://docs.github.com/en/rest/actions/workflow-runs)). Still **misses** third-party check runs and commit statuses. `GitHubQueryHandler` today only injects `page` / `per_page` / `state` — a new query field would be extra Core work. |
| Branch protection / ruleset required checks | **Missing** | Admin surface; tells you what is required, not what ran. Inflates the slice. |
| GraphQL `statusCheckRollup` | Out of RestApi scope | GitHub’s own PR merge box combines checks + statuses here. [`RestApi.md` “不在范围内”](../design/RestApi.md) lists GraphQL. |

### 2.3 Write / create surface (usually out of a read-the-gate slice)

- **Create / update check runs:** GitHub Apps only. OAuth apps and authenticated users (PAT) can **view** check runs and check suites, not create them ([Check runs](https://docs.github.com/en/rest/checks/runs?apiVersion=2022-11-28)).
- **Create commit statuses:** possible with commit-status write; not needed to *see* the gate.
- **Rerequest a check suite / rerun Actions:** Actions rerun already exists on the workflow-run page (`RerunWorkflow`). Suite rerequest is a second write path and permission story. Treat as a size hazard, not a prerequisite for listing.

### 2.4 Response fields a small UI actually needs

Check run (list item): `id`, `name`, `status`, `conclusion`, `html_url`, `details_url`, `started_at`, `completed_at`, nested `app` / `check_suite.id` ([example list payload](https://docs.github.com/en/rest/checks/runs?apiVersion=2022-11-28#list-check-runs-for-a-git-reference)).

Statuses (combined): top-level `state`, `total_count`, `sha`; each status `context`, `state`, `description`, `target_url` ([combined status example](https://docs.github.com/en/rest/commits/statuses?apiVersion=2022-11-28#get-the-combined-status-for-a-specific-reference)).

Check `status` / `conclusion` vocabulary is documented in [Status checks](https://docs.github.com/en/pull-requests/reference/status-checks) (`queued` / `in_progress` / `completed` / Actions-only `pending` / `waiting` / …; conclusions `success` / `failure` / `neutral` / `cancelled` / `skipped` / `timed_out` / `action_required` / `stale`). Skipped jobs report success for merge purposes.

### 2.5 Fork / pagination caveats

- Checks endpoints only see pushes in the repository where the suite was created; fork pushes yield an empty `pull_requests` array on the check-run payload ([Check runs note](https://docs.github.com/en/rest/checks/runs?apiVersion=2022-11-28)). Listing by **head SHA on the PR’s repo** still works for same-repo PRs (GitPulse create-PR is same-repo only, M14).
- More than 1,000 check runs for a ref need suite iteration ([list-runs note](https://docs.github.com/en/rest/checks/runs?apiVersion=2022-11-28)). A small milestone should take **first page + `filter=latest`**, same grain as M15 “first page of reviews”.
- Combined status is **pending when `statuses` is empty**. Do not treat that as “CI running” on an Actions-only repo.

---

## 3. Auth (PAT-only)

- ADR-004 stays: PAT only; OAuth not required for this theme ([`ADR-004`](../adr/ADR-004-pat-auth-platform-credential-store.md)).
- Fine-grained PAT:
  - List/get check runs: repository **Checks: read** ([check-run GET fine-grained notes](https://docs.github.com/en/rest/checks/runs?apiVersion=2022-11-28#get-a-check-run)).
  - Combined status: repository **Commit statuses: read** ([combined status fine-grained notes](https://docs.github.com/en/rest/commits/statuses?apiVersion=2022-11-28#get-the-combined-status-for-a-specific-reference)).
- Classic PAT: `repo` already used by GitPulse covers private-repo reads; `repo:status` is the narrower statuses scope and is **not** sufficient alone for check runs.
- Public repos can be read without those permissions. No ADR-004 supersede.

---

## 4. Platform (Windows-first, Android best-effort)

- Natural home: **PR detail**, next to existing `MergeStatus`. Conversation already has merge + reviews; a compact list + rollup avoids a third tab unless the list is long.
- Head SHA is already loaded with the PR; no new navigation args.
- Opening `html_url` / `target_url` can reuse `IBrowserLauncher` (already in Architecture DI).
- Android (ADR-011 / ADR-014): same XAML; IME not involved if the slice is read-only; emulator bar is **open PR detail without crash**. Diff / merge stay best-effort.
- Live polling of checks while a run is in progress would reuse the M4 Interval pattern but is extra Events work — size hazard.

---

## 5. Interface / layering (constraints, not a pick)

ADR-009 split Actions because workflow-run wrappers, rerun, and log redirects are a different resource domain ([`ADR-009`](../adr/ADR-009-split-github-actions-api-interface.md)). Check runs live under `/repos/{owner}/{repo}/commits/{ref}/check-runs` and `/check-runs/{id}`, **not** `/actions/runs`. Stuffing them into `IGitHubActionsApi` would miss commit statuses and mix two GitHub products.

Two seams that fit existing ADRs (leave the choice to boundaries):

1. **Extend `IGitHubReposApi`** — same move as M15 `GET /user` + reviews: commit-scoped GETs, no fourth interface, no new ADR. Matches “path + optional `[Query] filter`” now that Observables 0.1.5 allows non-path params ([`IGitHubReposApi` remarks](../../src/GitPulse.GitHubApi/IGitHubReposApi.cs); [Observables #111](https://github.com/Skymly/Observables/issues/111)).
2. **New `IGitHubChecksApi` (ADR-015)** — mirrors ADR-008/009 if the slice is expected to grow (annotations, suites, rerequest). Heavier docs/PR tax for a read-only first page.

Do **not** require `GitHubQueryHandler` to learn `head_sha` / `filter` unless the slice insists on Actions-list-by-SHA instead of Checks API.

ViewModel seam already exists: `PullRequestDetailViewModel` + `PullRequestDetailViewModelTests` / `MockHttpHandler` (M6/M14/M15 pattern).

---

## 6. Size hazards for [Define the v0.4.0 feature boundaries](https://github.com/Skymly/GitPulse/issues/130)

These would blow a 1–3 feature cut (not a feature pick):

- Treating **combined status alone** as the PR gate (wrong for Actions-only repos).
- GraphQL `statusCheckRollup` (RestApi out of scope).
- Full GitHub **Checks tab** clone: annotations on Files, outputs, suite tree, requested actions.
- **Required** checks vs optional (branch protection / rulesets).
- **Rerequest / rerun from PR detail** (write + permission + mapping check-suite → Actions run).
- **PR list** rollup (N+1 per row) or Notifications/Toast for failing checks.
- Creating checks (GitHub App) or posting commit statuses.
- Live polling until green.
- Fork/cross-repo check discovery.
- Bundling parked themes (commit history, review requests, personal hub).

A small REST slice that still closes the daily loop is: **on PR detail, for `Head.Sha`, list latest check runs + combined commit statuses, show a client-side rollup, keep existing merge rules, open detail URLs in the browser**.

---

## 7. Pointers

| Kind | Where |
|------|--------|
| Product | [Status checks](https://docs.github.com/en/pull-requests/reference/status-checks) |
| REST Checks | [Check runs](https://docs.github.com/en/rest/checks/runs?apiVersion=2022-11-28), [Check suites](https://docs.github.com/en/rest/checks/suites?apiVersion=2022-11-28) |
| REST statuses | [Commit statuses](https://docs.github.com/en/rest/commits/statuses?apiVersion=2022-11-28) |
| Actions adjacent | [Workflow runs](https://docs.github.com/en/rest/actions/workflow-runs) |
| GitPulse API | `src/GitPulse.GitHubApi/IGitHubReposApi.cs`, `IGitHubActionsApi.cs` |
| GitPulse models | `src/GitPulse.Core/Models/PullRequest.cs`, `WorkflowRun.cs` |
| GitPulse UI | `PullRequestDetailViewModel`, `PullRequestDetailPage.xaml` |
| Design | `docs/design/RestApi.md`, ADR-004, ADR-009, ADR-011, ADR-014 |
