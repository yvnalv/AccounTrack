# GLOSSARY.md — Ubiquitous Language

Shared vocabulary for Accountrack. Use these exact terms in code, APIs, UI labels, and docs.
When a term has an Indonesian UI translation it is noted; the canonical code/identifier is
always the English term.

## Platform & Tenancy

- **Tenant** — The subscription owner / organization. The hard isolation boundary. A tenant
  owns one or more companies. (`TenantId`)
- **Company** — A legal entity / operational business unit within a tenant. The accounting and
  inventory boundary; books are kept per company. (`CompanyId`)
- **Tenant Context** — The ambient, server-resolved `{TenantId, current CompanyId, granted
  companies}` derived from the authenticated principal; drives global query filters.
- **Module** — A vertically-sliced bounded context (Identity, Sales, Inventory, …) layered
  Domain/Application/Infrastructure/API.
- **Integration Event** — A message published by one module and consumed by others to
  coordinate across boundaries (see INTEGRATION_EVENTS.md).
- **Outbox** — A table where integration events are persisted in the same transaction as the
  source change, then dispatched reliably.
- **Shared Kernel** — Cross-module building blocks (Money, base entity, audit fields, domain
  event base, soft-delete) shared by all modules.

## Accounting

- **Chart of Accounts (CoA)** — The structured list of GL accounts for a company.
- **Account** — A GL account with a type (Asset/Liability/Equity/Revenue/Expense), a normal
  balance (Debit/Credit), and an optional parent (hierarchy).
- **Control Account** — A GL account whose balance is the sum of a subledger (e.g. AR control,
  AP control). Not posted to directly except through the subledger.
- **Journal Entry** — A balanced accounting transaction: a header plus ≥2 **Journal Lines**
  where Σ debits = Σ credits.
- **Posting** — Committing a journal entry to the GL. Posted entries are immutable.
- **Reversal / Reversing Entry** — A journal that negates a previously posted entry; the only
  way to correct posted books.
- **General Ledger (GL)** — The complete set of posted journal lines; the source of truth for
  all financial reporting.
- **Subledger** — A detailed ledger (AR, AP, Inventory) that reconciles to a GL control account.
- **Open Item** — An unsettled receivable or payable (e.g. an unpaid invoice) tracked in the
  AR/AP subledger until allocated/paid.
- **Allocation** — Matching a payment (or credit note) against one or more open items.
- **Fiscal Year / Period** — The accounting calendar. Periods are **Open**, **Closed**, or
  **Locked**; posting is allowed only into Open periods.
- **Period Close** — The process that finalizes a period and writes balance snapshots.
- **Trial Balance** — A report listing every account's debit/credit balance; must balance.
- **Posting Rule / Account Determination** — Configuration mapping a business event to the GL
  accounts it should post to (see POSTING_RULES.md).
- **GR/IR (Goods Received / Invoice Received)** — A clearing account bridging the gap between
  receiving goods and receiving the supplier invoice.
- **COGS (Cost of Goods Sold)** — The inventory cost expensed when goods are shipped/sold.
- **VAT Output / PPN Keluaran** — Tax collected on sales (a liability).
- **VAT Input / PPN Masukan** — Tax paid on purchases (an asset / claimable).
- **Functional Currency** — The single base currency in which a company keeps its books (e.g.
  IDR).
- **Money** — A value object: an amount plus an explicit currency code.

## Inventory

- **Inventory Transaction** — An immutable ledger record of a stock movement (receipt, issue,
  adjustment, transfer leg, production consume/receive). Source of truth for quantity & value.
- **On-Hand** — Quantity currently in stock for a (Company, Warehouse, Product), derived from
  the ledger.
- **Moving Average Cost** — The running weighted-average unit cost maintained per (Company,
  Warehouse, Product); updated on each receipt, consumed on each issue.
- **Cost Bucket** — The (Company × Warehouse × Product) granularity at which average cost is
  maintained.
- **Cost Layer / Lot** — (Future, FIFO) a discrete receipt quantity at a specific unit cost.
- **Stock Adjustment** — A manual increase/decrease of stock (with reason), posting a variance
  journal.
- **Stock Transfer** — Movement of stock between warehouses (an out-leg and an in-leg carrying
  cost).
- **Stock Opname** — A physical stock count reconciled against book quantity (variance →
  adjustment).
- **Unit of Measure (UoM)** — The unit a product is tracked/traded in; conversions may apply.

## Sales & Purchasing (Documents)

- **Quotation** — A non-binding price offer to a customer.
- **Sales Order (SO)** — A confirmed customer order.
- **Delivery Order (DO) / Shipment** — The document recording goods shipped to a customer;
  triggers the stock issue and COGS.
- **Sales Invoice** — The document billing the customer; triggers AR + revenue (+ VAT Output).
- **Customer Payment / Receipt** — Cash received from a customer, allocated to invoices.
- **Sales Return / Credit Note** — Goods/credit returned by a customer.
- **Purchase Request (PR)** — An internal request to buy.
- **Purchase Order (PO)** — A confirmed order to a supplier.
- **Goods Receipt (GR)** — The document recording goods received; triggers stock receipt + GR/IR.
- **Purchase Invoice / Bill** — The supplier's bill; triggers AP (+ VAT Input), clears GR/IR.
- **Supplier Payment** — Cash paid to a supplier, allocated to bills.
- **Purchase Return / Debit Note** — Goods/credit returned to a supplier.
- **Three-Way Match** — Reconciling PO ↔ Goods Receipt ↔ Purchase Invoice before payment.

## Process & Security

- **Document Status** — The lifecycle state of a business document (e.g. Draft, Submitted,
  Approved, Posted, Completed, Cancelled).
- **Process Tracker** — The per-document timeline of business lifecycle milestones.
- **Audit Log** — The immutable record of field-level changes (who/when/before/after).
- **Approval Workflow** — Rules that route a document for one or more approvals before it can
  proceed.
- **Permission** — A named, assignable capability (e.g. `Accounting.Post`).
- **Role** — A named collection of permissions assigned to users.
- **Segregation of Duties (SoD)** — Control ensuring no single user both creates and
  approves/posts the same financial document.

## Distinction: Audit Log vs Process Tracker

- **Audit Log** answers *"who changed which field, from what to what, and when"* — technical,
  field-level, for compliance/forensics.
- **Process Tracker** answers *"where is this document in its business journey"* — milestone-
  level, user-facing (Created → Submitted → Approved → Posted → Completed).
