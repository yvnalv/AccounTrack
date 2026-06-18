# STATUS.md — Project Milestones & "You Are Here"

The single place to see **where the build is and what to do next**, so any session can resume in
context. Complements: [ROADMAP.md](ROADMAP.md) (the plan), [`../CHANGELOG.md`](../CHANGELOG.md)
(the history), [MODULES.md](MODULES.md) (per-module status table).

> Update this file whenever a module/slice lands (alongside the CHANGELOG entry).

## Snapshot

- **As of:** 2026-06-18 (last change **CHG-0033**)
- **Build:** green — backend `net8.0` (186 tests); **frontend** `frontend/` builds (vue-tsc + vite).
- **MVP transactional backend complete** (procure-to-pay + order-to-cash). **Frontend** now drives
  **both flows end-to-end**: **Sales** (submit → deliver → invoice → receive payment) and
  **Purchasing** (submit → receive → bill → pay supplier), plus app shell, light/dark toggle, login,
  finance dashboard. Reusable DataTable/StatusBadge/form kit. Next frontend: read/reporting screens
  (Inventory, Accounting, Master data) + ⌘K palette + `id` locale.
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
- ◻️ **Cross-tenant isolation integration suite** (Testcontainers) — MULTI_TENANCY.md §9

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
- ◻️ **Reporting** — P&L, Balance Sheet, Cash Flow, AR/AP aging, VAT, inventory valuation

### Phase 3 / 4
- ◻️ Manufacturing, CRM, Assets, Payroll, multi-currency, FIFO, AI/BI (see ROADMAP.md)

## Deferred backlog (planned slices / debts)

- **Accounting slice 2 (remaining):** period-close balance snapshots, year-end close to retained
  earnings, Cash Flow report. (P&L + Balance Sheet — CHG-0016; posting-rule engine — CHG-0017;
  AR/AP subledgers — CHG-0018.)
- **Inventory slice 2:** GL posting on stock moves (Dr/Cr Inventory/COGS/Variance), stock opname,
  per-company negative-stock setting, back-dating recompute.
- **Cross-module atomic posting:** ✅ foundation done (CHG-0019) — shared connection +
  `ICrossModuleUnitOfWork`, used by Goods Receipt. Sales shipment (stock issue + COGS) and invoice
  flows will reuse it.
- **Idempotency for atomic flows:** posting is not yet idempotent (no inbox/dedupe key) — re-posting a
  Goods Receipt would double-receive. Add idempotency keys before exposing retries (ADR-0021).
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
- **Idempotency keys** for the atomic posting flows (ADR-0021) — retries are not yet dedupe-safe.
- **Reporting:** Cash Flow, AR/AP aging already exist; VAT report (Output − Input); GL/account detail.
- **Accounting:** period-close balance snapshots, year-end close to retained earnings.
- **Inventory slice 2:** GL posting on adjustments/transfers; stock opname.
- **Returns:** purchase & sales returns/credit notes.
- A dev **customer seed** (none seeded today; created via API in e2e).

After order-to-cash, the backend is MVP-functional end to end — the natural next phase is the **Vue 3
frontend**, which requires the **UI/UX design discussion** before any build (user preference: not
template/AI-ish). Pause and raise it then.

Other open threads (not blocking): **idempotency keys** for atomic posting (ADR-0021); a dev
**customer seed** (none seeded today); **Inventory slice 2** GL posting on adjustments/transfers;
**Accounting** period-close snapshots / Cash Flow; purchase/sales **returns**.

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
