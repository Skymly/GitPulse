# GitPulse — AI Agent Notes

## Project Status

- **Type**: Personal project (Skymly workspace)
- **Remote**: https://github.com/Skymly/GitPulse
- **Stage**: M6 complete (PR review & merge) — auth, repo browsing, issue & PR lists with pagination, issue & PR detail with markdown, CRUD operations (comments, state toggle, labels, new issue), notification center with polling-simulated realtime (R3 Observable.Interval), repository file browser and editor (view, create, update, delete via Contents API), PR merge with method selection (merge/squash/rebase) and mergeability status
- **Purpose**: Real-world showcase application for [Observables](https://github.com/Skymly/Observables) (declarative reactive HTTP/events bridging for R3). Not a toy demo — a working GitHub client the author uses day-to-day.

## Tech Stack

- **.NET 10** (LTS) + **.NET MAUI**
- **R3** 1.3.0+ + **R3Extensions.Maui** (`UseR3()`, `BindableReactiveProperty<T>`)
- **Observables.RestAPI.R3** + **Observables.Events.R3** (source-generated reactive bridges)
- **CommunityToolkit.Mvvm** (RelayCommand etc.); state management via R3 `BindableReactiveProperty<T>`
- **Indiko.Maui.Controls.Markdown** 1.5.0 (native MAUI markdown rendering, no WebView)
- **MinVer** (Git-tag-based versioning)
- **Nuke** build orchestration
- **xunit.v3** testing

## Target Platforms

- **Windows** (primary): `net10.0-windows10.0.19041.0`
- **Android** (secondary): `net10.0-android`
- iOS / MacCatalyst: deferred until stable

## Project Structure

```
src/
  GitPulse.App/         — MAUI UI (Views, DI, platform entry points, BrowserLauncher)
  GitPulse.ViewModels/  — ViewModels (R3 state, MAUI-free, testable on net10.0)
  GitPulse.Core/        — Domain models, abstractions, HTTP helpers (no UI/IO)
    └─ Http/            — GitHubQueryHandler, LinkHeaderParser
  GitPulse.GitHubApi/   — Observables.RestAPI declarative interfaces + DTOs
  GitPulse.Services/    — GitHubClientFactory (auth, header setup, paged client)
tests/
  GitPulse.Tests/       — Unit tests (+ TestHelpers: MockHttpHandler, FakeGitHubClientFactory)
build/                  — Nuke build script (_build.csproj + Program.cs)
.nuke/                  — Nuke parameters & schema
```

### Layer responsibilities

| Layer | Responsibility | Depends on |
|-------|---------------|------------|
| **App** | MAUI Views (XAML), `MauiProgram` (DI + `UseR3()`), platform entry, `BrowserLauncher` | Core, GitHubApi, Services, ViewModels |
| **ViewModels** | ViewModels (R3 `BindableReactiveProperty` state, `[RelayCommand]`), `RepoNavigationArgs` | Core, GitHubApi |
| **Core** | Domain models (`Repo`, `Issue`, `User`, `PullRequest`, `Comment`, `Label`), abstractions (`ICredentialStore`, `IGitHubClientFactory`, `IBrowserLauncher`) | none |
| **GitHubApi** | Declarative `IGitHubReposApi` etc. (Observables.RestAPI) | Core |
| **Services** | `GitHubClientFactory`, auth infrastructure, caching, notification polling, app config | Core, GitHubApi |

## Build & CI (Nuke)

| Nuke target | Description |
|-------------|-------------|
| **Ci** | `Clean` → `Restore` → `Compile` → `UnitTest` |
| **CiLib** | Cross-platform library tests only (no App project) |
| **CiAll** | `Format` + `Ci` (full local/CI verification) |
| **Test** | Alias for `UnitTest` |
| **Format** | `dotnet format --verify-no-changes` |
| **FormatFix** | `dotnet format` (applies formatting) |
| **Publish** | Self-contained Windows exe → `artifacts/publish/{Runtime}/` |
| **PublishVerify** | `Publish` + verify entry point exists |
| **Release** | `CiAll` + `PublishVerify` (full release pipeline) |

Parameters: `--configuration`, `--runtime` (default `win-x64`), `--version` (override MinVer), `--windowsFramework`

```powershell
./build.ps1 --target CiAll --configuration Release
./build.ps1 --target Publish --configuration Release --runtime win-x64
```

## Observables Showcase Design

### RestAPI domain (core showcase)

Declarative interfaces — the source generator produces `HttpClient` proxy implementations at compile time:

```csharp
public interface IGitHubReposApi
{
    // ApiResponse<T> exposes response Headers (including Link for pagination)
    [Get("/user/repos")]
    Observable<ApiResponse<Repo[]>> ListMyReposPaged();

    [Get("/repos/{owner}/{repo}")]
    Observable<Repo> GetRepo(string owner, string repo);

    // Path-only parameters — query params injected via GitHubQueryHandler
    [Get("/repos/{owner}/{repo}/issues")]
    Observable<ApiResponse<Issue[]>> ListIssuesPaged(string owner, string repo);
}

var api = RestService.For<IGitHubReposApi>(httpClient);
```

**Pagination via `ApiResponse<T>`**: List methods return
`Observable<ApiResponse<T>>` to expose the `Link` response header. The
`ApiResponse<T>` wrapper provides `Content` (deserialized body) and
`Headers` (including `Link` for `rel="next"` detection). Query parameters
(`page`, `per_page`, `state`) are injected by `GitHubQueryHandler`
(Core/Http) at the HTTP layer, working around OBS3004.

### Events domain (UI layer showcase)

MAUI control events → `Observable<T>` streams. The intended source-generated
form is:

```csharp
searchBar.Events().TextChanged
    .Select(e => e.NewTextValue ?? string.Empty)
    .Debounce(TimeSpan.FromMilliseconds(300), TimeProvider.System)
    .DistinctUntilChanged()
    .Subscribe(text => vm.SearchText.Value = text);
```

**However**, the generated `.Events()` extension for `Microsoft.Maui.Controls.SearchBar`
produces code referencing `IControlsVisualElement` — an internal MAUI interface —
causing CS0122. Until the upstream generator handles MAUI's internal accessibility,
GitPulse bridges the event manually via an R3 `Subject<T>` in the page code-behind
(`ReposPage.xaml.cs`). The pipeline (Debounce + DistinctUntilChanged + ObserveOn)
is identical; only the event→Observable bridge is manual instead of source-generated.

### Known Observables 0.1.4 limitations (discovered via this project)

**1. RestAPI path validation (OBS3004) — FIXED in 0.1.5** —
`ValidatePathTemplate` in `Observables.RestAPI.SourceGenerators.Shared/Parser.cs`
previously required the set of path placeholders to **equal** the set of all
non-CancellationToken parameter names, rejecting `[Query]`/`[Body]`/`[Header]`
parameters. In 0.1.5, validation runs **after** parameter classification, so
non-path parameters are correctly excluded. Path + `[Body]` parameters now
coexist on the same method — M3 CRUD operations use this directly.

**Before the fix (0.1.4)**: API methods used only path parameters (no `[Query]`).
Pagination (`page`/`per_page`) and filtering (`state`) query parameters were
injected by `GitHubQueryHandler` (Core/Http), a `DelegatingHandler` that
modifies the request URI before it reaches the inner handler. This workaround
remains in place for pagination (since `ApiResponse<T>` + `Link` header is
still needed for `CanLoadMore` detection), but CRUD `[Body]` parameters are
now declared directly on the interface.
Upstream issue: https://github.com/Skymly/Observables/issues/111

**2. Events + MAUI internal interfaces** — The source-generated `.Events()`
extension for `Microsoft.Maui.Controls.SearchBar` (and likely other MAUI
controls) emits code referencing `IControlsVisualElement`, which is
`internal` in MAUI. This causes CS0122 at compile time. The generator does
not account for MAUI's internal accessibility boundaries.

**Workaround in GitPulse**: The search debounce pipeline in `ReposPage.xaml.cs`
uses a manual R3 `Subject<T>` bridge instead of the source-generated
`.Events()` extension. The reactive pipeline itself (Debounce +
DistinctUntilChanged + ObserveOn) is unaffected.

Both issues are exactly the kind of real-world friction a showcase project
should surface — tracked for upstream feedback.

## Authentication & Credentials

- PAT (Personal Access Token) as the sole auth method (GitHub App OAuth deferred)
- `ICredentialStore` abstraction (Core):
  - Windows: DPAPI (CurrentUser scope) — implementation in `App/Platforms/Windows/`
  - Android: `SecureStorage` (MAUI Essentials) — implementation in `App/Platforms/Android/`
  - Platform implementations live in the App project because they use platform-specific
    APIs (DPAPI / MAUI SecureStorage) that require the MAUI host or Windows-only packages.
- `GitHubClientFactory` (Services layer) builds `HttpClient` with `Authorization: Bearer <PAT>`,
  `Accept: application/vnd.github+json`, `X-GitHub-Api-Version: 2022-11-28`,
  `User-Agent: GitPulse`

## Milestone Roadmap

| Milestone | Content | Observables domains |
|-----------|---------|---------------------|
| **M0** ✅ | Project skeleton: solution, projects, Nuke, CI, docs, empty MAUI app builds | — |
| **M1** ✅ | Auth + repository list browsing | RestAPI + Events |
| **M2** ✅ | Issue/PR list & detail (issues + PRs, state filter, detail with comments) | RestAPI + Events |
| **M3** ✅ | Issue/PR CRUD (comments, state toggle, labels, new issue) | RestAPI |
| **M4** ✅ | Notification center (polling-simulated realtime) | Events (R3 Observable.Interval) |
| **M5** ✅ | File browsing & editing | RestAPI |
| **M6** ✅ | PR review & merge | RestAPI |
| **M7** | Android adaptation & stabilization | platform abstraction |
| **M8** | Release v0.1.0 | full pipeline |

## Conventions

- C# latest features allowed
- `async/await` for all I/O
- File-scoped namespaces
- Nullable reference types enabled
- No AI tool/model attribution in commits
- Commit messages: English
- Central package management (`Directory.Packages.props`)
- `TreatWarningsAsErrors` in Release (excludes Nuke `_build`)
