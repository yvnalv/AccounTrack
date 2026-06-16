# Brand (placeholder)

> **Placeholder identity.** The product owner will design the final logo later; this exists so the
> app shell, favicon, and login screen have a consistent, non-generic mark to build against. Keep it
> simple and easy to swap.

## Mark

An **ascending three-bar mark** (white, rounded) inside a rounded-square tile in brand teal
`#007E6E`. The rising bars read as *tracking / accounting growth* — on-theme without being a literal
dollar sign or a generic letter. Renders without any font dependency.

- File: [brand/logo-mark.svg](brand/logo-mark.svg) — 40×40, rounded-square (radius 11).
- App-icon / favicon source. For small favicons (16/32px) keep just the tile + bars.

## Wordmark / lockup

“Accoun**track**” — the **“track”** is set in brand teal `#007E6E`, the rest in `--text`, nodding to
the *account + track* origin. Plus Jakarta Sans, 700, slight negative tracking.

- File: [brand/logo-lockup.svg](brand/logo-lockup.svg) — mark + wordmark, for the sidebar header,
  login, and docs.

## Variants

- **Light background:** wordmark `#15171A` + teal “track” (as in the lockup file).
- **Dark background / sidebar:** wordmark switches to `#F2F3F5`; mark tile + bars unchanged (the teal
  tile reads fine on the near-black sidebar). At build time the wordmark uses `currentColor` so it
  adapts to the theme automatically.

## Usage

- Clear space ≥ the tile's corner radius on all sides; don't recolor the tile, stretch, or add
  effects. Minimum mark size 24px.
- The teal tile is the only place the brand accent appears "as a fill" at rest besides primary
  actions — keep it special.
