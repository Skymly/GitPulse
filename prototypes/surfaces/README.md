# Top-ranked surfaces prototype (throwaway)

Not production App code. Chrome atoms are locked from [`prototype/ux-chrome`](https://github.com/Skymly/GitPulse/tree/prototype/ux-chrome) variant W. Visual language: issue #450. Interaction standard: issue #451.

Three variants of **Pull Request** (Conversation + Files, including Diff view) and **Search** (typed Search + Review / Assigned / Mentions Inboxes), plus pick **W**.

```
Three variants of PR + Search, switchable via ?variant=, locked W chrome.
```

| Key | Name | Pull Request | Search |
| --- | --- | --- | --- |
| A | Stacked regions | In-page Conversation \| Files tabs; single-column regions | 2×2 hub grid (today’s shape) |
| B | Split + rail | Conversation timeline + metadata rail; Files list \| diff | Always-on SearchBar + one-row hubs |
| C | Files-first / cards | Files tree + diff as default; Conversation is timeline-only | Typed Search landing + inbox count cards |
| W | Pick | B Conversation + B Files; default region Conversation | B one-row hubs; SearchBar only on typed Search (from A) |

## Pick (variant W)

- **Pull Request Conversation** — B: lifecycle strip under the detail header (Merge / Close / Convert to draft), timeline (body, reviews, comments), metadata + Gate Rollup in a right rail. Default region is Conversation.
- **Pull Request Files** — B: file list \| diff split. Diff view lives here.
- **Search** — one-row hubs (Search / Review / Assigned / Mentions). SearchBar, submit, and type tabs (Repos / Issues / PRs / Code) only on typed Search. Inboxes are the list only.
- **Rejected** — A’s buried Lifecycle card and accordion Files; B’s SearchBar on Inboxes; C’s Files-first default and inbox-card landing.

## How to view

Open `index.html` or serve this folder. `?variant=A|B|C|W`. Arrow keys cycle. Surface / scene / Light/Dark are evaluation chrome only.
