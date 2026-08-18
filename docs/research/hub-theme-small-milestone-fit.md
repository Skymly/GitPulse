# Research: personal hub entry — small-milestone fit

| Field | Value |
|-------|-------|
| **Ticket** | [#147](https://github.com/Skymly/GitPulse/issues/147) (part of [#146](https://github.com/Skymly/GitPulse/issues/146)) |
| **Theme (decided)** | **personal hub entry** — locked on [v0.5.0 daily-driver feature slice](https://github.com/Skymly/GitPulse/issues/146) |
| **Date** | 2026-08-18 |
| **Purpose** | Constrain a ~1–3 related-feature small milestone. **Does not** pick the final feature list (deferred to [Define the v0.5.0 feature boundaries](https://github.com/Skymly/GitPulse/issues/148)). |

## Method

Primary sources: GitPulse `IGitHubReposApi` / `ReposViewModel` / `AppShell`; ADR-004/005/006/011; GitHub REST starring and repos docs.

---

## 1. What “personal hub” can mean

| Sense | GitHub REST? | GitPulse today |
|-------|----------------|----------------|
| **My repositories** | `GET /user/repos` | **Present** — `ListMyReposPaged` + Repos tab + local `SearchText` filter ([`ReposViewModel`](../../src/GitPulse.ViewModels/ReposViewModel.cs)) |
| **Starred repositories** | `GET /user/starred` ([Starring](https://docs.github.com/en/rest/activity/starring?apiVersion=2022-11-28#list-repositories-starred-by-the-authenticated-user)) | **Missing** |
| **Recently pushed / updated** | `GET /user/repos?sort=pushed` ([List repositories for the authenticated user](https://docs.github.com/en/rest/repos/repos?apiVersion=2022-11-28#list-repositories-for-the-authenticated-user)) | **Missing sort** — list exists; `GitHubQueryHandler` only injects `page` / `per_page` / `state` ([`GitHubQueryHandler.cs`](../../src/GitPulse.Core/Http/GitHubQueryHandler.cs)) |
| **Recently viewed in GitPulse** | **No GitHub API** | Would be local-only (new store). Not a RestAPI showcase. |
| **Watching / subscriptions** | `GET /user/subscriptions` | Different product from stars; extra surface. |
| **Global find-a-repo** | Search API | Already a Shell tab (M9). |

Daily-driver gap vs GitHub.com home: **starred list** (bookmark jump) and maybe **recently pushed** among repos you already have. “Recently viewed” is not REST.

---

## 2. Existing coverage vs missing

### Present

- Paged `GET /user/repos` → Repos tab → `RepoDetailPage` ([`IGitHubReposApi.ListMyReposPaged`](../../src/GitPulse.GitHubApi/IGitHubReposApi.cs)).
- Client-side name/description filter (not GitHub `q`).
- Search tab already finds other people’s repos.
- `Repo` model already is the starring list item type (same JSON shape as `/user/starred`).

### Missing

| Endpoint | Notes |
|----------|--------|
| `GET /user/starred` | Paged via `Link`; items are `Repo`. Fine-grained PAT: **Starring: read**. Classic `repo` already used by GitPulse. |
| `PUT` / `DELETE /user/starred/{owner}/{repo}` | Star / unstar. Write permission. Not required to *open* a starred hub. |
| `GET /user/starred/{owner}/{repo}` | Check if starred (204/404). Needed only if the slice adds a star toggle on Repo detail. |
| `sort` / `affiliation` on `/user/repos` | Handler change or `[Query]` on a new method. Affects “recently pushed” without a new resource. |

Shell today: Repos / Notifications / Search / Settings ([`AppShell.xaml`](../../src/GitPulse.App/AppShell.xaml)). A fifth tab is a UX tax on phone (ADR-011). Prefer a **segment on the existing Repos tab** over a new tab.

---

## 3. Auth / platform

- ADR-004: PAT only. Starring read does not need OAuth.
- Windows-first same XAML. Starred list is another `CollectionView` + `PagedGitHubSession` — same pattern as Repos.
- Android: no IME if read-only; open Repos tab / starred segment without crash.

---

## 4. Interface / layering

`GET /user/starred` is an activity/starring resource, but the payload is `Repo[]` and the consumer is the Repos hub. Two seams (leave pick to boundaries):

1. **Add `ListStarredReposPaged` on `IGitHubReposApi`** — M15/M16 style, no new interface, no ADR.
2. New activity API interface — ADR tax for one GET.

Do **not** overload `ListMyReposPaged` with a mode flag. Keep Paged GitHub Session; starred uses its own session.

`sort=pushed` on existing list is a handler/`[Query]` change — small but touches Core if done via `GitHubQueryHandler`. Prefer `[Query] sort` on a dedicated method if Observables 0.1.5 allows it (already used for Check Run `filter`).

---

## 5. Size hazards for [Define the v0.5.0 feature boundaries](https://github.com/Skymly/GitPulse/issues/148)

- New Shell tab (phone chrome).
- Star / unstar + “is starred” on every repo detail.
- Local recently-viewed store (no GitHub source of truth).
- Watching / notifications subscription list.
- Replacing Search.
- Changing default `/user/repos` affiliation.
- Infinite “home dashboard” (issues assigned, review requests, …) — that is another theme.

A small REST slice: **on the Repos tab, switch My repos (existing) vs Starred (`GET /user/starred`, paged), reuse `Repo` → Repo detail.** Optionally add recently pushed as a third segment only if it stays one page.

---

## 6. Pointers

| Kind | Where |
|------|--------|
| REST starring | [List starred repos](https://docs.github.com/en/rest/activity/starring?apiVersion=2022-11-28#list-repositories-starred-by-the-authenticated-user) |
| REST repos | [List repos for the authenticated user](https://docs.github.com/en/rest/repos/repos?apiVersion=2022-11-28#list-repositories-for-the-authenticated-user) |
| GitPulse | `IGitHubReposApi`, `ReposViewModel`, `ReposPage`, `AppShell` |
| Design | ADR-004, ADR-006, ADR-011, `RestApi.md` |
