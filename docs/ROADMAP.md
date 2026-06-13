# ROADMAP.md

Delivery roadmap for Accountrack. Phases follow `CLAUDE.md`; the MVP target is **Phase 1 +
Phase 2 core ERP** (ADR-confirmed 2026-06-13). Ordering favors building the platform and the
accounting/inventory engines before the transactional modules that depend on them.

## Phase 1 — Foundation (Platform)

Goal: a secure, multi-tenant, auditable platform with no business modules yet.
Status legend: ✅ done · 🔜 next · ◻️ planned. (See `../CHANGELOG.md` for detail.)

1. ✅ **Solution skeleton** — modular-monolith layout, shared kernel (Money, entity bases, audit,
   domain-event base, Result), base DbContext (tenant + soft-delete filters, audit interceptor,
   RowVersion), CQRS pipeline, error middleware, architecture-fitness tests. (ARCHITECTURE.md)
2. ✅ **Identity** — users, RBAC permission catalog + roles, JWT + rotating refresh tokens,
   login/logout, company grants. (SECURITY.md)
3. ✅ **Company Management** — tenants, companies, company settings; HttpContext-backed tenant
   context. (MULTI_TENANCY.md)
4. ✅ **Audit Log** — SaveChanges interceptor capturing before/after into a shared audit table
   (ADR-0026); tenant-scoped read API.
5. 🔜 **Approval Workflow** (engine) + **Process Tracker** + **Notification** (in-app + email).
6. ◻️ **Cross-tenant isolation test suite** (permanent, integration). (MULTI_TENANCY.md §9)

Exit criteria: a user can be created, assigned roles, log in, switch between granted companies,
and every change is audited; isolation tests green.

> Note: the cross-tenant isolation suite (item 6) is integration-level (Testcontainers) and is
> still outstanding — tenant isolation is currently enforced and covered by unit/architecture
> tests, with the dedicated integration suite to follow.

## Phase 2 — Core ERP

Goal: end-to-end operations with integrated accounting and inventory.

7. **Master Data** — CoA (template + system accounts), tax code PPN11, products/UoM, customers,
   suppliers, warehouses.
8. **Accounting engine** — journals + balanced-posting invariant, fiscal years/periods
   (open/close/lock), posting-rule resolver, AR/AP subledgers, reversal, snapshots.
   (ACCOUNTING_DESIGN.md, POSTING_RULES.md)
9. **Inventory engine** — `InventoryTransaction` ledger, moving-average buckets, adjustments,
   transfers, opname; `IInventoryLedger`. (INVENTORY_DESIGN.md)
10. **Purchasing** — PR → PO → Goods Receipt (→ inventory + GR/IR) → Purchase Invoice (→ AP +
    VAT Input + clear GR/IR) → Supplier Payment; Purchase Returns; approvals.
11. **Sales** — Quotation → SO → Delivery (→ inventory issue + COGS) → Sales Invoice (→ AR +
    Revenue + VAT Output) → Customer Payment; Sales Returns; approvals.
12. **Reporting** — Trial Balance, P&L, Balance Sheet, Cash Flow, AR/AP Aging, GL, VAT, inventory
    valuation/stock-card; export + drill-down.
13. **Frontend** — Vue 3 SPA covering the above, i18n (EN/ID).

Exit criteria (MVP done): run a full accounting month end-to-end across two companies; books
reconcile (TB balances, subledgers reconcile to control accounts, inventory reconciles to GL);
period closes; reports + VAT correct.

## Phase 3 — Advanced ERP
- Manufacturing (BOM, production orders, WIP, finished goods, production costing — reusing the
  inventory ledger's reserved production movement types and cost layers).
- CRM, Fixed Assets, Payroll, expanded Tax (multi-rate, withholding/PPh, e-Faktur integration).
- Multi-currency + FX revaluation (schema already reserved, ADR-0013).
- FIFO/lot/batch costing (ledger already reserves cost layers, ADR-0015).
- Bank reconciliation; consolidated multi-company financials.

## Phase 4 — Intelligence
- AI assistant, forecasting, analytics/BI, visual workflow builder.
- Infrastructure scale-out: Redis caching, Hangfire, RabbitMQ, OpenTelemetry, ElasticSearch;
  optional extraction of modules into services (architecture already supports it).

## Cross-Phase Engineering Tracks (continuous)
- Testing (unit/integration; high-priority accounting/inventory/approval/security). (TESTING.md)
- CI/CD pipeline, environments (Local/Dev/UAT/Prod). (DEPLOYMENT.md)
- Documentation kept synchronized with implementation (`CLAUDE.md` Documentation Rules).
- Security reviews on changed code before merge.

## Sequencing Principle
Build dependencies before dependents: platform → master data → accounting & inventory engines →
purchasing & sales → reporting → UI. This avoids rework in the highest-risk areas (tenancy,
accounting, inventory) and lets transactional modules plug into stable engines.
