# TESTING.md

Testing strategy for Accountrack. Required: unit + integration tests. High-priority coverage:
**Accounting, Inventory, Approval Workflow, Security** (`CLAUDE.md`).

## 1. Test Levels

| Level | Scope | Tooling | Speed |
|---|---|---|---|
| **Unit** | Domain logic, value objects, posting-rule resolution, moving-average math, validators, handlers (mocked ports) | xUnit + FluentAssertions + NSubstitute/Moq | fast, run always |
| **Integration** | Use case → DB (real PostgreSQL via Testcontainers), EF mappings, query filters, outbox, atomic flows | xUnit + Testcontainers + Respawn | medium |
| **Architecture** | Boundary/dependency rules | NetArchTest | fast |
| **Contract** | `Modules.Contracts` event/service shapes & idempotency | xUnit | fast |
| **API** | Endpoint → pipeline → handler (WebApplicationFactory) | xUnit + `WebApplicationFactory` | medium |
| **Frontend** | Components/stores/composables | Vitest + Vue Test Utils | fast |
| **E2E (later)** | Critical user journeys | Playwright | slow |

## 2. High-Priority Suites (must exist and never regress)

### Accounting (ACCOUNTING_DESIGN.md, POSTING_RULES.md)
- Balanced-journal invariant (BR-ACC-1/2) including rounding edge cases (BR-ACC-12).
- Each posting rule produces the expected lines for each event (sales/purchase/payment/return/
  adjustment) — table-driven.
- Reversal restores account balances exactly (BR-ACC-3).
- No posting into closed/locked period (BR-ACC-4).
- Idempotent re-posting never duplicates (BR-ACC-6).
- AR/AP subledger reconciles to control account; partial allocation; aging buckets (BR-ACC-8/9).
- Period close snapshot equals recomputed GL; year-end roll to retained earnings (BR-ACC-11).
- VAT Output/Input totals and VAT report (BR-TAX-*).

### Inventory (INVENTORY_DESIGN.md)
- Moving-average math across receipt/issue/return/transfer sequences (BR-INV-2/6).
- **Concurrency**: parallel receipts and issues on one bucket keep a correct average (ADR-0021).
- Negative-stock rejection by default; opt-in path reconciles (BR-INV-3).
- Back-dating recompute within open period; rejection in closed period (BR-INV-4/5).
- Inventory valuation reconciles to the Inventory GL account (BR-INV-7).
- Idempotent movement posting.

### Approval Workflow (WORKFLOW_APPROVAL.md)
- Single / multi-level / conditional routing; quorum; reject returns the document.
- No-definition auto-approve (BR-APR-3); submitter self-approval blocked (BR-APR-2/SoD).
- Document state and approval state stay consistent; process timeline reflects transitions.

### Security & Tenancy (SECURITY.md, MULTI_TENANCY.md §9)
- Cross-tenant query returns zero foreign rows. ✅ `Accountrack.IntegrationTests` (CHG-0042)
- Insert stamps tenant/company from ambient context; cross-tenant modify & no-context insert are
  rejected. ✅ (CHG-0042)
- Every tenant-scoped entity has a global query filter (reflection over all 11 contexts). ✅ (CHG-0042)
- Un-granted company → 403; forged TenantId/CompanyId overridden/rejected. ◻️ (API-level, pending)
- No `IgnoreQueryFilters` outside the allow-list (architecture test). ◻️ pending — current allow-list
  is `**/Seed/**`, Identity login/refresh repositories, and `CompanyDirectory`.
- Background job without tenant context cannot read tenant data. ◻️ pending.
- AuthZ: each protected endpoint denies without the required permission. ◻️ pending.
- Auth: lockout, refresh rotation, reuse-detection revocation.

> **Isolation-suite infra:** TESTING.md prescribes Testcontainers; where Docker is unavailable the
> suite targets a local/CI PostgreSQL (env `ACCOUNTRACK_TEST_PG`, default localhost) and skips the
> behavioral tests if none is reachable. The offline model-convention tests always run.

## 3. Conventions

- **AAA** (Arrange-Act-Assert); one logical assertion theme per test.
- **Test naming:** `Method_State_ExpectedResult` (e.g. `Post_ClosedPeriod_ThrowsBusinessRule`).
- **Deterministic:** inject `IClock`; no real `DateTime.Now`, no random without seed, no sleeps.
- **Isolation:** each integration test runs against a clean DB state (Respawn between tests);
  Testcontainers spins PostgreSQL.
- **Data builders / object mothers** for entities to keep tests readable.
- Reference business rules by id in test names/comments where relevant (`BR-INV-3`).

## 4. Coverage & Gates

- No hard global coverage % mandate, but the high-priority modules (Accounting, Inventory,
  Approval, Security) must have comprehensive behavioral coverage; PRs touching them require
  tests.
- CI gate (DEPLOYMENT.md): build → unit → architecture → contract → integration → frontend.
  A red suite blocks merge.

## 5. Performance / Reconciliation Checks (periodic)
- Trial Balance balances on representative data volumes.
- Subledger↔GL and inventory↔GL reconciliation jobs report zero drift on the seed/demo dataset.
- Report queries on snapshots stay within target latency as volume grows.

## 6. Test Project Layout
```
tests/
├── <Module>.UnitTests
├── <Module>.IntegrationTests
├── Accountrack.ArchitectureTests
└── Accountrack.Contracts.Tests
frontend/  (Vitest co-located or under tests/)
```
