# CODING_STANDARDS.md

Coding standards for Accountrack. Extends `CLAUDE.md` (Coding Standards, Code Quality Rules).

## 1. General

- Favor **SOLID**, **DI**, **Clean Architecture**, and DDD principles (`CLAUDE.md`).
- Avoid god classes, massive services, circular dependencies, shared mutable state, and static
  business logic.
- Keep methods small and intention-revealing; prefer composition over inheritance.
- No business logic in controllers/endpoints or in EF entities' persistence concerns.
- New code matches the style of surrounding code.

## 2. Naming (per `CLAUDE.md`)

| Artifact | Convention | Example |
|---|---|---|
| C# types/classes | PascalCase | `SalesOrder`, `InventoryTransaction` |
| C# methods/properties | PascalCase | `PostAsync`, `GrandTotal` |
| C# locals/params | camelCase | `unitCost` |
| Interfaces | `I` prefix | `IInventoryLedger` |
| Async methods | `Async` suffix | `ReceiveAsync` |
| SQL tables | PascalCase plural | `SalesOrders` |
| API routes | kebab-case plural | `sales-orders` |
| JSON fields | camelCase | `grandTotal` |
| Vue components | PascalCase | `SalesOrderForm.vue` |
| TS vars/functions | camelCase | `fetchOrders` |

## 3. Backend (.NET 8 / C#)

- **Layering (ADR-0002):** Domain has no outward deps; Application defines ports; Infrastructure
  implements them; API is thin. Enforced by NetArchTest (ADR-0023).
- **Nullable reference types** enabled; treat warnings as errors in CI.
- **Async all the way**; pass `CancellationToken`; never block on async (`.Result`/`.Wait()`).
- **No `DateTime.Now`** in domain/application — inject `IClock` (UTC).
- **Money**: use the `Money` value object; never `double`/`float` for amounts; `decimal` only.
- **Result type** for expected failures; exceptions for unexpected faults (ERROR_HANDLING.md).
- **Validation**: FluentValidation per command; domain invariants in the domain.
- **EF Core**: keep queries in Infrastructure behind ports; no business logic in `DbContext`;
  never use `IgnoreQueryFilters` outside the reviewed allow-list (MULTI_TENANCY.md).
- **Idempotency** on posting handlers (ADR-0021); **RowVersion** on mutable documents.
- One `DbContext` per module; entities live in their module's schema.
- Prefer records/immutability for value objects and DTOs.
- XML doc-comments on public contracts in `Modules.Contracts`.

## 4. Frontend (Vue 3 / TypeScript)

- `<script setup>` + Composition API; TypeScript everywhere (`strict` on).
- State in **Pinia** stores per domain; routing via Vue Router; styling via **Tailwind**.
- API access through a typed client layer (generated from OpenAPI where possible); no `fetch`
  scattered in components.
- **i18n (bilingual, mandatory)**: **every** user-facing word exists in **both** `en.ts` and `id.ts`
  — menus, titles, tabs, labels, buttons, table headers, placeholders, tooltips, empty states,
  toasts, validation/error messages, and status/enum labels. No hardcoded UI strings; all text via
  `t('…')`. The two locale files must stay structurally identical (same keys, none missing on either
  side — CI/`i18n-diff` guard). Exceptions only for proper nouns, codes, formats, and universal
  loan-words/terms (PPN, Transfer, PDF, Email, Status, Total, Debit).
  - **Backend enums** shown in the UI are translated via an i18n map keyed by the enum value (e.g.
    `accounting.sources.*`, `inventory.sources.*`), never rendered raw.
  - **Seeded reference data** shown in the UI (chart of accounts, expense categories) is localized by
    a code→{en,id} map that overrides a name only while it is still the seeded default, so a user
    rename is shown verbatim (`frontend/src/lib/coa.ts`, `expenseCategories.ts`). New standard/system
    seed data must be added to the matching map.
  - Localized text must react to a live locale switch (prefer `computed`/inline `t()` over values
    captured once at load).
- Money/dates formatted via shared utilities respecting locale; never format in templates ad hoc.
- Components small and presentational where possible; business/data logic in composables/stores.

## 5. Formatting & Linting

- C#: `.editorconfig` + `dotnet format`; analyzers on; warnings-as-errors in CI.
- TS/Vue: ESLint + Prettier; Vue official lint config.
- Max line length and import ordering enforced by the formatters (don't hand-fight them).

## 6. Comments & Documentation

- Comment the **why**, not the **what**. Reference business rules by id (`// BR-ACC-4`).
- Public module contracts and non-obvious domain logic get doc comments.
- Keep docs in `docs/` synchronized with code (`CLAUDE.md` Documentation Rules): a change to
  schema/architecture/API/rules updates the matching doc in the same PR.

## 7. Errors & Logging
- Use the standard envelope + codes (ERROR_HANDLING.md). Never leak internals to clients.
- Structured logging with `traceId/tenantId/userId/companyId`; never log secrets or full PII.

## 8. Git Hygiene
- Conventional, present-tense commit messages scoped by module (e.g.
  `accounting: enforce balanced-journal invariant`).
- Small, focused PRs; reference the ADR/BR ids affected. See CONTRIBUTING.md.

## 9. Definition of Done (per change)
- Builds; analyzers/lint clean; unit + relevant integration tests pass; architecture tests pass;
  docs updated; business rules referenced; security implications considered (tenant scope,
  permissions, SoD).
