# STATUS.md — Project Milestones & "You Are Here"

The single place to see **where the build is and what to do next**, so any session can resume in
context. Complements: [ROADMAP.md](ROADMAP.md) (the plan), [`../CHANGELOG.md`](../CHANGELOG.md)
(the history), [MODULES.md](MODULES.md) (per-module status table).

> Update this file whenever a module/slice lands (alongside the CHANGELOG entry).

## Snapshot

- **As of:** 2026-06-14 (last change **CHG-0019**)
- **Build:** green — `net8.0`, warnings-as-errors. **Tests:** 153 passing.
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
- 🟡 **Purchasing** (slice 1 + Goods Receipt) — Purchase Orders + Approval/Process-Tracker/Notification
  (CHG-0015); **Goods Receipt** → atomic inventory ledger + Dr Inventory/Cr GR-IR journal (CHG-0019).
  Remaining slice 2: Purchase Invoice → AP/VAT + clear GR-IR, Supplier Payment
- ◻️ **Sales** — Quotation → SO → Delivery → Sales Invoice → Customer Payment, returns
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

Goods Receipt (CHG-0019) posts inventory + GR-IR atomically. The clear next step is **Purchasing
slice 2 — Purchase Invoice**: bill against the receipt → post **Dr GR-IR + Dr VAT Input / Cr AP
control**, clearing GR-IR and opening an **AP subledger** item (the engine for both — posting rules
CHG-0017, AP subledger CHG-0018 — is ready). Then **Supplier Payment** (allocate AP open items,
Dr AP / Cr Cash-Bank). Alternatively start **Sales slice 1** (Quotation → SO → Delivery → Invoice),
which reuses the same atomic-posting + subledger foundation on the order-to-cash side.

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
