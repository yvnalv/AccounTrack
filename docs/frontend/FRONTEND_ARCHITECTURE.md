# Frontend Architecture

The Accountrack web client. Scaffolded in CHG-0027.

## Stack

- **Vue 3** (`<script setup>`, Composition API) + **TypeScript** (strict)
- **Vite 6** (dev server + build)
- **Pinia** (state), **Vue Router 4** (routing)
- **Tailwind CSS 3** (utilities) driven by **CSS custom properties** for theming
- **vue-i18n** (EN now; Bahasa Indonesia later)
- **Apache ECharts** via `vue-echarts` (charts)
- **axios** (API client), **Lucide** (icons)

Lives in **`frontend/`** as its own project (sibling to `src/`).

## Run

```bash
# 1) backend (from repo root)
cd src/Bootstrapper/Accountrack.Api
ASPNETCORE_ENVIRONMENT=Development Database__Initialize=true Database__AutoMigrate=true Seed__Enabled=true dotnet run
# 2) frontend
cd frontend
npm install
npm run dev      # http://localhost:5173  (proxies /api → http://localhost:5080)
npm run build    # vue-tsc typecheck + vite production build
```

Dev login: `admin@accountrack.local` / `ChangeMe!123`.

## Structure

```
frontend/src/
├── assets/styles/   tokens.css (design tokens, light/dark) + main.css (Tailwind + base)
├── components/
│   ├── layout/      AppSidebar, AppTopbar
│   └── ui/          AppButton, AppCard, StatTile, ThemeToggle
├── i18n/            index.ts + locales/en.ts
├── layouts/         AppShell (sidebar + topbar + <RouterView>)
├── lib/             api.ts (axios + envelope unwrap + 401 handling), format.ts (id-ID money/number)
├── router/          routes + auth guard
├── stores/          auth (Pinia), theme
├── types/           api.ts (response envelope + DTOs)
└── views/           LoginView, DashboardView, PlaceholderView
```

## Theming

`tokens.css` defines all colors as CSS variables on `:root` and `[data-theme="dark"]`. Tailwind's
theme maps utilities to those variables (`tailwind.config.ts`), so the **theme toggle** just flips
`data-theme` on `<html>` (persisted in `localStorage`, defaults to `prefers-color-scheme`). The dark
sidebar is dark in both themes (per DESIGN_LANGUAGE). Charts read the CSS vars at runtime so ECharts
re-themes too. See [DESIGN_LANGUAGE.md](DESIGN_LANGUAGE.md).

## API & auth

- `lib/api.ts` — axios instance at `/api/v1`; request interceptor attaches the bearer token; `unwrap`
  peels the `{ success, data }` envelope; a 401 clears the session and routes to `/login`.
- `stores/auth.ts` — login (`POST /auth/login`), session (token + user in `localStorage`),
  `has(permission)` for permission checks. **Refresh-token rotation is a TODO** (currently 401 →
  re-login).
- Router guard redirects unauthenticated users to `/login` (with `?redirect=`).

## Conventions

- Money/qty use `formatMoney`/`formatNumber` (id-ID, negatives in parentheses) + the `.tnum` class
  (tabular figures).
- Reusable presentation lives in `components/ui`; one accent (teal) used sparingly; semantic colors
  for status. No emoji; Lucide icons only.

## Status & next

✅ Scaffold + design tokens + app shell (sidebar, top bar, theme toggle, ⌘K placeholder) + login +
**dashboard** (KPIs + revenue/expense chart) wired to `GET /api/v1/dashboard/summary`. Other nav
targets are `PlaceholderView` ("coming soon").

Next slices (each its own CHG): wire ⌘K command palette; **Sales** list + Sales-Order
create/detail (first real CRUD against the dense-table register); shared `DataTable` + `DocumentForm`
components; then Purchasing/Inventory/Accounting screens; i18n `id` locale; self-host the font;
refresh-token rotation.
