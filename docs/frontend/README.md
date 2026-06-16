# Frontend & UI/UX

Home for everything about the Accountrack web client (Vue 3 + TypeScript + Pinia + Vue Router +
Tailwind, per CLAUDE.md) — design language, references, and component/screen specs.

> **Guiding constraint (from the product owner):** the UI must look intentional and crafted, **not
> template-y or "AI-ish"** (no generic admin-kit look — avoid purple gradients, oversized cards,
> emoji icons, clip-art illustrations). Every visual decision is documented here so the build stays
> consistent.

## Status

🟢 **Scaffolded (CHG-0027).** Design language locked; the Vue app lives in `frontend/` with the app
shell, theme toggle, login, and the finance dashboard working against the API. See
[FRONTEND_ARCHITECTURE.md](FRONTEND_ARCHITECTURE.md).

## Documents

| Doc | Purpose |
|---|---|
| [REFERENCES.md](REFERENCES.md) | Inspiration sources (links + saved screenshots) and what we take from each |
| [DESIGN_LANGUAGE.md](DESIGN_LANGUAGE.md) | 🟢 **Locked v1** — design tokens: color, typography, spacing, radius, shadow, motion, iconography. Single source of truth for styling |
| [BRAND.md](BRAND.md) | Placeholder logo/wordmark + usage ([brand/](brand/) holds the SVGs) |
| [FRONTEND_ARCHITECTURE.md](FRONTEND_ARCHITECTURE.md) | App structure, routing, state, API client, auth, theming, i18n, how to run |
| _(next)_ SCREENS.md | Per-screen layouts/wireframes (list, detail, document forms) |
| _(later)_ COMPONENTS.md | Reusable component inventory + states |

## How to add a visual reference

1. Save the image into [references/](references/) (e.g. `references/decko-crm-dashboard.png`).
2. Add an entry in [REFERENCES.md](REFERENCES.md) with the source link and notes on what to borrow.
