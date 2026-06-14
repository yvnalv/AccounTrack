# STATUS.md — Project Milestones & "You Are Here"

The single place to see **where the build is and what to do next**, so any session can resume in
context. Complements: [ROADMAP.md](ROADMAP.md) (the plan), [`../CHANGELOG.md`](../CHANGELOG.md)
(the history), [MODULES.md](MODULES.md) (per-module status table).

> Update this file whenever a module/slice lands (alongside the CHANGELOG entry).

## Snapshot

- **As of:** 2026-06-14 (last change **CHG-0012**)
- **Build:** green — `net8.0`, warnings-as-errors. **Tests:** 103 passing.
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
- 🔜 **Process Tracker** — document lifecycle timeline
- 🔜 **Notification** — in-app + email
- ◻️ **Cross-tenant isolation integration suite** (Testcontainers) — MULTI_TENANCY.md §9

### Phase 2 — Core ERP
- 🟡 **Accounting** (slice 1) — chart of accounts, fiscal periods (open/close/lock), double-entry
  journals + reversal, trial balance (CHG-0008)
- ✅ **Master Data** — products, categories, units, customers, suppliers, warehouses, tax codes (CHG-0009)
- 🟡 **Inventory** (slice 1) — transaction ledger, moving-average buckets, receive/adjust/transfer,
  on-hand + stock card, `IInventoryLedger` (CHG-0010)
- ◻️ **Purchasing** — PR → PO → Goods Receipt → Purchase Invoice → Supplier Payment, returns
- ◻️ **Sales** — Quotation → SO → Delivery → Sales Invoice → Customer Payment, returns
- ◻️ **Reporting** — P&L, Balance Sheet, Cash Flow, AR/AP aging, VAT, inventory valuation

### Phase 3 / 4
- ◻️ Manufacturing, CRM, Assets, Payroll, multi-currency, FIFO, AI/BI (see ROADMAP.md)

## Deferred backlog (planned slices / debts)

- **Accounting slice 2:** posting rules / account determination (POSTING_RULES.md), AR/AP
  subledgers, period-close balance snapshots, financial reports (P&L / Balance Sheet / Cash Flow).
- **Inventory slice 2:** GL posting on stock moves (Dr/Cr Inventory/COGS/Variance), stock opname,
  per-company negative-stock setting, back-dating recompute.
- **Cross-module atomic posting:** making a Sales shipment (stock issue + COGS journal) and an
  invoice (AR/AP + journal) atomic across module DbContexts — to be designed when Sales/Purchasing
  land (INTEGRATION_EVENTS.md §2).
- **Frontend:** Vue 3 SPA — **pause for a UI/UX design discussion before building** (user
  preference: not template/AI-ish).

## ▶️ Next up (recommended)

Two reasonable paths:
1. **Finish Phase 1 foundation** — Process Tracker + Notification (both event-driven; most useful
   once transactional modules emit events). Smaller, completes the foundation.
2. **Purchasing (procure-to-pay)** — closes the loop and forces the deferred cross-module GL
   integration: PO → **Goods Receipt** (inventory ledger via `IInventoryLedger` + Dr Inventory /
   Cr GR-IR) → **Purchase Invoice** (Dr GR-IR + VAT Input / Cr AP) → **Supplier Payment**.
   Exercises Master Data + Inventory + Accounting + Approval together — the highest-value vertical.

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
