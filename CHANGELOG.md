# Changelog

All notable changes to GitPulse are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Versions are derived automatically from Git tags by MinVer.

## [Unreleased]

### Added — M3 CRUD operations (issue/PR write via Observables 0.1.5)

- Upgraded to `Observables.RestAPI.R3` 0.1.5 — the upstream OBS3004 fix
  (`ValidatePathTemplate` now runs after parameter classification) allows
  path + `[Body]` parameters to coexist on the same declarative interface
  method. See https://github.com/Skymly/Observables/issues/111.
- `IGitHubReposApi` new CRUD methods (all declarative, path + `[Body]`):
  - `CreateIssue` — POST /repos/{owner}/{repo}/issues
  - `UpdateIssue` — PATCH /repos/{owner}/{repo}/issues/{number} (state toggle)
  - `CreateIssueComment` — POST /repos/{owner}/{repo}/issues/{number}/comments
  - `ListIssueLabels` — GET /repos/{owner}/{repo}/issues/{number}/labels
  - `ReplaceIssueLabels` — PUT /repos/{owner}/{repo}/issues/{number}/labels
- Request body DTOs (`IssueCreateRequest`, `IssueUpdateRequest`,
  `CommentCreateRequest`, `LabelsReplaceRequest`) in Core/Models.
- `IssueDetailViewModel` — `AddCommentCommand`, `ToggleStateCommand`,
  `SaveLabelsCommand` with `CommentInput`/`LabelInput` reactive properties
  and `IsSaving` state.
- `PullRequestDetailViewModel` — `AddCommentCommand` (PR comments use the
  issue comments endpoint), `ToggleStateCommand` (PR state via issue PATCH).
- `CreateIssueViewModel` — new ViewModel for the "New Issue" form with
  `TitleInput`/`BodyInput`/`LabelsInput` and `CreateCommand`; navigates
  to issue detail on success.
- `CreateIssuePage` (XAML) — title entry, markdown body editor,
  comma-separated labels entry, create button, error banner.
- `IssueDetailPage` — Close/Reopen button, labels editor (entry + save),
  comment input (editor + comment button).
- `PullRequestDetailPage` — Close/Reopen button, comment input.
- `IssuesPage` — "+ New Issue" button → `CreateIssuePage`.
- AppShell registered `CreateIssuePage` route; MauiProgram DI registered
  `CreateIssueViewModel` + `CreateIssuePage`.
- Tests: `IssueDetailViewModelCrudTests` (3 tests: add comment, toggle
  state, empty input guard), `CreateIssueViewModelTests` (4 tests:
  initialize, empty title guard, valid create, no-token guard). 35 total.

### Added — Pagination (server-side via `ApiResponse<T>` + `Link` header)

- `IGitHubReposApi` list methods now return `Observable<ApiResponse<T>>`
  (instead of `Observable<T>`) to expose response headers. The
  `ApiResponse<T>` wrapper provides `Content` (deserialized body) and
  `Headers` (including the `Link` header for pagination detection).
  Renamed: `ListMyRepos` → `ListMyReposPaged`, `ListIssues` →
  `ListIssuesPaged`, `ListPullRequests` → `ListPullRequestsPaged`.
- `GitHubQueryHandler` (Core/Http) — `DelegatingHandler` that injects
  `page`, `per_page`, and `state` query parameters into outgoing requests.
  Works around the Observables 0.1.4 OBS3004 limitation (path + `[Query]`
  parameters cannot coexist). The handler is per-ViewModel-load-cycle:
  `IGitHubClientFactory.CreatePagedClientAsync` returns
  `(HttpClient, GitHubQueryHandler)` so the ViewModel can set `Page`/`State`
  before each request.
- `LinkHeaderParser` (Core/Http) — parses RFC 8288 `Link` headers to
  extract `rel="next"` URL and page number. Uses source-generated regex.
- `IGitHubClientFactory.CreatePagedClientAsync` — new method returning a
  client backed by `GitHubQueryHandler` alongside the handler instance.
- ViewModels (`ReposViewModel`, `IssuesViewModel`, `PullRequestsViewModel`)
  now support "Load more" pagination: `CanLoadMore` reactive property
  driven by `Link` header `rel="next"` presence, `LoadMoreCommand`
  increments page and appends results. State filter changed from
  client-side filtering to server-side (via `GitHubQueryHandler.State`),
  triggering a reload from page 1 on filter change.
- UI: "Load more" button added to `ReposPage`, `IssuesPage`,
  `PullRequestsPage` (visible when `CanLoadMore` is true).
- Tests: `LinkHeaderParserTests` (6 tests), updated
  `IssuesViewModelTests` and `PullRequestsViewModelTests` for paged API
  + `MockHttpHandler` now supports `Link` header in canned responses.

### Added — Markdown rendering (issue/PR body + comments)

- `Indiko.Maui.Controls.Markdown` 1.5.0 — native MAUI markdown viewer
  (no WebView). Renders headings, bold/italic/strikethrough, inline code,
  code blocks, lists, tables, blockquotes, links, images. Uses
  `MarkdownThemeDefaults.GitHub` theme with `UseAppTheme` for light/dark.
- `IssueDetailPage` and `PullRequestDetailPage` — issue/PR body and
  comment bodies now rendered as markdown via `MarkdownView` instead of
  plain `Label`. Replaces the previous `LineBreakMode="WordWrap"` text
  display with full markdown formatting.
- `MauiProgram` registers `UseMarkdownView()`.

### Changed — Services layer reorganization

- Moved `GitHubClientFactory` from `GitPulse.GitHubApi` to `GitPulse.Services`
  (namespace `GitPulse.GitHubApi` → `GitPulse.Services`). The factory is auth
  infrastructure, not a declarative API contract — it belongs with other
  service-layer concerns. The GitHubApi layer now contains only Observables.RestAPI
  declarative interfaces.
- Credential store implementations remain in `App/Platforms/{Windows,Android}/`
  because they use platform-specific APIs (DPAPI / MAUI SecureStorage) that
  require the MAUI host. `ICredentialStore` abstraction stays in Core.
- Updated `AGENTS.md` layer table to reflect the new boundary.

### Added — M2 completion (PR list & detail + ViewModel testability)

- New `GitPulse.ViewModels` project (net10.0) — ViewModels extracted from the
  MAUI App project so they are testable without a MAUI host. References Core
  + GitHubApi + R3 + CommunityToolkit.Mvvm. The App project now references
  ViewModels; all XAML `xmlns:vm` updated to the new assembly.
- New `IBrowserLauncher` abstraction (Core) — decouples ViewModels from
  MAUI `Launcher.OpenAsync`. `BrowserLauncher` implementation lives in the
  App layer; `FakeBrowserLauncher` in tests records opened URLs.
- `PullRequestsViewModel` — mirrors `IssuesViewModel` with
  `ListPullRequests` and reactive state filtering (open/closed/all).
- `PullRequestDetailViewModel` — uses `GetPullRequest` +
  `ListIssueComments` (PR conversation comments share the issue comments
  endpoint in the GitHub REST API).
- `PullRequestsPage` (XAML) — CollectionView with state filter tabs,
  draft/merged badges, head→base ref display, back navigation, PR
  selection → detail navigation.
- `PullRequestDetailPage` (XAML) — PR header (state, draft, merged,
  author, date, head→base, body), "Open in browser" button, conversation
  comments via BindableLayout, error banner, loading indicator.
- Shell navigation: `IssuesPage` → "PRs →" button →
  `PullRequestsPage?owner=&repo=` → tap PR →
  `PullRequestDetailPage?owner=&repo=&number=`.
- AppShell registered `PullRequestsPage` + `PullRequestDetailPage` routes;
  MauiProgram DI registered 2 new ViewModels + 2 new Pages +
  `IBrowserLauncher`.
- Test infrastructure: `MockHttpHandler` (canned JSON by URI path),
  `FakeGitHubClientFactory` (HttpClient with mock handler + optional auth),
  `FakeBrowserLauncher`.
- 9 new ViewModel tests (IssuesViewModel: Initialize, no-token error, Load
  success, state filter all/closed; PullRequestsViewModel: Initialize,
  Load success, state filter closed, no-token error). Total: 23 tests
  passing.
- Added missing semantic color resources to `Colors.xaml`: `Green700`,
  `Purple700`, `Orange900`, `Red100`, `Red900` (previously referenced by
  IssuesPage/IssueDetailPage but undefined — latent runtime bug fixed).

### Added — M2 (issue/PR list & detail)

- New Core models: `Label`, `Comment`, `PullRequest`, `PullRequestRef`.
  `Issue` expanded with `Labels`, `CommentsCount`, `MilestoneTitle`,
  `PullRequestRef` (for PR detection).
- Extended `IGitHubReposApi` with 5 new declarative methods:
  `GetIssue`, `ListIssueComments`, `ListPullRequests`, `GetPullRequest`.
- `IssuesViewModel` with R3 state filtering (open/closed/all) via
  `BindableReactiveProperty<string> StateFilter`.
- `IssuesPage` (XAML) with CollectionView, state filter tabs (Open/Closed/All),
  back navigation, issue selection → detail navigation.
- `IssueDetailViewModel` loading issue + comments sequentially via
  `GetIssue` + `ListIssueComments`.
- `IssueDetailPage` (XAML) with issue header (state, author, date, body),
  comments list via BindableLayout, error banner, loading indicator.
- Shell navigation: `ReposPage` → tap repo → `IssuesPage?owner=&repo=` →
  tap issue → `IssueDetailPage?owner=&repo=&number=`.
- New converters: `StringEqualsConverter`, `NotNullConverter`,
  `IntGreaterThanZeroConverter`.
- 6 new unit tests for Issue/Comment/PullRequest/Label model defaults.
  Total: 14 tests passing.

### Added — M1 (auth + repository list browsing)

- `ICredentialStore` platform implementations:
  - Windows: DPAPI (CurrentUser scope), persisted to `%APPDATA%/GitPulse/token.bin`
  - Android: MAUI `SecureStorage` (Android KeyStore + EncryptedSharedPreferences)
- `SettingsViewModel` with R3 `BindableReactiveProperty` state (TokenInput,
  HasToken, StatusMessage, IsBusy) and `SaveToken`/`ClearToken` commands.
- `SettingsPage` (XAML) with PAT entry, save/clear buttons, status feedback.
- `ReposViewModel` showcasing `Observables.RestAPI.R3` — calls
  `IGitHubReposApi.ListMyRepos()` via `RestService.For<T>`, manages state
  via R3 `BindableReactiveProperty` (SearchText, IsLoading, IsAuthenticated,
  ErrorMessage) and `ObservableCollection<Repo>`.
- `ReposPage` (XAML) with `CollectionView` repo list, search bar, error
  banner, loading indicator.
- Reactive search debounce pipeline in `ReposPage.xaml.cs` — R3 `Subject<T>`
  bridge → `Debounce(300ms)` → `DistinctUntilChanged` → `ObserveOn` →
  ViewModel `SearchText`. Manual bridge documented as workaround for the
  Observables.Events.R3 + MAUI internal interface issue (see Known limitations).
- `AppShell` updated to `TabBar` navigation (Repos + Settings tabs).
- Global XAML converters: `InvertedBoolConverter`, `StringToBoolConverter`.
- DI registration for pages, ViewModels, and platform credential stores.
- 6 unit tests for `GitHubClientFactory` (auth header, base address, accept,
  API version, user agent, no-token case). Total: 8 tests passing.

### Fixed

- CI: MAUI Windows target failed with `NETSDK1112` (runtime pack not
  downloaded). Added a second `DotNetRestore` step in the Nuke `Restore`
  target with explicit `win-x64` RID so the runtime pack is fetched.

### Known limitations

- **Observables 0.1.4 RestAPI path validation**: `ValidatePathTemplate` does
  not exclude `[Query]`/`[Body]` parameters when comparing path placeholders
  to method parameters, so query/body parameters cannot coexist with path
  parameters on the same method. GitPulse works around this by using only
  path parameters on API methods; pagination/filtering will use a custom
  `HttpMessageHandler` until the upstream validation is relaxed. Tracked
  for upstream feedback.
- **Observables 0.1.4 Events + MAUI internal interfaces**: The source-
  generated `.Events()` extension for `Microsoft.Maui.Controls.SearchBar`
  emits code referencing `IControlsVisualElement` (internal in MAUI),
  causing CS0122. GitPulse bridges the event manually via an R3 `Subject<T>`
  in the page code-behind; the reactive pipeline itself is unaffected.
  Tracked for upstream feedback.

### Added — M0 (project skeleton)

- Solution `GitPulse.slnx` with five projects:
  - `GitPulse.App` (.NET MAUI, `net10.0-windows10.0.19041.0` + `net10.0-android`)
  - `GitPulse.Core` (domain models, abstractions)
  - `GitPulse.GitHubApi` (Observables.RestAPI declarative interfaces)
  - `GitPulse.Services` (auth, caching, polling — placeholder)
  - `GitPulse.Tests` (xunit.v3)
- `Directory.Build.props` (net10.0, Nullable, LangVersion latest, MinVer,
  TreatWarningsAsErrors in Release) and `Directory.Packages.props` (central
  package management).
- Nuke build orchestration (`build/_build.csproj` + `Program.cs`,
  `.nuke/`, `build.ps1`) with targets: `Ci`, `CiLib`, `CiAll`, `Test`,
  `Format`, `FormatFix`, `Publish`, `PublishVerify`, `Release`.
- GitHub Actions CI workflow (`.github/workflows/build-and-test.yml`).
- Documentation: `README.md`, `AGENTS.md`, `CONTRIBUTING.md`, `CHANGELOG.md`.
- `MauiProgram` configured with `UseR3()` and `IGitHubClientFactory` DI registration.
- `IGitHubReposApi` declarative interface demonstrating `Observables.RestAPI.R3`.
- `GitHubClientFactory` building `HttpClient` with GitHub headers
  (Authorization, Accept, X-GitHub-Api-Version, User-Agent).
- Domain models: `Repo`, `Issue`, `User`.
- Abstractions: `ICredentialStore`, `IGitHubClientFactory`.
- Sanity unit tests for domain model defaults.

### Known limitations

- **Observables 0.1.4 RestAPI path validation**: `ValidatePathTemplate` does
  not exclude `[Query]`/`[Body]` parameters when comparing path placeholders
  to method parameters, so query/body parameters cannot coexist with path
  parameters on the same method. GitPulse works around this by using only
  path parameters on API methods; pagination/filtering will use a custom
  `HttpMessageHandler` until the upstream validation is relaxed. Tracked
  for upstream feedback.
