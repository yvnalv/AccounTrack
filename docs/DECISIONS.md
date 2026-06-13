# DECISIONS.md — Architectural Decision Records

This file is the index and home for Accountrack's Architectural Decision Records (ADRs).
Every major architectural, accounting, inventory, security, or process decision is recorded
here. New ADRs use the template in [`adr/0000-template.md`](adr/0000-template.md).

An ADR is **immutable once Accepted**. To change a decision, write a new ADR that supersedes
the old one and update the old one's status to `Superseded by ADR-XXXX`.

## Index

| # | Title | Status | Date |
|---|-------|--------|------|
| 0001 | Modular Monolith over Microservices for MVP | Accepted | 2026-06-13 |
| 0002 | Clean Architecture per module | Accepted | 2026-06-13 |
| 0003 | SQL Server + EF Core | Accepted | 2026-06-13 |
| 0004 | Shared-database, shared-schema multi-tenancy | Accepted | 2026-06-13 |
| 0005 | GUID (sequential) primary keys | Accepted | 2026-06-13 |
| 0006 | Soft delete + standard audit fields | Accepted | 2026-06-13 |
| 0007 | In-process integration events + transactional outbox | Accepted | 2026-06-13 |
| 0008 | General Ledger is the source of truth | Accepted | 2026-06-13 |
| 0009 | Double-entry; posted journals immutable (reversal-only) | Accepted | 2026-06-13 |
| 0010 | Fiscal periods with open/close/lock | Accepted | 2026-06-13 |
| 0011 | AR/AP as subledgers with open-item allocation | Accepted | 2026-06-13 |
| 0012 | Indonesia-first PPN (VAT 11%) in MVP | Accepted | 2026-06-13 |
| 0013 | Single functional currency per company; FX fields reserved | Accepted | 2026-06-13 |
| 0014 | Inventory ledger is source of truth | Accepted | 2026-06-13 |
| 0015 | Moving-average costing per (Company × Warehouse × Product) | Accepted | 2026-06-13 |
| 0016 | Negative stock disallowed by default (configurable) | Accepted | 2026-06-13 |
| 0017 | Chronological posting; no back-dating into closed periods | Accepted | 2026-06-13 |
| 0018 | GR/IR clearing account for purchases | Accepted | 2026-06-13 |
| 0019 | RBAC with configurable permissions + segregation of duties | Accepted | 2026-06-13 |
| 0020 | JWT access + rotating refresh tokens | Accepted | 2026-06-13 |
| 0021 | Optimistic concurrency (RowVersion) + idempotent posting | Accepted | 2026-06-13 |
| 0022 | Period-balance snapshots for reporting | Accepted | 2026-06-13 |
| 0023 | Architecture-fitness tests (NetArchTest) enforce boundaries | Accepted | 2026-06-13 |
| 0024 | Configurable posting rules (account determination) | Accepted | 2026-06-13 |

---

## ADR-0001: Modular Monolith over Microservices for MVP

- **Status:** Accepted — **Date:** 2026-06-13

**Context.** Accountrack is a multi-module ERP targeting SMB/mid-market. A microservice
topology adds distributed-transaction, deployment, and observability cost that a small team
cannot absorb at MVP, and accounting requires strong transactional consistency.

**Decision.** Build a single deployable modular monolith. Modules (Identity, Company,
Sales, Purchasing, Inventory, Accounting, Reporting, …) are internally isolated and must be
extractable into services later.

**Consequences.** (+) Simple deployment, in-process transactions, easy refactoring.
(−) Requires discipline to keep boundaries clean — enforced by ADR-0007 and ADR-0023.

---

## ADR-0002: Clean Architecture per module

- **Status:** Accepted — **Date:** 2026-06-13

**Decision.** Each module is layered Domain → Application → Infrastructure → API. The Domain
layer has no outward dependencies; dependencies point inward. Business rules live in Domain,
use cases in Application, persistence/integration in Infrastructure, contracts in API.

**Consequences.** (+) Testable business logic, swappable infrastructure. (−) More
boilerplate; mitigated by shared kernel and conventions (see DATABASE.md, CODING_STANDARDS.md).

---

## ADR-0003: SQL Server + EF Core

- **Status:** Accepted — **Date:** 2026-06-13

**Decision.** Relational database (SQL Server) with EF Core as ORM. Strong relational
integrity and transactions are required for accounting and inventory.

**Consequences.** (+) Mature tooling, migrations, transactions, global query filters for
tenancy. (−) Vendor coupling; mitigated by keeping provider-specific SQL behind repositories.

---

## ADR-0004: Shared-database, shared-schema multi-tenancy

- **Status:** Accepted — **Date:** 2026-06-13

**Decision.** One database, one schema; every business row carries `TenantId` and `CompanyId`.
Isolation is enforced by EF global query filters bound to an ambient tenant context resolved
from the JWT, plus authorization guards and repository validation.

**Consequences.** (+) Lowest operational cost, simplest cross-company reporting within a
tenant. (−) Query-filter bypass is a critical risk → see MULTI_TENANCY.md / SECURITY.md for
mandatory controls (banned `IgnoreQueryFilters`, isolation tests, TenantId-leading indexes).

---

## ADR-0005: GUID (sequential) primary keys

- **Status:** Accepted — **Date:** 2026-06-13

**Decision.** Business entities use GUID primary keys, generated as sequential GUIDs
(`NEWSEQUENTIALID()` / ordered client-side) to limit index fragmentation.

**Consequences.** (+) Globally unique, merge/offline friendly, no cross-tenant id guessing.
(−) Wider keys than int; mitigated by sequential generation and TenantId-leading indexes.

---

## ADR-0006: Soft delete + standard audit fields

- **Status:** Accepted — **Date:** 2026-06-13

**Decision.** Business data is never physically deleted. Every table carries `CreatedAt/By`,
`UpdatedAt/By`, `DeletedAt/By`, `IsDeleted`. A global query filter hides soft-deleted rows.
Audit history (before/after values) is captured automatically via an EF SaveChanges interceptor.

**Consequences.** (+) Full auditability, recoverable data. (−) Must filter soft-deleted rows
everywhere (handled by the global filter) and consider unique constraints over `IsDeleted`.

---

## ADR-0007: In-process integration events + transactional outbox

- **Status:** Accepted — **Date:** 2026-06-13

**Context.** Modules must stay decoupled yet coordinate (e.g. an invoice must produce a
journal; a shipment must move stock and recognize COGS).

**Decision.**
- Cross-module communication uses **integration events** published via an in-process mediator,
  plus public application-service contracts. No module reads another module's tables.
- Effects that must be reliable but may be **eventually consistent** are delivered via a
  **transactional outbox** (event persisted in the same transaction as the source change, then
  dispatched).
- Effects that require **atomicity** with the source change are committed in the same DB
  transaction within a coordinating application service.

**Consequences.** (+) Loose coupling, reliable delivery, future bus swap (RabbitMQ) is local.
(−) Outbox + idempotency machinery (see ADR-0021). Detailed in INTEGRATION_EVENTS.md.

---

## ADR-0008: General Ledger is the source of truth

- **Status:** Accepted — **Date:** 2026-06-13

**Decision.** All financial reports derive from journal entries / GL, never directly from
transactional tables. Transactional modules raise events; the Accounting module posts journals.

**Consequences.** (+) One reconcilable truth, auditable reports. (−) Requires posting rules
(ADR-0024) and period snapshots for performance (ADR-0022).

---

## ADR-0009: Double-entry; posted journals immutable (reversal-only)

- **Status:** Accepted — **Date:** 2026-06-13

**Decision.** Every financial event posts a balanced journal (Σdebits = Σcredits) enforced as
a domain invariant. A posted journal can never be edited or deleted; corrections are made by
posting a **reversing journal** and, if needed, a corrected one.

**Consequences.** (+) Tamper-evident books, clean audit trail. (−) Users must understand
reversal workflow; UI must make it easy.

---

## ADR-0010: Fiscal periods with open/close/lock

- **Status:** Accepted — **Date:** 2026-06-13

**Decision.** Each company has a fiscal calendar of periods (monthly within a fiscal year).
Periods have states Open → Closed → Locked. No journal may post into a Closed/Locked period.
Period close produces account-balance snapshots (ADR-0022).

**Consequences.** (+) Correct period reporting, prevents back-dated tampering. (−) Requires
period-management UI and reopen controls (permissioned).

---

## ADR-0011: AR/AP as subledgers with open-item allocation

- **Status:** Accepted — **Date:** 2026-06-13

**Decision.** Accounts Receivable and Accounts Payable are maintained as **subledgers**: each
invoice is an open item; payments allocate against open items (full or partial); the subledger
reconciles to the GL control account. Aging reports derive from open items.

**Consequences.** (+) Real receivables/payables management, partial payments, aging. (−) More
modeling than a single control account; required for a real ERP.

---

## ADR-0012: Indonesia-first PPN (VAT 11%) in MVP

- **Status:** Accepted — **Date:** 2026-06-13

**Decision.** MVP implements single-rate Indonesian **PPN at 11%** on sales and purchase
invoices. Tax is stored per line (tax code, rate, base, amount). Sales tax posts to **VAT
Output (liability)**; purchase tax posts to **VAT Input (asset)**. Schema supports multiple
tax codes/rates, inclusive/exclusive pricing, and withholding tax in future phases.

**Consequences.** (+) Compliant invoices for the launch market. (−) Tax-code abstraction now
even though only one rate is active; protects against later migration.

---

## ADR-0013: Single functional currency per company; FX fields reserved

- **Status:** Accepted — **Date:** 2026-06-13

**Decision.** Each company has one functional/base currency (e.g. IDR). All monetary values
are stored as a `Money` value object (amount + currency code). Transaction tables reserve
`TransactionCurrency`, `ExchangeRate`, and functional-amount columns so multi-currency and
revaluation can be enabled later. No FX gain/loss or revaluation logic in MVP.

**Consequences.** (+) No painful migration when multi-currency arrives. (−) Slightly more
columns now; negligible cost.

---

## ADR-0014: Inventory ledger is source of truth

- **Status:** Accepted — **Date:** 2026-06-13

**Decision.** `InventoryTransaction` is the append-only source of truth for quantity and
value on hand. `Product.CurrentStock` (if present) is a cached projection only, never
authoritative.

**Consequences.** (+) Full traceability, supports future FIFO/lot costing. (−) On-hand and
valuation are derived; cached projections must be rebuildable from the ledger.

---

## ADR-0015: Moving-average costing per (Company × Warehouse × Product)

- **Status:** Accepted — **Date:** 2026-06-13

**Decision.** Initial costing is **moving weighted average**, maintained per
**(Company, Warehouse, Product)**. Each receipt updates the running average for that cost
bucket; each issue consumes at the current average. The ledger schema reserves optional
cost-layer references so FIFO/lot costing can be added without restructuring (see ADR rationale
in INVENTORY_DESIGN.md).

**Consequences.** (+) Simple, no lot tracking, warehouse-accurate valuation. (−) Transfers
must carry cost between warehouses; concurrency on the average must be serialized (ADR-0021).

---

## ADR-0016: Negative stock disallowed by default (configurable)

- **Status:** Accepted — **Date:** 2026-06-13

**Decision.** By default, an issue that would drive on-hand below zero is rejected. A
per-company setting may allow negative stock; when enabled, oversold issues cost at the last
known average and are reconciled on the next receipt. Default = disallow.

**Consequences.** (+) Protects moving-average integrity. (−) Stricter operational discipline;
the configurable escape hatch exists for businesses that need it.

---

## ADR-0017: Chronological posting; no back-dating into closed periods

- **Status:** Accepted — **Date:** 2026-06-13

**Decision.** Inventory and journal transactions post in chronological order within an open
period. Back-dating into a **closed/locked** period is forbidden. Back-dating within the
current open period is allowed but triggers a forward recompute of the moving average for the
affected cost bucket.

**Consequences.** (+) Avoids the worst moving-average ordering traps. (−) Recompute logic for
in-period back-dating; bounded because closed periods are immutable.

---

## ADR-0018: GR/IR clearing account for purchases

- **Status:** Accepted — **Date:** 2026-06-13

**Decision.** Goods Receipt posts Dr Inventory / Cr **GR/IR clearing**. Purchase Invoice posts
Dr GR/IR clearing / Cr Accounts Payable. This decouples the physical receipt from the
financial invoice and surfaces received-not-invoiced balances.

**Consequences.** (+) Correct accrual accounting, three-way-match foundation. (−) One extra
control account to reconcile.

---

## ADR-0019: RBAC with configurable permissions + segregation of duties

- **Status:** Accepted — **Date:** 2026-06-13

**Decision.** Authorization is RBAC. Permissions are data (e.g. `Sales.Create`,
`Accounting.Post`), assignable to roles, never hardcoded. Sensitive verbs (Approve, Post, Pay)
are separate permissions to enable **segregation of duties**.

**Consequences.** (+) Configurable, auditable access; SoD enforceable. (−) Permission catalog
must be maintained and seeded.

---

## ADR-0020: JWT access + rotating refresh tokens

- **Status:** Accepted — **Date:** 2026-06-13

**Decision.** Stateless JWT access tokens (short-lived) plus persisted, **rotating** refresh
tokens with revocation and reuse-detection. Tenant and granted companies are claims, validated
server-side. OAuth (Google/Microsoft) is a later phase.

**Consequences.** (+) Scalable auth, revocable sessions. (−) Refresh-token store and rotation
logic required (see SECURITY.md).

---

## ADR-0021: Optimistic concurrency (RowVersion) + idempotent posting

- **Status:** Accepted — **Date:** 2026-06-13

**Decision.** All mutable documents carry a `RowVersion` for optimistic concurrency. Posting
handlers are **idempotent**, keyed by source document id + event type, so retried events never
double-post journals or inventory movements. Moving-average updates serialize on the cost-bucket
row.

**Consequences.** (+) Safe under retries and concurrent edits. (−) Handlers must check/record
idempotency keys; conflicts surface to the user.

---

## ADR-0022: Period-balance snapshots for reporting

- **Status:** Accepted — **Date:** 2026-06-13

**Decision.** Period close writes per-(account, company, period) balance snapshots. Reports
read opening snapshot + in-period movements rather than summing all history. Redis caching is
introduced only when measured latency requires it.

**Consequences.** (+) Fast, scalable reports; fast trial balance. (−) Snapshots must stay
consistent with the GL (rebuildable from journals).

---

## ADR-0023: Architecture-fitness tests (NetArchTest) enforce boundaries

- **Status:** Accepted — **Date:** 2026-06-13

**Decision.** CI runs architecture tests asserting: Domain has no Infrastructure/EF
dependency; modules don't reference each other's internals; dependencies point inward. A
violation fails the build.

**Consequences.** (+) Boundaries stay clean automatically. (−) Some upfront test setup.

---

## ADR-0024: Configurable posting rules (account determination)

- **Status:** Accepted — **Date:** 2026-06-13

**Decision.** The mapping from business events to GL accounts (revenue, COGS, AR/AP control,
VAT, GR/IR, inventory, bank/cash, rounding, variance) is **configuration per company**, not
hardcoded. Defaults are seeded; advanced setups can key rules by product category, warehouse,
or document type. Detailed in POSTING_RULES.md.

**Consequences.** (+) Adaptable to different charts of accounts; testable. (−) Configuration
surface and validation required (every active rule must resolve to a valid account).
