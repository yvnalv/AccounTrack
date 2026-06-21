# STATUS.md — Project Milestones & "You Are Here"

The single place to see **where the build is and what to do next**, so any session can resume in
context. Complements: [ROADMAP.md](ROADMAP.md) (the plan), [`../CHANGELOG.md`](../CHANGELOG.md)
(the history), [MODULES.md](MODULES.md) (per-module status table).

> Update this file whenever a module/slice lands (alongside the CHANGELOG entry).

## Snapshot

- **As of:** 2026-06-21 (last change **CHG-0076**)
- **Build:** green — backend `net8.0` (298 tests); **frontend** `frontend/` builds (vue-tsc + vite).
  Latest: **dynamic roles & access management** — 6 seeded standard roles, editable permission matrix
  in Settings (CHG-0076); **distinct Edit/Cancel permissions for Sales & Purchasing documents** (CHG-0075);
  **returns against settled invoices (refund) + returns list screens** (CHG-0074); per-company
  negative-stock policy (CHG-0073); Expenses on-account (Cr AP) + category edit/deactivate (CHG-0072);
  Chart-of-Accounts edit + activate/deactivate, new Accounts tab (CHG-0071); edit draft SO/PO
  (CHG-0070); list search + full-width content + gated VAT tab (CHG-0069); optional VAT (PKP) flag
  (CHG-0068); collapsible/responsive sidebar (CHG-0067); insight-rich dashboard (CHG-0063/0064).
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
- ✅ **Identity** — auth (JWT + rotating refresh), RBAC + SoD, users, company grants (CHG-0003);
  **6 seeded standard roles + dynamic role/permission management in Settings (CHG-0076)**. Pending:
  user-management UI (assign roles/companies) and public organization sign-up.
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
- 🟡 **Inventory** (slice 1 + 2) — transaction ledger, moving-average buckets, receive/adjust/transfer,
  on-hand + stock card, `IInventoryLedger` (CHG-0010); **slice 2 (CHG-0057)** — adjustments + stock
  opname post Dr/Cr Inventory↔Variance to the GL atomically (Adjust/Count UI); **per-company
  negative-stock policy (CHG-0073)**. Remaining: back-dating recompute
- ✅ **Purchasing** (procure-to-pay complete) — Purchase Orders + Approval/Process-Tracker/Notification
  (CHG-0015); **Goods Receipt** → atomic inventory + Dr Inventory/Cr GR-IR (CHG-0019); **Purchase
  Invoice** → atomic Dr GR-IR+VAT/Cr AP + AP open item, clears GR-IR (CHG-0020); **Supplier Payment**
  → atomic Dr AP/Cr Cash-Bank + AP allocation (CHG-0021). (Returns are a later enhancement.)
- ✅ **Sales** (order-to-cash complete) — Sales Orders + Approval/event integration (CHG-0022);
  **Delivery Order** → atomic stock issue + Dr COGS/Cr Inventory (CHG-0023); **Sales Invoice** →
  atomic Dr AR/Cr Revenue+VAT + AR open item (CHG-0024); **Customer Payment** → atomic Dr Cash-Bank/
  Cr AR + AR allocation (CHG-0025). (Returns are a later enhancement.)
- ✅ **Reporting** — P&L, Balance Sheet, **VAT (CHG-0043)**, **Cash Flow (CHG-0056)**, **General
  Ledger / account detail (CHG-0058)**, AR/AP aging, **inventory valuation (CHG-0062)** done

### Phase 3 / 4
- ◻️ Manufacturing, CRM, Assets, Payroll, multi-currency, FIFO, AI/BI (see ROADMAP.md)

## Deferred backlog (planned slices / debts)

- **Returns:** ✅ sales returns (CHG-0046, BR-SAL-8) + purchase returns (CHG-0047, BR-PUR-7) — credit/
  debit notes that restock/de-stock at cost, reverse Revenue/VAT/AR resp. AP/VAT-Input, and move the
  subledger; ✅ **settled-invoice refund + returns list screens (CHG-0074)** — returning a paid
  invoice refunds the excess to a chosen cash/bank account; dedicated sales/purchase returns lists.
  Remaining: a standalone return-detail page.
- **CRUD completion (ADR-0029):** 🟡 master data Edit + activate/deactivate done for customers/
  suppliers/warehouses/products (CHG-0045) **and Units/Categories/Tax-codes (CHG-0060)**; **draft
  SO/PO cancel (CHG-0061) + edit (CHG-0070); CoA edit + deactivate (CHG-0071)**; **distinct
  `Sales/Purchasing.Edit` + `.Cancel` permissions for documents (CHG-0075)**. Remaining: split
  master-data `MasterData.Manage` into Create/Edit/Delete; a dedicated permission for CoA edit. (BR-X-7/8.)
- **Expenses module (ADR-0030):** 🟡 done (CHG-0048/0072) — vouchers paid from cash/bank **or on
  account (Cr AP, opens an AP subledger item)**; category→GL via posting rules; atomic Dr Expense
  [+ VAT Input] / Cr Cash-Bank|AP; **category edit + activate/deactivate (CHG-0072)**. Remaining:
  approvals. (BR-EXP-*.)
- **Data Import/Export (ADR-0031):** 🟡 CSV **import** for all four master-data entities
  (CHG-0049/0050); **CSV + Excel export** across all list menus — master data, sales orders, purchase
  orders, inventory on-hand, expenses (CHG-0051, ClosedXML/MIT); **PDF** documents — Invoice +
  Quotation (CHG-0052), **purchase-document PDFs** PO + bill (CHG-0054) — and **report PDFs**
  TB/P&L/BS/VAT (CHG-0053), all QuestPDF, with the **brand logo** embedded as vector SVG (CHG-0054).
  Remaining: optional Plus Jakarta Sans font embedding, list-export-with-active-filters, Excel
  *import*, async large files. (BR-IMP-*.)
- **Accounting slice 2 (remaining):** period-close balance snapshots (rebuildable, ADR-0022). (P&L +
  Balance Sheet — CHG-0016; posting-rule engine — CHG-0017; AR/AP subledgers — CHG-0018; **Cash Flow
  — CHG-0056**; **year-end close to retained earnings — CHG-0059**.)
- **Inventory slice 2:** ✅ GL posting on stock adjustments (Dr/Cr Inventory↔Variance) + stock opname
  done (CHG-0057); ✅ **per-company negative-stock policy (CHG-0073)**. Remaining: back-dating
  recompute. (Transfers are GL-neutral under a single Inventory control account.)
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
- **Reporting:** Cash Flow, AR/AP aging, **VAT (CHG-0043)**, **General Ledger / account detail
  (CHG-0058)**, **inventory valuation (CHG-0062)** done. Reporting suite complete.
- **Accounting:** period-close balance snapshots (year-end close to retained earnings — done CHG-0059).
- **Inventory slice 2:** ✅ GL posting on adjustments + stock opname done (CHG-0057); ✅ per-company
  negative-stock policy (CHG-0073); remaining: back-dating recompute.
- **Returns:** purchase & sales returns/credit notes.
- A dev **customer seed** (none seeded today; created via API in e2e).

After order-to-cash, the backend is MVP-functional end to end — the natural next phase is the **Vue 3
frontend**, which requires the **UI/UX design discussion** before any build (user preference: not
template/AI-ish). Pause and raise it then.

Other open threads (not blocking): a dev **customer seed** (none seeded today); **Inventory slice 2**
remaining (back-dating recompute); **Accounting** period-close snapshots; purchase/sales
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
