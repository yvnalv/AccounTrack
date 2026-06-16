# Design Language

The single source of truth for Accountrack's visual styling. These tokens map directly to the
Tailwind theme config + CSS custom properties at build time, so the app stays consistent.

> **State: 🟢 LOCKED (v1).** Derived from the Decko reference (R1, see [REFERENCES.md](REFERENCES.md))
> but with our own **brand accent (deep teal `#007E6E`)** instead of Decko's lime. Color hexes other
> than the accent are still eyeballed from the reference (marked _approx_) and may be nudged for
> contrast during build, but the palette structure is fixed.

## Locked decisions

- ✅ **Brand accent = teal `#007E6E`** (not lime). Used **sparingly**: primary buttons, active nav,
  focus ring, key chart series, links. A **separate semantic palette** carries finance meaning
  (paid/credit = green, overdue/negative = red, pending = amber) so brand ≠ status.
- ✅ **Two-register density.** Spacious register for dashboard + document/detail headers; dense,
  tabular register for lists, ledgers, document line-items. One shared token set.
- ✅ **Light + Dark from day one**, with a **top-bar toggle** (persisted per user; respects
  `prefers-color-scheme` on first load).
- ✅ **App shell = Decko structure**: dark sidebar (in **both** themes) + light/dark canvas, soft
  cards with large radius, rounded charts with black tooltip, `⌘K` search.
- ✅ **Typography = Plus Jakarta Sans** (SIL OFL 1.1 — free for commercial use).
- ✅ **Icons = Lucide** (MIT).
- ✅ **Locale/number default = `id-ID`** (`1.234.567,89`); company sets functional currency (IDR
  default, ADR-0012). **Tabular figures** on all numbers.
- ✅ **Logo = simple placeholder lettermark** (this doc set, see [BRAND.md](BRAND.md)); product owner
  will design the final logo later.

## 0. Principles

- **Calm, dense, professional** — used all day; clarity & scannability over decoration.
- **Crafted, not template** — restrained palette, real line icons, consistent 4px rhythm. No purple
  gradients, no emoji, no stock illustrations.
- **Data-first** — money/tables/forms are first-class; tabular figures everywhere numbers appear.
- **Accessible** — WCAG AA contrast, full keyboard support, visible teal focus ring.

## 1. Color

### Brand / accent (teal)
| Token | Value | Notes |
|---|---|---|
| `--accent` | `#007E6E` | brand; primary button bg, active nav, links, key chart series |
| `--accent-hover` | `#006B5D` | hover |
| `--accent-active` | `#005C50` | pressed; also use where small text-on-accent needs AA 4.5:1 |
| `--accent-soft` | `#E2F1EE` (light) · `rgba(0,126,110,.16)` (dark) | subtle tints, selected rows, soft badges |
| `--accent-contrast` | `#FFFFFF` | text/icon on accent fills |

### Neutrals
| Role | Light | Dark | Notes |
|---|---|---|---|
| `--bg` (canvas) | `#F4F5F7` _approx_ | `#0F1012` | page background |
| `--surface` (card) | `#FFFFFF` | `#16181B` | cards, menus, inputs |
| `--surface-2` | `#F7F8FA` | `#1C1F23` | subtle fills, table header, hover |
| `--sidebar` | `#0F1012` | `#0B0C0E` | **dark in both themes** |
| `--border` | `#E7E9EC` _approx_ | `#26292E` | hairline dividers/borders |
| `--text` | `#15171A` | `#F2F3F5` | primary |
| `--text-muted` | `#6B7280` _approx_ | `#9BA1A8` | labels, secondary |

### Semantic (distinct from brand teal)
| Role | Light | Dark | Used for |
|---|---|---|---|
| `--positive` | `#16A34A` | `#22C55E` | paid, credit, +delta, posted |
| `--negative` | `#E5484D` _approx_ | `#F2555A` | overdue, −delta, validation errors |
| `--warning` | `#D97706` | `#F59E0B` | pending approval, due soon |
| `--info` | `#2563EB` | `#3B82F6` | informational |

> **Finance note:** negative amounts render `(1.234,56)` in `--negative`; status chips use the
> semantic palette, never brand teal — so a teal "Post" button never reads as a "Paid" status.

## 2. Typography

- **Family:** **Plus Jakarta Sans** (Google Fonts / SIL OFL 1.1). Self-hosted (woff2) for perf/offline.
- **Numerics:** `font-variant-numeric: tabular-nums lining-nums` on every amount/qty; **id-ID**
  formatting by default.
- **Scale (rem / px):** 0.75/12 · 0.8125/13 · 0.875/14 (**body**) · 1/16 · 1.25/20 · 1.5/24 ·
  1.875/30 · 2.25/36 (KPI numbers). Dense tables 13; spacious body 14.
- **Weights:** 400 body · 500 medium (labels, buttons, nav) · 600 semibold (headings, KPI numbers) ·
  700 reserved for large display numbers.

## 3. Spacing & layout

- **Spacing scale (px):** 2 · 4 · 8 · 12 · 16 · 20 · 24 · 32 · 40 · 48 (4px base).
- **App shell:** fixed **sidebar 248px**, collapsible to 72px (icon-only); top bar ~64px; content
  max-width ~1280–1440 with 24–32px gutters.
- **Spacious register:** card padding 20–24; section gap 24.
- **Dense register:** card/table padding 12–16; **table row 40 comfortable / 32 compact** (user toggle).

## 4. Radius, border & elevation  _(approx)_

- **Radius:** cards/large surfaces `16`; inputs/buttons/menus `10`; dense table containers `8`;
  pills/avatars full.
- **Surfaces:** light = hairline `--border` **+ soft shadow** (`0 1px 2px rgba(16,24,40,.04),
  0 1px 3px rgba(16,24,40,.06)`); dark = border only (no shadow).
- **Focus ring:** 2px `--accent` + 2px offset.

## 5. Iconography

- **Lucide** (MIT), ~1.5px stroke, 20px default. **No emoji.** Active nav item uses the filled/solid
  variant where available (Decko-style).

## 6. Motion

- Subtle & fast: 120–180ms ease for hovers/menus/toggles; theme switch crossfades; respect
  `prefers-reduced-motion`.

## 7. Core components (spec in COMPONENTS.md later)

App shell (sidebar + top bar + theme toggle + ⌘K command palette), `Card`, `StatTile`
(label/number/delta chip), `StatusBadge` (semantic), `Button` (primary teal / secondary / ghost /
danger), `DataTable` (dense, sortable, sticky header, tabular nums, density toggle), `DocumentForm`
(header + line-items grid), form controls, `Money` display (id-ID, negatives in parens), charts
(bar/line/donut, teal series, black tooltip), toasts, modals.

## 8. Token → Tailwind mapping (at scaffold time)

CSS custom properties are defined per theme on `:root` / `[data-theme="dark"]`; Tailwind theme
extends to reference them (e.g. `colors.accent.DEFAULT = 'var(--accent)'`), so a single source drives
both utility classes and component CSS. Theme toggle flips `data-theme` on `<html>`.

## 9. Resolved earlier open decisions
A teal accent + distinct semantics ✅ · B Plus Jakarta Sans ✅ · C Lucide ✅ · D placeholder
lettermark ✅ · E dark sidebar both themes ✅.
