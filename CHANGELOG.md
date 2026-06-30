# Changelog

All notable changes to GitPulse are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Versions are derived automatically from Git tags by MinVer.

## [Unreleased]

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
