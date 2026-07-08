# Contributing to GitPulse

Thank you for your interest in GitPulse. This document covers contribution
workflow and release conventions.

## Contributing

### Before you open a PR

1. Build and test locally (same as CI):

   ```powershell
   ./build.ps1 --target CiAll --configuration Release
   ```

2. If you change user-facing behavior, update documentation:
   - `CHANGELOG.md` `[Unreleased]`
   - `docs/spec/` or `docs/design/` when API or implementation contracts change
   - `docs/ROADMAP.md` when milestone status changes
   - `README.md` for user-visible setup or feature list changes

3. Follow the **documentation-driven development** standard in
   [docs/DOCUMENTATION.md](./docs/DOCUMENTATION.md). New milestones require
   RFC + Plan before implementation.

4. Follow existing project layout and conventions (see [AGENTS.md](./AGENTS.md)
   and [docs/DEVELOPMENT.md](./docs/DEVELOPMENT.md)).

### PR conventions

- **Titles and descriptions**: English.
- **Scope**: Prefer one layer per PR (App, Core, GitHubApi, Services, Tests, or
  Solution Items for root props / `build/` / `.github/`).
- **Commits**: English; do not mention AI or agent tools in commit messages.
- **Do not** create tags unless the task explicitly requests a release.

## Build & CI (Nuke)

Build orchestration uses [Nuke](https://nuke.build). The CI workflow calls Nuke
targets; the same commands run locally.

```powershell
# CI-equivalent (what the workflow runs)
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
dotnet run --project build/_build.csproj -- --target CiAll --configuration Release
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
