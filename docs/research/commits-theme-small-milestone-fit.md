# Research: commit history — small-milestone fit

| Field | Value |
|-------|-------|
| **Ticket** | [#164](https://github.com/Skymly/GitPulse/issues/164) (part of [#163](https://github.com/Skymly/GitPulse/issues/163)) |
| **Theme** | **commit history** |
| **Date** | 2026-08-19 |

## Coverage

GitPulse has no commit list. `PullRequest.Commits` is a count only. File browser edits files; Search can open a file at a SHA. Repo detail has Issues / PRs / Files / Actions entries, no Commits.

## REST

- `GET /repos/{owner}/{repo}/commits` — list commits on the default branch; `Link` pagination; items have `sha`, `html_url`, nested `commit.message` / `commit.author` ([List commits](https://docs.github.com/en/rest/commits/commits?apiVersion=2022-11-28#list-commits)).
- `GET /repos/{owner}/{repo}/commits/{ref}` — single commit + files. Inflates the slice.
- `GET /repos/{owner}/{repo}/pulls/{number}/commits` — PR-only; adjacent, not required for a repo hub.
- Compare / blame / stats — outsized.

Fine-grained PAT: Contents read. Classic `repo` already used.

## Size hazards

In-app commit diff, branch picker, PR commits, blame, compare, creating commits.

Small slice: **repo detail → paged commit list → open `html_url`**.
