# STATUS.md — Project Milestones & "You Are Here"

The single place to see **where the build is and what to do next**, so any session can resume in
context. Complements: [ROADMAP.md](ROADMAP.md) (the plan), [`../CHANGELOG.md`](../CHANGELOG.md)
(the history), [MODULES.md](MODULES.md) (per-module status table).

> Update this file whenever a module/slice lands (alongside the CHANGELOG entry).

## Snapshot

- **As of:** 2026-06-20 (last change **CHG-0048**)
- **Build:** green — backend `net8.0` (238 tests); **frontend** `frontend/` builds (vue-tsc + vite).
  Latest: **Expenses module** — operating-expense vouchers, category→GL via posting rules, atomic
  Dr Expense (+VAT Input) / Cr Cash-Bank (CHG-0048).
- **MVP transactional backend complete** (procure-to-pay + order-to-cash). **Frontend** is now
  demo-complete: app shell + light/dark + login + dashboard; **Sales** (submit→deliver→invoice→
  receive payment); **Purchasing** (submit→receive→bill→pay supplier); **Accounting reports**
  (TB/P&L/Balance Sheet); **Inventory** (on-hand + stock card); **Master data** (products/customers/
  suppliers/warehouses lists + create), **Approvals** (pending list + approve/reject), **English +
  Bahasa Indonesia**, and a **⌘K command palette**, plus **Settings** — company/profile/preferences
  (CHG-0041). Every nav item now has a real UI; no placeholders remain. Reusable
  DataTable/StatusBadge/form/modal kit. **Idempotency** for posting/create commands landed
  (CHG-0040); the **cross-tenant isolation suite** landed (CHG-0042). Next: backend debts
  (period-close snapshots, durable outbox); returns. (VAT report — done, CHG-0043.)
- **Phase 1 foundation complete.** Phase 2: Accounting(s1), Master Data, Inventory(s1), Purchasing(s1) done.
- **Backend only.** No frontend yet (pending a UI/UX design discussion — see Deferred).
- **Dev login:** `admin@accountrack.local` / `ChangeMe!123` · Swagger: `http://localhost:5080/swagger`

## Milestones

Legend: ✅ done · 🟡 partial (slice) · 🔜 next · ◻️ not started.

### Building blocks
- ✅ SharedKernel, Application.Abstractions, Infrastructure.Common, Web.Common, Modules.Contracts
- ✅ Solution skeleton, CQRS pipeline, error envelope, tenancy + audit interceptors, arch-fitness tests (CHG-0002)

### Phase 1 — Foundation
- ✅ **Identity** — auth (JWT + rotating refresh), RBAC + SoD, users, company grants (CHG-0003)
- ✅ **Company Management** — tenants, companies, settings; tenant context (CHG-0005)
- ✅ **Audit Log** — automatic atomic before/after capture (CHG-0006)
- ✅ **Approval Workflow** — generic engine: conditional/multi-level, SoD, auto-approve (CHG-0012)
- ✅ **Process Tracker** — document lifecycle timeline; consumes approval events (CHG-0013)
- ✅ **In-process integration events** — publisher + handlers building block (ADR-0007) (CHG-0013)
- ✅ **Cross-module atomic transactions** — shared connection + unit of work (INTEGRATION_EVENTS.md §2) (CHG-0019)
- ✅ **Notification** — in-app notifications consuming approval events; list + mark-read (CHG-0014)
- ✅ **Cross-tenant isolation integration suite** — behavioral (real SQL Server) + model-convention
  reflection over all 11 contexts (CHG-0042). MULTI_TENANCY.md §9. (Uses local/CI SQL Server with
  skip, not Testcontainers — Docker unavailable here.)

### Phase 2 — Core ERP
- 🟡 **Accounting** (slice 1 + reports + posting engine + subledgers) — chart of accounts, fiscal
  periods, double-entry journals + reversal, trial balance (CHG-0008); **Profit & Loss + Balance
  Sheet** (CHG-0016); **posting-rule / account-determination engine** + health check (CHG-0017);
  **AR/AP subledgers** — open items, allocation, aging (CHG-0018)
- ✅ **Master Data** — products, categories, units, customers, suppliers, warehouses, tax codes (CHG-0009)
- 🟡 **Inventory** (slice 1) — transaction ledger, moving-average buckets, receive/adjust/transfer,
  on-hand + stock card, `IInventoryLedger` (CHG-0010)
- ✅ **Purchasing** (procure-to-pay complete) — Purchase Orders + Approval/Process-Tracker/Notification
  (CHG-0015); **Goods Receipt** → atomic inventory + Dr Inventory/Cr GR-IR (CHG-0019); **Purchase
  Invoice** → atomic Dr GR-IR+VAT/Cr AP + AP open item, clears GR-IR (CHG-0020); **Supplier Payment**
  → atomic Dr AP/Cr Cash-Bank + AP allocation (CHG-0021). (Returns are a later enhancement.)
- ✅ **Sales** (order-to-cash complete) — Sales Orders + Approval/event integration (CHG-0022);
  **Delivery Order** → atomic stock issue + Dr COGS/Cr Inventory (CHG-0023); **Sales Invoice** →
  atomic Dr AR/Cr Revenue+VAT + AR open item (CHG-0024); **Customer Payment** → atomic Dr Cash-Bank/
  Cr AR + AR allocation (CHG-0025). (Returns are a later enhancement.)
- 🟡 **Reporting** — P&L, Balance Sheet, Cash Flow, AR/AP aging, **VAT (CHG-0043)** done; inventory
  valuation + GL/account detail open

### Phase 3 / 4
- ◻️ Manufacturing, CRM, Assets, Payroll, multi-currency, FIFO, AI/BI (see ROADMAP.md)

## Deferred backlog (planned slices / debts)

- **Returns:** ✅ sales returns (CHG-0046, BR-SAL-8) + purchase returns (CHG-0047, BR-PUR-7) — credit/
  debit notes that restock/de-stock at cost, reverse Revenue/VAT/AR resp. AP/VAT-Input, and move the
  subledger. Remaining: crediting/debiting a **fully-settled** invoice (refund/credit), and a returns
  list/landing screen (today returns are driven from the order detail).
- **CRUD completion (ADR-0029):** 🟡 master data Edit + activate/deactivate done for customers/
  suppliers/warehouses/products (CHG-0045). Remaining: UoM/categories/tax-codes/CoA edit; distinct
  `*.Edit`/`*.Delete` permissions; status-gated edit/cancel for draft documents. (BR-X-7/8.)
- **Expenses module (ADR-0030):** 🟡 done (CHG-0048) — vouchers paid from cash/bank, category→GL via
  posting rules, atomic Dr Expense [+ VAT Input] / Cr Cash-Bank. Remaining: on-account (Cr AP),
  approvals, category edit/deactivate. (BR-EXP-*.)
- **Data Import/Export (ADR-0031):** CSV/Excel import (template + dry-run + commit), CSV/Excel list
  export, PDF for documents/reports — master data first. (BR-IMP-*.)
- **Accounting slice 2 (remaining):** period-close balance snapshots, year-end close to retained
  earnings, Cash Flow report. (P&L + Balance Sheet — CHG-0016; posting-rule engine — CHG-0017;
  AR/AP subledgers — CHG-0018.)
- **Inventory slice 2:** GL posting on stock moves (Dr/Cr Inventory/COGS/Variance), stock opname,
  per-company negative-stock setting, back-dating recompute.
- **Cross-module atomic posting:** ✅ foundation done (CHG-0019) — shared connection +
  `ICrossModuleUnitOfWork`, used by Goods Receipt. Sales shipment (stock issue + COGS) and invoice
  flows will reuse it.
- **Idempotency for atomic flows:** ✅ command-level idempotency done (CHG-0040) —
  `IdempotencyBehavior` keyed off the `Idempotency-Key` header dedupes replays of the posting/create
  commands (ADR-0021). Remaining hardening: write the key in the same transaction (exactly-once) +
  RowVersion optimistic concurrency on documents.
- **Frontend:** Vue 3 SPA — **pause for a UI/UX design discussion before building** (user
  preference: not template/AI-ish).

## ▶️ Next up (recommended)

**The MVP transactional backend is complete** (procure-to-pay CHG-0019–0021, order-to-cash
CHG-0022–0025; both post atomically across modules and reconcile to the AR/AP subledgers). The
**dashboard summary endpoint** landed CHG-0026.

**Frontend** (UI/UX discussed & design language locked — see [frontend/](frontend/)): scaffolded in
CHG-0027 (`frontend/`) — app shell, light/dark toggle, login, finance dashboard. Next frontend slices:
⌘K command palette; a shared **DataTable** + **DocumentForm** in the dense register; the first real
CRUD screen (**Sales** list + Sales-Order create/detail); then the other modules' screens; i18n `id`
locale; refresh-token rotation; self-hosted font.

Backend threads that can be picked up independently if desired (none block the frontend):
- **Idempotency keys** for the atomic posting flows — ✅ done (CHG-0040, ADR-0021). Remaining:
  same-transaction key write (exactly-once) + RowVersion concurrency.
- **Reporting:** Cash Flow, AR/AP aging already exist; **VAT report (Output − Input) — done
  (CHG-0043)**; GL/account detail still open.
- **Accounting:** period-close balance snapshots, year-end close to retained earnings.
- **Inventory slice 2:** GL posting on adjustments/transfers; stock opname.
- **Returns:** purchase & sales returns/credit notes.
- A dev **customer seed** (none seeded today; created via API in e2e).

After order-to-cash, the backend is MVP-functional end to end — the natural next phase is the **Vue 3
frontend**, which requires the **UI/UX design discussion** before any build (user preference: not
template/AI-ish). Pause and raise it then.

Other open threads (not blocking): a dev **customer seed** (none seeded today); **Inventory slice 2**
GL posting on adjustments/transfers; **Accounting** period-close snapshots / Cash Flow; purchase/sales
**returns**; idempotency **exactly-once** hardening (same-transaction key + RowVersion).

## How to resume

```bash
# Run all tests
dotnet test Accountrack.sln

# Run the API (dev) with DB migrate + seed (creates schemas + dev tenant/company/admin/chart)
cd src/Bootstrapper/Accountrack.Api
ASPNETCORE_ENVIRONMENT=Development Database__Initialize=true Database__AutoMigrate=true dotnet run
# Swagger: http://localhost:5080/swagger   ·   login: admin@accountrack.local / ChangeMe!123
```

Gotchas:
- After `dotnet ef migrations add`, **rebuild the host** before `dotnet run --no-build` (otherwise
  the host's bin has a stale module DLL without the new migration).
- Each module: own EF schema + `Initial<Module>` migration; design-time factories point at a
  throwaway `Accountrack_Design` DB; the real dev DB is `Accountrack_Dev`.
- Dev tenant/company GUIDs are well-known (`1111…`/`2222…`) and shared by all seeders.
