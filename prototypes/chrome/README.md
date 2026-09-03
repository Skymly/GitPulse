# Chrome prototype (throwaway)

Not production App code. Visual language: issue #450.

## Pick (variant W)

Windows chrome atoms:

- **Tab bar** — A: top NavigationView (icon + label, filled selected) on Mica under the TitleBar. Hidden on stacked detail.
- **List row** — A: two-line flush (Domain Icon + title; meta row with state icon+text). Unread = Accent edge. Not Desktop-density, not cards.
- **Detail header** — B: Back · parent Surface · identity, then Domain Icon + state text + page title 20 + caption.
- **GitPulse Mark** — `mark.svg` (pulse + graph, `currentColor`, 16px).

Android bottom destinations are [Dual-platform rule application](https://github.com/Skymly/GitPulse/issues/455), not this ticket.

## How to view

Open `index.html` or serve this folder. `?variant=A|B|C|W`. Arrow keys cycle. List/Detail and Light/Dark are evaluation chrome only.
