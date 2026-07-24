## Summary

<!-- What does this PR do and why? Write in English. -->

## Related Issue

Closes #

## Solution module

<!-- Must match a single module in AGENTS.md / docs/design/Architecture.md. -->

- [ ] App (`src/GitPulse.App/`)
- [ ] ViewModels (`src/GitPulse.ViewModels/`)
- [ ] Core (`src/GitPulse.Core/`)
- [ ] GitHubApi (`src/GitPulse.GitHubApi/`)
- [ ] Services (`src/GitPulse.Services/`)
- [ ] Tests (`tests/GitPulse.Tests/`)
- [ ] Docs / Repository (`docs/`, `README.md`, `CONTRIBUTING.md`, `AGENTS.md`, `build/`, `.github/`)

## Type of change

- [ ] Bug fix
- [ ] Feature
- [ ] Refactor (no behavior change)
- [ ] Docs / repo metadata only

## Test plan

- [ ] `./build.ps1 --target CiLib --configuration Release`
- [ ] `./build.ps1 --target CiAndroid --configuration Release` (if App / Android-related)
- [ ] `./build.ps1 --target CiAll --configuration Release` (if App or formatting changed)

## Breaking changes

- [ ] None
- [ ] Yes — describe migration steps:

## Checklist

- [ ] This PR touches **only one** solution module
- [ ] Commit messages are in **English** (no AI/agent tooling mentions)
- [ ] PR description is **English** only

## Documentation checklist

<!-- See docs/DOCUMENTATION.md -->

- [ ] Design Doc updated if API, model, or implementation changed
- [ ] User-facing documentation synced if needed
- [ ] `CHANGELOG.md` / `docs/ROADMAP.md` updated if applicable
- [ ] No documentation changes needed
