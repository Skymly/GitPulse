# GitPulse

A cross-platform .NET 10 MAUI GitHub client showcasing [Skymly/Observables](https://github.com/Skymly/Observables) (declarative RestAPI + R3 event bridge). Windows is the primary target, Android is secondary, and iOS/MacCatalyst are deferred ([ADR-005](docs/adr/ADR-005-windows-first-platform-strategy.md)).

Read [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md) before modifying code. Current roadmap and backlog live solely in [`docs/ROADMAP.md`](docs/ROADMAP.md).

## Build & Verify

```powershell
# Mandatory library & test gate for every PR:
./build.ps1 --target CiLib --configuration Release

# When touching App or Android:
./build.ps1 --target CiAndroid --configuration Release

# Full verification (Format + Windows + Android):
./build.ps1 --target CiAll --configuration Release
```

## Architecture & Invariants

- **Strict unidirectional layering**: `App -> ViewModels -> GitHubApi -> Core` and `App -> Services -> GitHubApi -> Core`. ViewModels must never reference MAUI or Services; PAT credentials flow through `ICredentialStore` with platform implementations isolated under `Platforms/` ([ADR-004](docs/adr/ADR-004-pat-auth-platform-credential-store.md)). See [`docs/design/Architecture.md`](docs/design/Architecture.md).
- **Single-module PR boundary**: Keep each PR scoped to one module (`App`, `ViewModels`, `Core`, `GitHubApi`, `Services`, `Tests`, or `Docs-Repo` covering `docs/`, root markdown, `build/`, `.github/`).
- **Stack**: R3 `BindableReactiveProperty<T>`, `Observables.RestAPI.R3` / `Events.R3`, and CommunityToolkit.Mvvm `[RelayCommand]`; package versions live in [`Directory.Packages.props`](Directory.Packages.props).

## Gotchas

- Release builds enforce `TreatWarningsAsErrors=true` — a passing Debug build does not guarantee CI passes.
- Pagination call chains must follow [ADR-018](docs/adr/ADR-018-per-request-pagination-options.md) and [`docs/design/RestApi.md`](docs/design/RestApi.md); `SearchBar.Events()` routes through the public event adapter ([ADR-015](docs/adr/ADR-015-searchbar-public-event-adapter.md)).
- Use file-scoped namespaces; Nullable, LangVersion, and CPM settings are inherited from [`Directory.Build.props`](Directory.Build.props).

## Docs & Agent Config

- **Domain glossary**: [`docs/CONTEXT.md`](docs/CONTEXT.md) (intentionally kept under `docs/` because immutable ADRs and core source comments reference that path)
- **Design & governance**: [`docs/DOCUMENTATION.md`](docs/DOCUMENTATION.md), [`docs/adr/`](docs/adr/), [`docs/design/`](docs/design/), [`CHANGELOG.md`](CHANGELOG.md)
- **Issue routing & triage labels**: [`docs/agents/`](docs/agents/)

## Repo-Specific Conventions

- Commit messages, issues, and PRs: **English** (fill out [`.github/pull_request_template.md`](.github/pull_request_template.md)).
- Update the matching Design Doc, ADR (immutable once accepted; Supersede only), `CHANGELOG.md` (`[Unreleased]`), and `docs/ROADMAP.md` in the same PR when applicable.
- Do not commit, push, tag, or release unless explicitly requested.
