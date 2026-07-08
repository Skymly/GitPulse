# GitPulse

[![CI](https://github.com/Skymly/GitPulse/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/Skymly/GitPulse/actions/workflows/build-and-test.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
![.NET](https://img.shields.io/badge/.NET-10-512BD4.svg)
![MAUI](https://img.shields.io/badge/MAUI-10-512BD4.svg)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Android-blue.svg)

A cross-platform GitHub client built with .NET MAUI, serving as a real-world
showcase application for the [Observables](https://github.com/Skymly/Observables)
source-generator library (declarative reactive HTTP / events bridging for R3).

This is not a toy demo — it is a working tool the author uses day-to-day and
iterates on until genuinely useful.

## Quick Start

### Prerequisites

- .NET 10 SDK
- .NET MAUI workload (`dotnet workload install maui`)
- Windows (primary) or Android (secondary target)

### Build & Run

```powershell
git clone https://github.com/Skymly/GitPulse.git
cd GitPulse

# Via Nuke (CI-authoritative)
./build.ps1 --target Ci --configuration Release

# Or traditional dotnet
dotnet build GitPulse.slnx -c Release
dotnet run --project src/GitPulse.App/GitPulse.App.csproj -c Debug -f net10.0-windows10.0.19041.0
```

### Configure GitHub Access

On first launch, open **Settings** and paste a GitHub Personal Access Token
(PAT). The token is encrypted at rest:

- Windows: DPAPI (CurrentUser scope)
- Android: SecureStorage

## Features

The application is under active development. See the [milestone roadmap](./docs/ROADMAP.md)
for the planned feature progression.

Maintainer documentation (documentation-driven development, ADRs, specs) lives in
[`docs/`](./docs/README.md) — adapted from the
[DesignPatterns](https://github.com/Skymly/DesignPatterns) documentation system.

### Showcase: Observables integration

GitPulse demonstrates two Observables domains today, with more planned:

| Domain | Usage |
|--------|-------|
| `Observables.RestAPI.R3` | Declarative GitHub REST API interfaces — write the interface, get `Observable<T>` proxies |
| `Observables.Events.R3` | MAUI control events → `Observable<T>` streams (debounced search, scroll-to-load, etc.) |

Example — declarative API:

```csharp
public interface IGitHubReposApi
{
    [Get("/repos/{owner}/{repo}")]
    Observable<Repo> GetRepo(string owner, string repo);
}

var api = RestService.For<IGitHubReposApi>(httpClient);
using var d = api.GetRepo("Skymly", "Observables")
    .Subscribe(repo => Console.WriteLine(repo.FullName));
```

## Tech Stack

- .NET 10 + .NET MAUI
- R3 + R3Extensions.Maui (reactive core)
- Observables.RestAPI.R3 + Observables.Events.R3 (source-generated reactive bridges)
- CommunityToolkit.Mvvm
- MVVM architecture, multi-project solution (App / Core / GitHubApi / Services)
- Nuke build orchestration
- MinVer (Git-tag-based versioning)

## Project Layout

```
src/
  GitPulse.App/         — MAUI UI (Views, ViewModels, DI, platform entry points)
  GitPulse.Core/        — Domain models, abstractions (no UI/IO)
  GitPulse.GitHubApi/   — Observables.RestAPI declarative interfaces + DTOs
  GitPulse.Services/    — Auth, caching, polling, app config
tests/
  GitPulse.Tests/       — Unit tests
build/                  — Nuke build script
docs/                   — Documentation-driven dev (RFC, ADR, spec, design)
.nuke/                  — Nuke parameters & schema
```

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

```powershell
./build.ps1 --target CiAll --configuration Release
./build.ps1 --target Publish --configuration Release --runtime win-x64
```

## Versioning

Versions are derived automatically from Git tags by [MinVer](https://github.com/adamralph/minver).
Pushing a `v*` tag triggers the CI release job and sets the version.

```powershell
git tag v0.1.0
git push origin v0.1.0
```

## License

MIT — see [LICENSE](./LICENSE).
