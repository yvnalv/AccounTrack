# ACCOUNTING_DESIGN.md

The accounting engine. Accounting integrity is non-negotiable (Core Principle 6). The General
Ledger is the single source of truth (ADR-0008). Account determination is in
[POSTING_RULES.md](POSTING_RULES.md).

## 1. Principles

1. **Double-entry, always balanced** (ADR-0009): every posted `JournalEntry` satisfies
   Σ debits = Σ credits, enforced as a domain invariant before commit.
2. **Automatic posting**: users never hand-write journals for sales, purchases, payments,
   inventory moves, or tax. Business events post journals via posting rules.
3. **GL is truth** (ADR-0008): all financial reports derive from journal lines / snapshots,
   never from transactional tables.
4. **Immutability / reversal-only** (ADR-0009): a posted entry is never edited or deleted; you
   reverse it and post a correction.
5. **Periods control time** (ADR-0010): postings only into Open periods.
6. **Subledgers reconcile to control accounts** (ADR-0011) for AR/AP (and Inventory).

## 2. Chart of Accounts (CoA)

`Accounts` carry:
- `Type` ∈ {Asset, Liability, Equity, Revenue, Expense}
- `NormalBalance` ∈ {Debit, Credit} (Asset/Expense = Debit; Liability/Equity/Revenue = Credit)
- `ParentAccountId` for hierarchy (rollups for reports)
- `IsControlAccount` + `ControlType` ∈ {AR, AP, Inventory, null}
- `IsSystem` (cannot be deleted; required by posting rules) and `IsActive`

A **default CoA template** (Indonesian SMB-friendly) is seeded per company and editable. System
accounts required by posting rules (AR control, AP control, Inventory, GR/IR, VAT Output, VAT
Input, COGS, Sales Revenue, Cash/Bank, Rounding, Inventory Variance, Retained Earnings) are
seeded and flagged `IsSystem`. The template also seeds **equity and financing** accounts covering
both sole-proprietor and PT structures — Owner's Capital, Additional Paid-in Capital, Owner's
Drawings, Share Capital, Dividends Declared, Opening Balance Equity, Bank Loan Payable, Dividends
Payable (ADR-0040). The chart and posting-rule seeders are **per-item idempotent**, so new default
accounts backfill onto companies provisioned before they existed.

Posting is to **postable (leaf) accounts**; parent/rollup accounts are not posted to directly.

## 3. Journal Model

```
JournalEntry
├── EntryNo            (unique per company, gapless sequence; NULL until posted — a manual journal
│                       awaiting approval has no number yet, ADR-0040)
├── Date               (must fall in an Open period)
├── PeriodId
├── Source             (SalesInvoice | PurchaseInvoice | Payment | Shipment | GoodsReceipt |
│                       StockAdjustment | Manual | CapitalContribution | OwnerDrawing | BankTransfer |
│                       MoneyReceipt | MoneyPayment | LoanReceipt | LoanRepayment | …)
├── SourceDocumentId   (drill-down link)
├── Description
├── Status             (Draft | PendingApproval | Posted | Reversed | Rejected)
├── ApprovalRequestId? (set while a manual journal is routed through approval — ADR-0040)
├── ReversesEntryId?   / ReversedByEntryId?
└── Lines[]            (≥ 2)
       ├── AccountId   (postable)
       ├── Debit  : Money
       ├── Credit : Money       (a line is debit XOR credit)
       ├── Description
       ├── SubledgerRef?  (CustomerId / SupplierId for control-account lines)
       └── Dimensions?    (reserved: cost center, project — future)
```

Invariants enforced before `Post`:
- ≥ 2 lines; each line is debit-only or credit-only, non-negative.
- Σ debits = Σ credits (to the money scale; rounding handled per §7).
- Period is Open; currency is the company functional currency (ADR-0013).
- Control-account lines carry a `SubledgerRef`.
- Idempotent by (Source, SourceDocumentId, eventType) — never double-post (ADR-0021).

## 4. Posting Flow

1. A business event fires (e.g. `SalesInvoicePosted`) with amounts.
2. The **posting-rule resolver** maps the event + selectors (product category, tax code,
   bank, warehouse) to accounts (POSTING_RULES.md).
3. The Accounting service builds balanced lines, validates invariants, writes the entry +
   subledger open items in **one transaction** (atomic flow, INTEGRATION_EVENTS.md §2).
4. `JournalPosted` is emitted (eventual) for reporting/process-tracker/audit.

## 5. Subledgers (AR / AP) — ADR-0011

- A Sales Invoice creates an **AR open item** (CustomerId, amount, due date) and posts
  Dr AR control / Cr Revenue / Cr VAT Output.
- A Purchase Invoice creates an **AP open item** and posts Dr GR/IR + Dr VAT Input / Cr AP control.
- A payment **allocates** to one or more open items (full or partial) via `PaymentAllocations`;
  posts Dr/Cr Cash/Bank and Dr/Cr the control account. Overpayments / unallocated cash are held
  on an advances account.
- **Reconciliation invariant:** Σ outstanding open items per customer = the customer's portion
  of the AR control balance; same for AP. A reconciliation report flags drift.
- **Aging** (Current / 30 / 60 / 90+) derives from open items and due dates.

## 6. Fiscal Periods — ADR-0010

- `FiscalYears` (per company, start month configurable) → `FiscalPeriods` (monthly).
- States: **Open** (posting allowed) → **Closed** (no posting; reopenable with
  `Accounting.PeriodReopen` permission, audited) → **Locked** (hard, e.g. after statutory
  filing; not reopenable without admin override).
- **Period close** writes a **`PeriodBalances`** snapshot — each account's cumulative debit/credit as
  of the period end — so reports can show month-end positions and opening balances without re-summing
  the whole ledger. The snapshot is **rebuildable** from the GL at any time (`POST
  /fiscal-periods/{id}/balances/rebuild`) and is dropped when a period is reopened, so it can never
  drift from the ledger (ADR-0022; CHG-0079). Year-end close rolls P&L (Revenue − Expense) into
  **Retained Earnings** and opens the next year.

## 7. Money, Rounding & Currency

- Money = `decimal(19,4)` + currency code; **never floating point** (ADR-0013).
- Single functional currency per company; `TransactionCurrency`/`ExchangeRate`/functional-amount
  columns reserved for future multi-currency (no FX logic in MVP).
- **Rounding:** line tax = round(base × rate) using banker's/half-up per a documented company
  policy (default: half-up to 2 decimals for IDR which has 0 minor units in practice — store as
  whole rupiah where configured). Any residual rounding difference on a document posts to a
  dedicated **Rounding** account so the journal stays balanced.

## 8. Tax (PPN) — ADR-0012

- `TaxCodes` (`PPN11`, rate 11%, exclusive default) referenced on invoice lines.
- Sales: tax amount → **VAT Output** (liability). Purchases: tax amount → **VAT Input** (asset).
- A simple **VAT report** (Output − Input = payable/claimable) is derived from these accounts.
- Designed for multi-rate, inclusive pricing, and withholding (PPh) in later phases without
  schema change.

## 9. Standard Postings (summary; full matrix in POSTING_RULES.md)

| Event | Debit | Credit |
|---|---|---|
| Sales Invoice | AR control (gross) | Revenue (net) + VAT Output (tax) |
| Goods Shipment (COGS) | COGS | Inventory |
| Customer Payment | Cash/Bank | AR control |
| Sales Return | Revenue + VAT Output; Inventory | AR control; COGS |
| Goods Receipt | Inventory | GR/IR clearing |
| Purchase Invoice | GR/IR clearing + VAT Input | AP control |
| Supplier Payment | AP control | Cash/Bank |
| Purchase Return | AP control; GR/IR or Inventory | Inventory; … |
| Stock Adjustment (increase) | Inventory | Inventory Variance |
| Stock Adjustment (decrease) | Inventory Variance | Inventory |
| Year-end close | Revenue (net debit) | Retained Earnings (or vice-versa) |

## 10. Financial Reports

Derived from GL + snapshots (ADR-0022), never transactional tables:
- **Trial Balance** — all accounts' debit/credit; must balance.
- **Profit & Loss** — Revenue − Expense for a period range.
- **Balance Sheet** — Assets = Liabilities + Equity as of a date.
- **Cash Flow** — indirect method (CHG-0056): net income + period movement of every non-cash
  balance-sheet account (non-cash assets & operating liabilities → Operating; equity → Financing);
  cash/bank (10xx) is the reconciling target, so the three sections always sum to the actual change
  in cash. Investing (non-current assets) & financing-debt detail refine when those accounts exist.
- **AR/AP Aging** — from open items.
- **General Ledger / Account Detail** — line-level with drill-down to source documents. Implemented
  (CHG-0058): posted lines over a period, optional single-account filter, per-account opening balance
  carried into a running balance + closing; signed debit − credit, reconciles to the Trial Balance.
- **VAT report** — Output vs Input.

All reports are **company-scoped** and permission-gated; consolidated multi-company reporting
(within a tenant) is a later enhancement.

## 11. Correctness Risks & Controls

| Risk | Control |
|---|---|
| Unbalanced journal | Domain invariant + rounding account (§7) |
| Double posting on retry | Idempotency keys (ADR-0021) |
| Back-dated tampering | Period lock (ADR-0010/0017) |
| Subledger ↔ GL drift | Reconciliation report (§5) |
| Wrong account chosen | Posting rules validated to resolve to active accounts (POSTING_RULES.md) |
| Float rounding errors | Decimal money only (§7) |
| Report ≠ ledger | Reports derive from GL/snapshots, snapshots rebuildable (ADR-0022) |

## 12. Test Coverage (high priority — TESTING.md)
Balanced-journal invariant; each posting rule produces the expected lines; reversal restores
balances; period-close snapshot equals recomputed GL; AR/AP allocation and aging; VAT totals;
idempotent re-posting; rounding edge cases; the journal approval lifecycle (submit → approve/reject)
and that held/rejected journals stay out of the GL.

## 13. Manual Journals & Cash & Bank flows — ADR-0040

For financial events that are **not** a sale, purchase, inventory move, or expense — capital, owner
drawings, cash/bank transfers, loans, opening balances, accruals/depreciation:

- **General Journal.** A manual, balanced double-entry (`Source = Manual`), created via the general
  journal form and exposed in a **register** (`GET /journal-entries`). Corrections are reversal-only.
- **Guided Cash & Bank flows.** Opinionated wrappers (Capital contribution, Owner drawing, Bank/cash
  transfer, Receive money, Spend money, Loan receipt, Loan repayment) that assemble a balanced journal
  through the **posting-rule engine** (accounts never hardcoded) so a non-accountant never sees debits
  and credits. Each carries its own `Source` for a clear register/audit trail.
- **Approval lifecycle.** Every manual journal / flow is **submitted to the Approval Workflow** exactly
  like an Expense voucher (ADR-0030). No matching definition → auto-approved and posted now; a matching
  definition → held as `PendingApproval` (persisted, but **excluded from the GL**) and posted by the
  Accounting approval consumer on approval, or marked `Rejected` on rejection. The submitter cannot
  self-approve (segregation of duties, BR-APR-2). Admins enforce approval by configuring a threshold
  definition for the `ManualJournal` document type.
- **GL isolation.** A held journal has no gapless `EntryNo` (assigned only at posting), and every
  balance/report query includes only `Status ∈ {Posted, Reversed}` — so `PendingApproval` and
  `Rejected` entries never affect the trial balance, GL, or financial statements. `Reversed` stays
  included because its posted lines are offset by the reversal.

### Standard postings (guided flows)

| Flow | Debit | Credit |
|---|---|---|
| Capital contribution | Cash/Bank | Owner's Capital / Share Capital |
| Owner drawing | Owner's Drawings | Cash/Bank |
| Cash/bank transfer | Destination Cash/Bank | Source Cash/Bank |
| Receive money | Cash/Bank | chosen income/source |
| Spend money | chosen expense/asset | Cash/Bank |
| Loan receipt | Cash/Bank | Loan Payable |
| Loan repayment | Loan Payable (+ Interest Expense) | Cash/Bank |

Equity and loan movements classify into the **Financing** section of the cash-flow statement.
