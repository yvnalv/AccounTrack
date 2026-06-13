# CONTRIBUTING.md

How to contribute to Accountrack — for human developers and AI coding assistants.

## 0. Read First
- [`CLAUDE.md`](../CLAUDE.md) — the single source of truth. It overrides everything if there is
  a conflict (unless explicitly superseded by an accepted ADR).
- [ARCHITECTURE.md](ARCHITECTURE.md), [DECISIONS.md](DECISIONS.md), [GLOSSARY.md](GLOSSARY.md),
  and the design doc for the area you're touching.

## 1. Golden Rules
1. Never break a non-negotiable rule in `CLAUDE.md`. If one seems wrong, raise an ADR — don't
   silently violate it.
2. No module reads/writes another module's tables (ADR-0007). Communicate via contracts/events.
3. Tenant isolation, soft delete, audit, double-entry, and inventory-ledger invariants are
   sacred. Don't bypass query filters; don't physically delete; don't hand-write journals.
4. Documentation is part of the product — update the relevant `docs/` file in the same PR as the
   code change (`CLAUDE.md` Documentation Rules).

## 2. Branching & Commits
- Branch off `main`: `feature/<module>-<short>`, `fix/<module>-<short>`, `docs/<short>`,
  `chore/<short>`.
- Commit messages: present tense, module-scoped — e.g.
  `inventory: serialize moving-average updates on cost bucket`.
- Reference affected ADRs / business rules where relevant (`Implements BR-INV-2`).
- Small, focused PRs. One concern per PR.

## 3. Pull Requests
A PR must include:
- A clear description: what, why, which `CLAUDE.md`/ADR/BR items it relates to.
- Tests for new/changed behavior (TESTING.md); high-priority modules require them.
- Updated docs (DATABASE/API/MODULES/BUSINESS_RULES/ACCOUNTING/INVENTORY/DECISIONS as
  applicable).
- Green CI: build, lint, unit, architecture, contract, integration, frontend, vuln scan
  (DEPLOYMENT.md §5).
- Security consideration note: tenant scope, required permissions, SoD impact.

PRs touching Accounting, Inventory, Security, or Tenancy get extra review scrutiny and should
pass `/security-review` on the diff.

## 4. Definition of Done
Per CODING_STANDARDS.md §9: builds; analyzers/lint clean; tests (unit + relevant integration +
architecture) pass; docs updated; business rules referenced; security implications addressed.

## 5. Making Architectural / Rule Changes
- **Architectural decision** → add an ADR (copy [`adr/0000-template.md`](adr/0000-template.md)),
  index it in DECISIONS.md, and update affected docs. Supersede rather than rewrite accepted ADRs.
- **Business rule** → add/update the `BR-` entry in BUSINESS_RULES.md, reference its id in the
  enforcing code and tests.
- **New integration event/contract** → define in `Modules.Contracts`, document in
  INTEGRATION_EVENTS.md with its consistency mode, add idempotency + tests.

## 6. Local Setup (summary; see DEPLOYMENT.md)
1. Install .NET 8 SDK, Node LTS, Docker.
2. `docker compose up` to start SQL Server (+ api/nginx).
3. Backend: `dotnet build`, `dotnet test`, run `Accountrack.Api`.
4. Frontend: `npm install`, `npm run dev`.
5. Secrets via `dotnet user-secrets`; never commit secrets.

## 7. Language & i18n
- All code, comments, and docs in **English** (`CLAUDE.md`).
- All user-facing strings localized (English default + Bahasa Indonesia); no hardcoded UI text.

## 8. For AI Assistants
- Read `CLAUDE.md` fully before acting. Follow it exactly; it overrides default behavior.
- Don't scaffold or change architecture without an approved decision. When a requirement is
  ambiguous, ask or record an ADR rather than assuming.
- Keep changes consistent with surrounding code and the conventions in CODING_STANDARDS.md.
