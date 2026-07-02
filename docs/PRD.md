# PRD.md — Product Requirements

Product requirements for Accountrack. Subordinate to `CLAUDE.md`. Scope decisions confirmed
2026-06-13 are recorded in DECISIONS.md (ADR-0012/0013, plus full Phase 1+2 MVP).

## 1. Vision

Accountrack is a modern ERP and accounting platform for SMB and mid-market trading,
distribution, and manufacturing businesses — built to grow into a complete business operating
system (accounting, sales, purchasing, inventory, manufacturing, reporting, automation, BI, AI).

## 2. Goals & Non-Goals

**Goals (MVP)**
- A correct, auditable, double-entry accounting core with automatic posting.
- Multi-company from day one under a single tenant; strict data isolation.
- End-to-end order-to-cash and procure-to-pay with integrated inventory and accounting.
- Moving-average inventory valuation with full traceability.
- Indonesian PPN (11%) compliant invoicing.
- RBAC + approval workflows + complete audit trail.
- English + Bahasa Indonesia UI.

**Non-Goals (MVP)**
- Manufacturing/WIP, payroll, fixed assets, CRM (Phase 3).
- Multi-currency FX / revaluation (schema reserved; ADR-0013).
- Multi-jurisdiction tax, withholding tax (Phase 2/3).
- AI assistant, forecasting, workflow builder (Phase 4).
- Microservices (modular monolith for MVP; ADR-0001).

## 3. Target Users & Personas

- **Business Owner / Director** — oversight, approvals, financial reports.
- **Accountant / Finance** — journals, periods, AR/AP, tax, reports.
- **Sales staff** — quotations, orders, deliveries, invoices, receipts.
- **Purchasing staff** — requests, POs, receipts, bills, payments.
- **Warehouse staff** — receipts, shipments, adjustments, transfers, opname.
- **Administrator** — users, roles, companies, settings.

## 4. MVP Functional Scope (Phase 1 + Phase 2 core ERP)

### Foundation
- Authentication (email/password, JWT + refresh), RBAC with configurable permissions + SoD.
- Tenant & multi-company management and settings.
- Immutable audit log; approval workflow (single/multi/conditional); process tracker;
  notifications (in-app + email for approvals).

### Master Data
- Products (+ categories, types, UoM/conversions), Customers, Suppliers, Warehouses,
  Chart of Accounts (seeded template + system accounts), Tax codes (PPN11).

### Sales (order-to-cash)
- Quotation → Sales Order → Delivery Order → Sales Invoice → Customer Payment; Sales Returns.
- Status workflow, approvals, partial delivery/invoice/payment, AR subledger, COGS at shipment.

### Purchasing (procure-to-pay)
- Purchase Request → PO → Goods Receipt → Purchase Invoice → Supplier Payment; Purchase Returns.
- Approvals, GR/IR clearing, AP subledger, basic three-way match.

### Inventory
- Transaction ledger, moving-average costing, adjustments, transfers, opname, valuation +
  reconciliation, stock-card reporting.

### Accounting
- Automatic double-entry posting, fiscal periods (open/close/lock), AR/AP subledgers, posting
  rules, PPN, reversal-only corrections.
- Reports: Trial Balance, P&L, Balance Sheet, Cash Flow, AR/AP Aging, General Ledger, VAT.

### Reporting
- Financial + inventory + sales/purchasing operational reports; export (PDF/Excel); drill-down;
  permission-gated.

### Expenses (ADR-0030)
- Operating-expense vouchers (electricity, transport, rent, supplies, salaries-as-cash, etc.):
  category → expense GL account via posting rules, optional tax, paid or on-account (AP), automatic
  atomic posting, approvals. (Full payroll is Phase 3.)

### Cross-Cutting: Records & Bulk Data
- **CRUD across the app (ADR-0029):** Edit + deactivate (soft-delete) for master data; status-gated
  edit/cancel for draft documents; posted documents corrected by reversal/return only.
- **Import/Export (ADR-0031):** CSV/Excel import with per-entity templates and a validated dry-run;
  CSV/Excel export of lists; PDF export of documents and financial reports.

## 5. Key Non-Functional Requirements

- **Correctness & auditability** first; books reconcile; full traceability end-to-end.
- **Security**: tenant isolation, RBAC, SoD, immutable audit (SECURITY.md, MULTI_TENANCY.md).
- **Performance**: report queries backed by period snapshots; sensible pagination; TenantId-
  leading indexes.
- **Reliability**: atomic financial postings; outbox for eventual side effects; idempotency.
- **i18n**: no hardcoded UI text; English default + Bahasa Indonesia.
- **Maintainability**: modular monolith, clean architecture, enforced boundaries.

## 6. Success Metrics (MVP)
- A company can run a full month: transact sales & purchases, value inventory, close the period,
  and produce a balanced Trial Balance, P&L, Balance Sheet, and VAT report that reconcile to the
  subledgers and inventory.
- Zero cross-tenant data exposure (verified by the isolation test suite).
- All high-priority accounting/inventory/security/approval paths covered by tests.

## 7. Constraints
- Tech stack and non-negotiables per `CLAUDE.md` (.NET 8, PostgreSQL, Vue 3/TS, Tailwind,
  modular monolith, GUID PKs, soft delete, double-entry, inventory ledger, moving average).

## 8. Assumptions & Dependencies
- Launch market is Indonesia; functional currency typically IDR.
- One tenant may own multiple companies; users are scoped to a subset of companies.
- Statutory e-Faktur / tax-authority integrations are out of MVP (future).

## 9. Open Product Questions (tracked, not blocking docs)
- e-Faktur / DJP integration timing.
- Bank reconciliation & cash management depth in MVP vs Phase 3.
- Consolidated multi-company financials timing.
- Customer/supplier portal (self-service) — future.
