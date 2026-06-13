# BUSINESS_RULES.md

Authoritative catalog of business rules. Rules must never live only in source code
(`CLAUDE.md`). Each rule has an id `BR-<MODULE>-<n>` for reference in code comments and tests.

Legend: rules marked **(invariant)** are enforced as hard domain invariants; **(config)** are
configurable per company with the stated default.

## Cross-Cutting

- **BR-X-1 (invariant)** Every business record carries `TenantId` and `CompanyId`; both are
  stamped from ambient context, never client-supplied.
- **BR-X-2 (invariant)** Business data is never physically deleted; use soft delete.
- **BR-X-3 (invariant)** Every change to a business entity is captured in the immutable audit log.
- **BR-X-4 (invariant)** Money is stored as decimal with an explicit currency; one functional
  currency per company.
- **BR-X-5** Document numbers are unique per company per document type, generated from a
  per-company sequence with a configurable format.
- **BR-X-6 (config)** A cancelled document's number is not reused; sequences are not rewound
  (default). Gap reporting is available.

## Accounting

- **BR-ACC-1 (invariant)** Every journal entry is balanced: Σ debits = Σ credits.
- **BR-ACC-2 (invariant)** A journal entry has at least two lines; each line is debit-only or
  credit-only and non-negative.
- **BR-ACC-3 (invariant)** A posted journal cannot be edited or deleted; corrections are made by
  a reversing entry.
- **BR-ACC-4 (invariant)** No journal may post into a Closed or Locked fiscal period.
- **BR-ACC-5 (invariant)** Journals post only to active, postable (leaf) accounts.
- **BR-ACC-6 (invariant)** Posting is idempotent per (source, source document, event type).
- **BR-ACC-7 (invariant)** Control-account lines (AR/AP) reference a subledger party
  (customer/supplier).
- **BR-ACC-8** AR/AP subledger outstanding totals reconcile to their GL control account balances.
- **BR-ACC-9** A payment may not allocate more than an open item's outstanding amount; excess
  goes to advances.
- **BR-ACC-10** Reopening a closed period requires `Accounting.PeriodReopen` and is audited;
  locked periods need an admin override.
- **BR-ACC-11** Year-end close rolls net P&L into Retained Earnings.
- **BR-ACC-12 (config)** Rounding differences post to the Rounding account; default rounding
  half-up to the currency scale.
- **BR-ACC-13** All financial reports derive from the GL/snapshots, never transactional tables.

## Tax (PPN)

- **BR-TAX-1 (config)** Default tax is `PPN11` at 11%, exclusive. Tax is captured per invoice line.
- **BR-TAX-2 (invariant)** Sales tax posts to VAT Output (liability); purchase tax posts to VAT
  Input (asset).
- **BR-TAX-3** VAT payable/claimable = VAT Output − VAT Input for the period.

## Inventory

- **BR-INV-1 (invariant)** `InventoryTransaction` is the source of truth; on-hand and value are
  derived from it.
- **BR-INV-2 (invariant)** Costing is moving weighted average per (Company × Warehouse × Product).
- **BR-INV-3 (config, default disallow)** An issue that would drive on-hand negative is rejected;
  negative stock is opt-in per company.
- **BR-INV-4 (invariant)** Movements post chronologically; no back-dating into a closed period.
- **BR-INV-5** Back-dating within the open period triggers a forward recompute of the affected
  cost bucket.
- **BR-INV-6 (invariant)** A transfer carries source moving-average cost to the destination
  warehouse.
- **BR-INV-7** Inventory valuation reconciles to the Inventory GL account.
- **BR-INV-8** Stock opname variances are posted as adjustments; large variances may require
  approval.
- **BR-INV-9 (invariant)** Every costed movement that affects the GL is posted atomically with
  its journal.

## Sales

- **BR-SAL-1 (invariant)** A Sales Order requires at least one line item.
- **BR-SAL-2 (invariant)** A Sales Invoice cannot invoice more quantity than ordered (across all
  invoices for the order).
- **BR-SAL-3** A Delivery Order cannot ship more than ordered (across all deliveries).
- **BR-SAL-4** COGS is recognized at shipment (Delivery Order), using moving-average cost at that
  moment — not at invoicing.
- **BR-SAL-5** A Sales Invoice posts AR + Revenue + VAT Output and creates an AR open item.
- **BR-SAL-6 (config)** A Sales Order exceeding the customer's credit limit requires approval.
- **BR-SAL-7** Partial delivery, partial invoicing, and partial payment are supported.
- **BR-SAL-8** A Sales Return reverses revenue/tax (credit note) and returns goods to stock at
  the original COGS cost.

## Purchasing

- **BR-PUR-1 (invariant)** A Purchase Order requires at least one line item.
- **BR-PUR-2 (config)** A Purchase Order over the configured threshold requires approval
  (single/multi/conditional).
- **BR-PUR-3** Goods Receipt increases stock and posts Dr Inventory / Cr GR-IR at receipt cost.
- **BR-PUR-4** Purchase Invoice posts Dr GR-IR + Dr VAT Input / Cr AP and creates an AP open item.
- **BR-PUR-5** A Goods Receipt cannot exceed ordered quantity beyond a configurable tolerance.
- **BR-PUR-6** A Purchase Invoice is matched against PO and receipt (three-way match policy);
  price variance posts to a variance account.
- **BR-PUR-7** A Purchase Return removes goods from stock and adjusts AP/GR-IR accordingly.

## Approval & Process

- **BR-APR-1** A document requiring approval cannot be Posted until Approved.
- **BR-APR-2 (invariant)** The submitter cannot be the sole approver of the same document (SoD).
- **BR-APR-3** If no approval definition matches a submitted document, it is auto-approved.
- **BR-APR-4** Approve, Post, and Pay are distinct permissions.
- **BR-APR-5** Every document exposes a complete process timeline of its milestones.

## Security & Access

- **BR-SEC-1 (invariant)** A user can only access companies explicitly granted within their tenant.
- **BR-SEC-2 (invariant)** Cross-tenant data access is impossible (enforced by query filters +
  context).
- **BR-SEC-3** Permissions are configurable data; none are hardcoded.

> When a rule changes, update this file, the enforcing code's referenced `BR-` id, the relevant
> tests, and (if architectural) DECISIONS.md.
