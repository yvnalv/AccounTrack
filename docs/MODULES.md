# MODULES.md

The module catalog: purpose, responsibilities, dependencies, key events, and MVP scope for each
module. Boundaries and communication rules are in ARCHITECTURE.md and INTEGRATION_EVENTS.md.

> Dependency note: "depends on" means consumes another module's **contract or events** — never
> its tables (ADR-0007).

## Implementation Status

| Module | Status | Notes |
|---|---|---|
| Identity | ✅ Implemented | auth (JWT + rotating refresh), RBAC + SoD permissions, users, company grants |
| Company Management | ✅ Implemented | tenants, companies, company settings; dev tenant/company seeder |
| Audit Log | ✅ Implemented | automatic atomic before/after capture (ADR-0026), tenant-scoped read API |
| Approval Workflow | ✅ Implemented | generic, document-agnostic engine: conditional + multi-level definitions (User/Role approvers), submit → approve/reject with segregation of duties, auto-approve when no rule matches |
| Process Tracker | ✅ Implemented | per-document lifecycle timeline; consumes approval integration events (in-process event dispatch, ADR-0007) |
| Notification | ✅ Implemented | in-app notifications; consumes approval events (notifies the submitter); list + mark-read. Email channel is future |
| Accounting | 🟡 Slice 1 + reports + posting engine + subledgers | chart of accounts, fiscal periods (open/close/lock), double-entry journal posting + reversal, trial balance, **Profit & Loss + Balance Sheet** (derived from GL), **posting-rule / account-determination engine** (configurable, most-specific-wins, health check), **AR/AP subledgers** (open items, payment allocation, aging). Remaining slice 2: period-close snapshots, year-end close, Cash Flow |
| Master Data | ✅ Implemented | products (+ categories, units of measure), customers, suppliers, warehouses, tax codes; create + list, dev seed |
| Inventory | 🟡 Slice 1 | transaction ledger (source of truth), moving-average buckets, receive/adjust/transfer, on-hand + stock-card; `IInventoryLedger`. Slice 2: GL posting on moves, stock opname, negative-stock setting, FIFO option |
| Purchasing | ✅ Procure-to-pay | Purchase Orders (Approval + Process Tracker + Notification); **Goods Receipt** (atomic inventory + Dr Inventory/Cr GR-IR); **Purchase Invoice** (atomic Dr GR-IR + VAT Input / Cr AP + AP subledger, three-way-match lite clears GR-IR); **Supplier Payment** (atomic Dr AP / Cr Cash-Bank + AP allocation). Returns are a later enhancement |
| Sales | ✅ Order-to-cash | Sales Orders (Approval + event-driven status); **Delivery Order** (atomic stock issue + Dr COGS/Cr Inventory); **Sales Invoice** (atomic Dr AR / Cr Revenue + VAT Output + AR subledger); **Customer Payment** (atomic Dr Cash-Bank / Cr AR + AR allocation). Returns are a later enhancement |
| Reporting | 🟡 Partial | TB/P&L/BS/VAT + AR/AP aging/Cash Flow done; GL drill-down + export pending |
| Expenses | ◻️ Planned | Phase 2 — operating-expense vouchers, category→GL via posting rules (ADR-0030) |
| Data Import/Export | ◻️ Planned | Cross-cutting — CSV/Excel import (template + dry-run), CSV/Excel/PDF export (ADR-0031) |
| Manufacturing | ◻️ Planned | Phase 3 |

> **CRUD status (ADR-0029):** master-data and transactional screens currently ship **list + create**;
> **Edit** and **deactivate/cancel** (soft-delete, status-gated) are a planned cross-module pass. The
> "full CRUD" notes below are the target, not yet the implemented state.

(Authoritative change history is in [`../CHANGELOG.md`](../CHANGELOG.md).)

## Foundation Modules

### Identity
- **Purpose:** authentication, authorization, users/roles/permissions.
- **Responsibilities:** login, JWT issue + refresh-token rotation, password management, RBAC
  permission catalog, role assignment, user→company grants.
- **Depends on:** Company (for company grants).
- **Emits:** `UserCreated`, `PermissionsChanged`.
- **MVP:** email/password, JWT + refresh, RBAC, SoD-capable permissions, company scope.
  OAuth/MFA = later.

### Company Management
- **Purpose:** tenant & company setup and configuration.
- **Responsibilities:** tenants, companies, company settings (functional currency, fiscal-year
  start, timezone, numbering formats, negative-stock flag, posting-rule overrides).
- **Depends on:** none (foundation).
- **Emits:** `CompanyCreated`, `CompanySettingsChanged`.
- **MVP:** create tenant/company, core settings that Accounting/Inventory require.

### Audit Log
- **Purpose:** immutable field-level change history + security events.
- **Responsibilities:** capture before/after via SaveChanges interceptor; security event log;
  append-only storage.
- **Depends on:** all (cross-cutting, via interceptor + events).
- **MVP:** automatic entity-change capture, query API (permissioned).

### Approval Workflow
- **Purpose:** generic, document-agnostic approval routing.
- **Responsibilities:** approval definitions/steps, requests/actions, single/multi/conditional
  routing, SoD enforcement, escalation (model). See WORKFLOW_APPROVAL.md.
- **Depends on:** Identity (approvers), Notification.
- **Emits:** `ApprovalRequested`, `ApprovalDecided`.
- **MVP:** single/multi/conditional approval; escalation timers optional.

### Process Tracker
- **Purpose:** user-facing per-document lifecycle timeline.
- **Responsibilities:** subscribe to state/approval events, append milestones, expose timeline.
- **Depends on:** events from all transactional modules.
- **MVP:** timeline for Sales/Purchasing/Inventory/Accounting documents.

### Notification
- **Purpose:** email + in-app notifications.
- **Responsibilities:** templates, queued outbound email, in-app inbox, read state.
- **Depends on:** Identity; consumes events from many modules (eventual).
- **MVP:** in-app notifications + email for approvals; event-driven/queued from day one.

## Master Data

### Products / Categories / Types / UoM
- **Purpose:** product catalog and units.
- **Responsibilities:** products (SKU, stock-tracked flag, sales/purchase/tax defaults),
  categories, types, UoM + conversions.
- **Depends on:** Accounting (default account refs), Company.
- **MVP:** full CRUD, UoM conversion, posting-relevant defaults.

### Customers / Suppliers
- **Purpose:** trading-partner master.
- **Responsibilities:** profiles, contacts, terms, credit limit, tax id (NPWP).
- **Depends on:** Company.
- **MVP:** full CRUD + terms used by AR/AP.

### Warehouses
- **Purpose:** stock locations.
- **MVP:** CRUD; referenced by inventory cost buckets.

### Chart of Accounts
- **Purpose:** the accounting structure (lives in Accounting module; surfaced as master data).
- **Responsibilities:** accounts with type/normal-balance/hierarchy/control flags; default
  template seed.
- **MVP:** CRUD + seeded template + system accounts.

### Tax Setup
- **Purpose:** tax codes (PPN). ADR-0012.
- **MVP:** `PPN11` seeded; tax code referenced on invoice lines; output/input accounts mapped.

## ERP Transactional Modules

### Sales
- **Purpose:** the order-to-cash flow.
- **Responsibilities:** Quotation → Sales Order → Delivery Order → Sales Invoice → Customer
  Payment, Sales Returns; status workflow + approvals.
- **Depends on:** Master Data, Inventory (issue + COGS), Accounting (AR/Revenue/VAT), Approval.
- **Emits:** `SalesOrderConfirmed`, `GoodsShipped`, `SalesInvoicePosted`,
  `CustomerPaymentReceived`, `SalesReturnPosted`.
- **MVP:** full flow incl. partial delivery/invoice/payment.

### Purchasing
- **Purpose:** the procure-to-pay flow.
- **Responsibilities:** Purchase Request → PO → Goods Receipt → Purchase Invoice → Supplier
  Payment, Purchase Returns; approvals; GR/IR (ADR-0018).
- **Depends on:** Master Data, Inventory (receipt), Accounting (AP/VAT/GR-IR), Approval.
- **Emits:** `PurchaseOrderApproved`, `GoodsReceived`, `PurchaseInvoicePosted`,
  `SupplierPaymentPosted`, `PurchaseReturnPosted`.
- **MVP:** full flow; three-way match policy documented, basic matching.

### Inventory
- **Purpose:** the stock ledger + valuation. INVENTORY_DESIGN.md.
- **Responsibilities:** `InventoryTransaction` ledger, moving-average buckets, adjustments,
  transfers, opname; exposes `IInventoryLedger`.
- **Depends on:** Master Data; emits cost to Accounting via atomic contracts.
- **Emits:** `StockAdjusted`, `StockTransferred`, `StockMovementRecorded`.
- **MVP:** receipts/issues/adjustments/transfers/opname, moving average, reconciliation.

### Accounting
- **Purpose:** the GL + financials. ACCOUNTING_DESIGN.md, POSTING_RULES.md.
- **Responsibilities:** journals, GL, fiscal periods, AR/AP subledgers, posting rules,
  snapshots, financial reports, VAT report; exposes `IJournalPoster`, `IFiscalPeriodQuery`.
- **Depends on:** Master Data (accounts/tax); consumes events from Sales/Purchasing/Inventory.
- **Emits:** `JournalPosted`, `JournalReversed`, `FiscalPeriodClosed`.
- **MVP:** auto-posting, periods, subledgers, TB/P&L/BS/Cash Flow/Aging/GL/VAT reports.

### Reporting
- **Purpose:** read models and report delivery.
- **Responsibilities:** report queries over GL/snapshots/inventory; export (PDF/Excel);
  drill-down; report-level permissions.
- **Depends on:** Accounting, Inventory, Sales, Purchasing (read).
- **MVP:** the financial + inventory + sales/purchasing operational reports.

### Expenses (ADR-0030)
- **Purpose:** capture day-to-day operating expenses without manual journals.
- **Responsibilities:** expense vouchers (date, payee, lines), expense **categories** mastered and
  mapped to expense GL accounts via posting rules; automatic atomic posting (Dr Expense [+ VAT Input]
  / Cr Cash-Bank or AP); approvals + process tracker.
- **Depends on:** Master Data (categories/payees), Accounting (posting/AP), Approval.
- **Emits:** `ExpensePosted`.
- **MVP:** create/list/edit (draft), post, pay-or-on-account; PPh withholding is Phase 3. Payroll is
  out of scope (Phase 3) — salaries-as-cash use a category.

## Cross-Cutting Capabilities

### Data Import & Export (ADR-0031)
- **Purpose:** bulk data in/out across every list-bearing module.
- **Responsibilities:** per-entity CSV/Excel **import** with a downloadable template + **dry-run
  preview** (per-row validation, create/update/skip summary) + transactional commit; **export** of
  lists to CSV/Excel (honoring filters) and documents/reports to PDF. Shared building block opted into
  per entity; reuses each entity's domain validation. Permission-gated (`*.Import` / `*.Export`),
  tenant-scoped, audited.
- **Depends on:** every module that exposes importable/exportable entities.
- **MVP:** master-data import first (onboarding) + list export; PDF for documents/reports next; async
  for large files later (Hangfire).

### Manufacturing (Phase 3 — not MVP)
- **Purpose:** BOM, production orders, WIP, finished goods, production costing.
- **Architecture note:** inventory ledger already reserves ProductionConsume/ProductionReceive
  movement types and cost-layer structure so WIP slots in without restructuring.

## Module Dependency Overview

```
Identity ─┐
Company  ─┼─► (foundation, used by all)
          │
MasterData ─► Sales, Purchasing, Inventory, Accounting
Inventory  ─► Sales (COGS), Purchasing (receipt), Accounting (valuation)
Accounting ◄─ Sales, Purchasing, Inventory   (events → journals)
Approval   ─► Sales, Purchasing, Inventory (gating)
Audit, ProcessTracker, Notification ◄─ events from all (eventual)
Reporting  ◄─ read GL/snapshots/ledgers
```
