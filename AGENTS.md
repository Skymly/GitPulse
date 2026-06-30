# GitPulse — AI Agent Notes

## Project Status

- **Type**: Personal project (Skymly workspace)
- **Remote**: https://github.com/Skymly/GitPulse
- **Stage**: M0 (project skeleton) — solution, project structure, Nuke build, CI, docs framework in place; empty MAUI app builds
- **Purpose**: Real-world showcase application for [Observables](https://github.com/Skymly/Observables) (declarative reactive HTTP/events bridging for R3). Not a toy demo — a working GitHub client the author uses day-to-day.

## Tech Stack

- **.NET 10** (LTS) + **.NET MAUI**
- **R3** 1.3.0+ + **R3Extensions.Maui** (`UseR3()`, `BindableReactiveProperty<T>`)
- **Observables.RestAPI.R3** + **Observables.Events.R3** (source-generated reactive bridges)
- **CommunityToolkit.Mvvm** (RelayCommand etc.); state management via R3 `BindableReactiveProperty<T>`
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
  GitPulse.App/         — MAUI UI (Views, ViewModels, DI, platform entry points)
  GitPulse.Core/        — Domain models, abstractions (no UI/IO)
  GitPulse.GitHubApi/   — Observables.RestAPI declarative interfaces + DTOs
  GitPulse.Services/    — Auth, caching, polling, app config
tests/
  GitPulse.Tests/       — Unit tests
build/                  — Nuke build script (_build.csproj + Program.cs)
.nuke/                  — Nuke parameters & schema
```

### Layer responsibilities

| Layer | Responsibility | Depends on |
|-------|---------------|------------|
| **App** | MAUI Views (XAML), ViewModels (R3 state), `MauiProgram` (DI + `UseR3()`), platform entry | Core, GitHubApi, Services |
| **Core** | Domain models (`Repo`, `Issue`, `User`), abstractions (`ICredentialStore`, `IGitHubClientFactory`) | none |
| **GitHubApi** | Declarative `IGitHubReposApi` etc. (Observables.RestAPI), `GitHubClientFactory` | Core |
| **Services** | Auth, caching, notification polling, app config | Core, GitHubApi |

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
    [Get("/user/repos")]
    Observable<Repo[]> ListMyRepos();

    [Get("/repos/{owner}/{repo}")]
    Observable<Repo> GetRepo(string owner, string repo);
}

var api = RestService.For<IGitHubReposApi>(httpClient);
```

### Events domain (UI layer showcase)

MAUI control events → `Observable<T>` streams:

```csharp
searchBar.Events().TextChanged
    .Select(e => e.NewTextValue ?? string.Empty)
    .Throttle(TimeSpan.FromMilliseconds(300))
    .DistinctUntilChanged()
    .Subscribe(text => vm.SearchText.Value = text);
```

### Known Observables 0.1.4 limitation (discovered via this project)

`ValidatePathTemplate` in `Observables.RestAPI.SourceGenerators.Shared/Parser.cs`
requires the set of path placeholders to **equal** the set of all non-CancellationToken
parameter names. It does **not** exclude `[Query]`/`[Body]`/`[Header]` parameters,
so query/body parameters cannot coexist with path parameters on the same method
in 0.1.4. The `ClassifyParameter` routine correctly identifies `[Query]` parameters,
but the validation runs before classification and rejects the interface.

**Workaround in GitPulse**: API methods use only path parameters (no `[Query]`).
Pagination and filtering are handled via a custom `HttpMessageHandler` or
dedicated query-only methods until the upstream validation is relaxed.

This is exactly the kind of real-world friction a showcase project should
surface — tracked for upstream feedback.

## Authentication & Credentials

- PAT (Personal Access Token) as the sole auth method (GitHub App OAuth deferred)
- `ICredentialStore` abstraction:
  - Windows: DPAPI (CurrentUser scope)
  - Android: `SecureStorage` (MAUI Essentials)
- `GitHubClientFactory` builds `HttpClient` with `Authorization: Bearer <PAT>`,
  `Accept: application/vnd.github+json`, `X-GitHub-Api-Version: 2022-11-28`,
  `User-Agent: GitPulse`

## Milestone Roadmap

| Milestone | Content | Observables domains |
|-----------|---------|---------------------|
| **M0** ✅ | Project skeleton: solution, projects, Nuke, CI, docs, empty MAUI app builds | — |
| **M1** | Auth + repository list browsing | RestAPI + Events |
| **M2** | Issue/PR list & detail | RestAPI + Events |
| **M3** | Issue/PR CRUD (comments, state, labels) | RestAPI |
| **M4** | Notification center (polling-simulated realtime) | Events (+ Sse optional) |
| **M5** | File browsing & editing | RestAPI |
| **M6** | PR review & merge | RestAPI |
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
