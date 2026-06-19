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
| 0025 | Changelog policy (reverse-chronological, immutable, CHG-NNNN, UTC) | Accepted | 2026-06-13 |
| 0026 | Atomic audit capture into one shared audit table | Accepted | 2026-06-13 |
| 0027 | Build with the .NET 9 SDK while targeting net8.0 | Accepted | 2026-06-13 |
| 0028 | GUID primary keys are `ValueGeneratedNever` (domain-generated) | Accepted | 2026-06-14 |

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

**Implementation (CHG-0040).** Command-level idempotency is implemented as a MediatR pipeline
behavior (`IdempotencyBehavior`, between Logging and Validation). Commands opt in with the
`IIdempotentCommand` marker and must return `Result<Guid>`. The key comes from the
`Idempotency-Key` request header (`IIdempotencyContext`); it is scoped per
`{tenant}:{commandType}:{key}` and persisted in `platform.IdempotencyKeys` via `IIdempotencyStore`
(its own short-lived connection, never the shared cross-module transaction). A replayed key
short-circuits the handler and returns the original id. The frontend axios client sends a fresh
`Idempotency-Key` on every POST/PUT/PATCH so a transport-level retry replays rather than
double-posts. Marked commands: the create commands (Sales/Purchase Order) and all six posting
commands (Goods Receipt, Purchase Invoice, Supplier Payment, Delivery Order, Sales Invoice,
Customer Payment) plus manual Journal posting.

**Known limitation.** The result id is recorded *after* the command commits, so a crash in the
narrow window between commit and `SaveAsync` would let a retry re-execute (double-post). True
exactly-once requires writing the key inside the same transaction; that is a future hardening step
once a durable outbox lands. RowVersion optimistic concurrency on documents is still pending.

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

---

## ADR-0025: Changelog policy (reverse-chronological, immutable, CHG-NNNN, UTC)

- **Status:** Accepted — **Date:** 2026-06-13

**Context.** A human- and AI-readable history of notable changes aids reviews, releases, and
context for assistants. It needs consistent ordering and ids so it can be maintained reliably.

**Decision.** Maintain `CHANGELOG.md` at the repository root: reverse-chronological (newest on
top), sequential zero-padded `CHG-NNNN` ids, UTC timestamps `YYYY-MM-DD HH:mm:ss UTC`. Entries are
immutable — never edited, reordered, renumbered, or deleted; rollbacks are recorded as new
entries. Updating the changelog is part of the definition of done. Full rules live in `CLAUDE.md`
(Documentation Rules → CHANGELOG Rules).

**Consequences.** (+) Predictable, append-at-top history that both people and agents can update
correctly. (−) Requires discipline to add an entry per notable change.

---

## ADR-0026: Atomic audit capture into one shared audit table

- **Status:** Accepted — **Date:** 2026-06-13

**Context.** Audit history must be complete and tamper-evident (ADR-0006, SECURITY.md §4). In a
modular monolith each module owns its own DbContext/schema, but audit entries should live in one
queryable place and be written reliably with the change that produced them.

**Decision.** Audit entries are captured by a shared `AuditCaptureInterceptor` that records the
before/after of every inserted/updated/deleted business entity and adds an `AuditEntry` row to the
**same context/transaction** being saved — so the audit write is atomic with the business change.
The interceptor runs *before* the soft-delete-conversion interceptor so deletes are recorded as
deletes. `AuditEntry` is a shared SharedKernel primitive mapped to a single `audit.AuditEntries`
table; the AuditLog module's context owns the table's migration, and every other module context
maps the same table with `ExcludeFromMigrations()`. The AuditLog module exposes a tenant-scoped,
permissioned read API (`Audit.View`).

**Options considered.** (a) Per-module audit tables — rejected: querying/union pain. (b) Eventual
capture via the outbox — deferred: weaker guarantee and the dispatcher isn't built yet. (c) Shared
atomic table (chosen).

**Consequences.** (+) Complete, atomic, single-table audit that is simple to query; verified
end-to-end (login change captured). (−) Every module context maps the shared table (one line via
`BaseDbContext`); audited "after" values intentionally exclude the stamped audit fields.

---

## ADR-0027: Build with the .NET 9 SDK while targeting net8.0

- **Status:** Accepted — **Date:** 2026-06-13

**Context.** CLAUDE.md mandates .NET 8 (non-negotiable #1), but the development/build machine has
only the .NET 9 SDK installed. The .NET 9 SDK builds and runs `net8.0` projects.

**Decision.** All projects target **`net8.0`** (set once in `Directory.Build.props`); the .NET 9
SDK is used to build and run. No `global.json` pins the SDK. The API host sets
`<RollForward>LatestMajor</RollForward>` so it runs on the installed .NET 9 runtime when the .NET 8
runtime is absent. EF Core and ASP.NET packages are pinned to the 8.0.x line.

**Consequences.** (+) Honors the .NET 8 target/runtime contract while remaining buildable on the
available toolchain; CI can install either SDK ≥ 8. (−) Roll-forward means local runs may execute
on the 9 runtime; production should install the .NET 8 runtime (or accept documented roll-forward).

---

## ADR-0028: GUID primary keys are `ValueGeneratedNever` (domain-generated)

- **Status:** Accepted — **Date:** 2026-06-14

**Context.** Entities assign their GUID `Id` in the domain (ADR-0005). The SQL Server provider, by
default, marks GUID keys as `ValueGenerated.OnAdd`. With a *pre-set* key, EF then mis-detects a
newly-constructed child added to an already-*loaded* aggregate as an existing row → it emits an
UPDATE that affects 0 rows and throws a concurrency exception (hit first in Approval, where
approving adds an `ApprovalAction` to a loaded request).

**Decision.** `BaseDbContext` conventions configure every entity's `Id` as
`ValueGeneratedNever()`. The application always supplies the key; EF then correctly marks new
untracked entities as Added (insert).

**Consequences.** (+) Adding children to loaded aggregates works (inserts, not phantom updates);
matches the client-generated-GUID design. The column DDL is unchanged (`uniqueidentifier` PK), so
no migration change was required. (−) The app must always set `Id` (already done by the `Entity`
base).

---

## ADR-0029: Edit/Delete policy — full CRUD for master data, reversal-only for posted documents

- **Status:** Accepted — **Date:** 2026-06-19

**Context.** Early screens shipped as list + create only. The product needs editing and removal
across the app, but business correctness and auditability (#12 soft delete, #28 posted journals
immutable, BR-X-2/BR-ACC-3) forbid a blanket "edit + hard delete everything."

**Decision.** CRUD capability is **tiered by record kind:**
- **Master data** (products, categories, UoM, customers, suppliers, warehouses, tax codes, chart of
  accounts, posting rules): **Edit** is allowed; **Delete is soft-delete / deactivate only** — never
  physical. A record that is **referenced by any non-cancelled transaction or is a system/seeded
  record** cannot be deactivated while in use (it can only be deactivated to stop *future* use).
- **Transactional documents** (SO/PO, deliveries/receipts, invoices, payments, journals, expenses):
  status-gated. A **Draft** may be edited and cancelled; once **Posted/Approved** it is **immutable**
  — corrections are made by **reversal, cancellation, or a return/credit note**, never in-place edit
  or delete (BR-ACC-3).
- Every edit, deactivate, and cancel is audited (BR-X-3) and permission-gated (separate
  `*.Edit` / `*.Delete` / `*.Cancel` permissions).

**Consequences.** (+) Uniform, predictable UX (every list gets Edit + Deactivate where legal) without
violating accounting integrity or the audit trail. (−) "Delete" never removes rows; the UI must
communicate deactivate-vs-delete and surface why a record can't be deactivated. New rules:
BR-X-7/BR-X-8.

---

## ADR-0030: Expenses module (operating-expense vouchers)

- **Status:** Accepted — **Date:** 2026-06-19

**Context.** The MVP records expenses only indirectly (purchase invoices for goods, or manual
journals). Businesses need to capture day-to-day operating costs — electricity, transport, rent,
supplies, wages-as-cash — without hand-writing journals.

**Decision.** Add an **Expenses** module (Phase 2). An **Expense Voucher** has a date, payee
(optional supplier or free-text), one or more lines each with an **expense category**, amount, and
optional tax (PPN Input where creditable), and a payment method (cash/bank now, or on-account → AP).
**Expense categories** map to **expense GL accounts via the posting-rule engine** (ADR-0024), so no
account is hardcoded. Posting is automatic and atomic (ADR-0019): **Dr Expense (+ Dr VAT Input) /
Cr Cash-Bank** (or **Cr AP** when unpaid), reusing the AP subledger for payables. Approvals and the
process tracker apply like other documents.

Scope boundary: this is **expense recording**, not **Payroll** — full payroll (employees,
components, statutory deductions, PPh 21) remains **Phase 3** (CLAUDE.md roadmap). Salaries paid as a
simple cash expense are supported here via a "Salaries & Wages" category.

**Consequences.** (+) Complete operating-expense capture with correct, configurable posting; P&L and
Cash Flow become meaningful. (−) New module surface (domain/app/infra/api + UI), category master,
and posting-rule keys for expense accounts. New rules: BR-EXP-*.

---

## ADR-0031: Data import (CSV/Excel) and export (CSV/Excel/PDF)

- **Status:** Accepted — **Date:** 2026-06-19

**Context.** Users need bulk onboarding (master data, opening balances) and to get data out for
sharing/filing, rather than entering or reading records one-by-one.

**Decision.** A **cross-cutting import/export capability** in a shared building block, opted into per
entity:
- **Import** — **CSV and Excel (.xlsx)**. Each importable entity publishes a **downloadable
  template** with documented columns. Import is a **two-step, validated** flow: upload → **dry-run
  preview** returning per-row validation results (and a "would create/update/skip" summary) →
  **commit**. Imports are **row-validated against the same domain rules** as single create, run inside
  a transaction, are **audited**, permission-gated (`*.Import`), and respect tenant/company context.
  Updates match on the entity's natural key (e.g. Code). Large files run **asynchronously** (Hangfire,
  Phase 4 infra) — until then a synchronous row cap applies.
- **Export** — **CSV and Excel** for any list/grid (respecting current filters), and **PDF** for
  documents and financial reports (invoices, TB/P&L/BS/VAT, statements). Exports are permission-gated
  (`*.Export`) and tenant-scoped.
- **Library choice:** a maintained, commercial-use-friendly spreadsheet library for .xlsx
  (e.g. ClosedXML / EPPlus-Polyform-noncommercial-aware — to be finalized at implementation, recorded
  here) and an HTML-to-PDF or report renderer for PDF. CSV uses a minimal hand-rolled/`CsvHelper`
  writer.

**Consequences.** (+) Bulk operations and portability across the whole app from one mechanism; safe
(dry-run + validation + audit). (−) Per-entity column maps/templates to maintain; a library
dependency and its licensing to vet; async pipeline needed for very large files. New rules:
BR-IMP-*.
