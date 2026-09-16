# Contributing to GitPulse

Thank you for your interest in GitPulse. This document covers contribution
workflow and release conventions.

## Contributing

### Before you open a PR

1. Build and test locally (same as CI). Every PR must pass `CiLib`. Run
   `CiAll` when the PR changes App or formatting:

   ```powershell
   ./build.ps1 --target CiLib --configuration Release
   ./build.ps1 --target CiAll --configuration Release   # App or formatting
   ```

2. If you change user-facing behavior, update documentation:
   - `CHANGELOG.md` `[Unreleased]`
   - `docs/design/` when API, model, or implementation contracts change
   - `docs/ROADMAP.md` when milestone status changes
   - `README.md` for user-visible setup or feature list changes

3. Follow [docs/DOCUMENTATION.md](./docs/DOCUMENTATION.md): record breaking
   architecture or API decisions in an ADR and update the relevant Design Doc.

4. Follow existing project layout and conventions (see [AGENTS.md](./AGENTS.md)
   and [docs/DEVELOPMENT.md](./docs/DEVELOPMENT.md)).

### PR conventions

- **Titles and descriptions**: English.
- **Scope**: Prefer one layer per PR: App, ViewModels, Core, GitHubApi,
  Services, Tests (`tests/GitPulse.Tests/`), or Docs-Repo (`docs/`, root
  markdown, `build/`, `.github/`). Windows/Android UI test projects are not a
  separate PR module; they ride with App changes and `CiAll`.
- **Commits**: English.

## Build & CI (Nuke)

Build orchestration uses [Nuke](https://nuke.build). The CI workflow calls Nuke
targets; the same commands run locally.

```powershell
# Library tests (required on every PR)
./build.ps1 --target CiLib --configuration Release

# Format + Windows + Android (required when App or formatting changed)
./build.ps1 --target CiAll --configuration Release

# Quick local build + test
./build.ps1 --target Ci

# Check formatting without changing files
./build.ps1 --target Format

# Apply formatting
./build.ps1 --target FormatFix

# Publish self-contained Windows executable
./build.ps1 --target Publish --configuration Release --runtime win-x64
```

Equivalent dotnet CLI:

```powershell
dotnet run --project build/_build.csproj -- --target CiLib --configuration Release
```

## Releases and versioning

Versions are derived automatically from Git tags by
[MinVer](https://github.com/adamralph/minver). Pushing a `v*` tag (e.g. `v0.1.0`)
triggers the CI release job and sets the assembly/package version. Between tags,
the version auto-increments as a pre-release.

```powershell
git tag v0.1.0
git push origin v0.1.0   # triggers CI release job
```

## License

By contributing, you agree that your contributions will be licensed under the
same license as the project (MIT — see [LICENSE](./LICENSE)).
