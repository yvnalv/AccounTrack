# End-to-End Guide — testing & reading the full accounting flow

This is a practical, click-through walkthrough of Accountrack: how to drive a complete business
cycle — **master data → purchasing → sales → expenses → inventory** — and then **read the resulting
accounting** (journals, subledgers, and financial reports). Use it to smoke-test the software after a
deploy, to onboard, or simply to understand how a document turns into a set of balanced journals.

> Golden rule of the platform: **the General Ledger is the single source of truth.** You never write
> journals by hand for business documents — posting a document *generates* its journal automatically
> and atomically (double-entry, always balanced). Reports are derived from the GL, never from the
> transactional tables. See [ACCOUNTING_DESIGN.md](ACCOUNTING_DESIGN.md) and
> [POSTING_RULES.md](POSTING_RULES.md).

---

## 0. The mental model

```
          Master data                Transactions (documents)              Accounting (derived)
  ┌──────────────────────┐   ┌───────────────────────────────────┐   ┌─────────────────────────┐
  │ Chart of accounts    │   │ Purchasing: PO → Receipt → Bill →  │   │ Journal entries (GL)    │
  │ Posting rules        │──▶│            → Supplier payment      │──▶│ AR / AP subledgers      │
  │ Products / UoM / Cat │   │ Sales:  SO → Delivery → Invoice →  │   │ Inventory ledger        │
  │ Customers / Suppliers│   │         → Customer receipt         │   │ Trial Balance, P&L,     │
  │ Warehouses / Tax     │   │ Expenses: voucher (draft→post) /   │   │ Balance Sheet, Cash     │
  │ Expense categories   │   │           reverse                  │   │ Flow, VAT, GL, Aging    │
  └──────────────────────┘   └───────────────────────────────────┘   └─────────────────────────┘
```

Every posted document does three things in **one atomic transaction**: writes the document, posts a
**balanced GL journal**, and (where relevant) updates a **subledger** (AR/AP open items) or the
**inventory ledger** (moving-average cost). If any part fails, nothing is written.

---

## 1. Prerequisites — get the stack running

**Local development** (see [DEPLOYMENT.md](DEPLOYMENT.md) for the Docker and VPS paths):

1. PostgreSQL running, with an app database + role (dev default DB `Accountrack_Dev`).
2. API running on `http://localhost:5080` (Swagger at `/swagger`), with these on first boot:
   `Database__Initialize=true`, `Database__AutoMigrate=true`, `Seed__Enabled=true`.
3. SPA running on `http://localhost:5173` (it proxies `/api` → the API).

**Log in** with the seeded administrator (dev default `admin@accountrack.local` / `ChangeMe!123`;
configurable via `Seed__AdminEmail` / `Seed__AdminPassword`). The seed also creates one demo company
(PKP / VAT-registered, functional currency **IDR**), the **chart of accounts**, **posting rules**,
**PPN 11%**, and a base unit / category / warehouse.

> **PKP note:** the demo company is VAT-registered, so PPN 11% fields appear. If a company is not
> registered for VAT, the tax fields are hidden and transactions default to no PPN.

---

## 2. The chart of accounts you'll see

The seeded accounts (codes are stable; posting rules map events to them — never hardcode):

| Code | Account | Role |
|------|---------|------|
| 1000 / 1010 | Cash / Bank | Cash & bank |
| 1100 | Accounts Receivable | **AR control** (reconciles to the AR subledger) |
| 1200 | Inventory | **Inventory control** (reconciles to the inventory ledger) |
| 1300 | VAT Input (PPN Masukan) | Creditable input VAT on purchases/expenses |
| 2100 | Accounts Payable | **AP control** (reconciles to the AP subledger) |
| 2150 | Goods Received / Invoice Received | **GR/IR clearing** (decouples receipt from bill) |
| 2300 | VAT Output (PPN Keluaran) | Output VAT on sales |
| 3900 | Retained Earnings | Equity (year-end close target) |
| 4000 | Sales Revenue | Income |
| 5000 | Cost of Goods Sold | COGS |
| 6000–6900 | Electricity, Transport, Rent, Supplies, Salaries, Other | Operating expenses |

---

## 3. Document → journal cheat-sheet

Keep this next to you while testing — it's exactly what each posted document produces:

| Document | Debit | Credit | Also updates |
|----------|-------|--------|--------------|
| **Goods receipt** | Inventory (1200) | GR/IR (2150) | Inventory ledger (+qty at cost) |
| **Purchase invoice (bill)** | GR/IR (2150) + VAT Input (1300) | AP (2100) | AP open item (+) |
| **Supplier payment** | AP (2100) | Cash/Bank | AP open item (settled) |
| **Delivery (shipment)** | COGS (5000) | Inventory (1200) | Inventory ledger (−qty at moving-avg) |
| **Sales invoice** | AR (1100) | Revenue (4000) + VAT Output (2300) | AR open item (+) |
| **Customer receipt** | Cash/Bank | AR (1100) | AR open item (settled) |
| **Expense — paid** | Expense (6xxx) [+ VAT Input] | Cash/Bank | — |
| **Expense — on account** | Expense (6xxx) [+ VAT Input] | AP (2100) | AP open item (+) |
| **Expense — reverse** | *mirror of the original* | *mirror of the original* | AP open item settled (if on account) |

Notice how **GR/IR** lets you receive goods before the supplier's bill arrives (receipt debits
inventory and credits GR/IR; the later bill clears GR/IR and raises AP), and how **AR/AP control
accounts** always stay equal to the sum of their open items.

---

## 4. Walkthrough

You can do everything below either in the **web app** (recommended for a real feel) or via the
**API** (Swagger / curl — handy for scripted smoke tests). API routes are under `/api/v1`.

### 4.1 Master data (once)

Under **Master data**, create the reference records you'll transact on:

1. **Units of measure** (e.g. `PCS`), **Product categories** (e.g. `ELEC`), **Warehouses** (e.g. `MAIN-WH`).
2. **Products** — mark them *stock-tracked*, *sold*, and/or *purchased*. Stock-tracked products flow
   through the inventory ledger.
3. **Customers** and **Suppliers** (payment terms, tax id optional).
4. **Tax codes** — `PPN11` (11%) is seeded.
5. Under **Expenses → Categories**, confirm the seeded operating-expense categories (each maps to an
   expense GL account via a posting-rule key).

*No journals are posted for master data* — it's reference data. Deactivate (never hard-delete)
records you no longer use.

### 4.2 Purchasing cycle (buy & stock)

Goal: bring stock in and record what you owe. Under **Purchasing**:

1. **Create a Purchase Order** (supplier, warehouse, lines with qty + unit price + PPN). Save → **Submit**.
   With no approval rule configured it **auto-approves** (status `Approved`). *No GL yet — a PO is a
   commitment, not a posting.*
2. **Goods receipt** (receive the ordered quantities). → **Journal:** *Dr Inventory / Cr GR/IR* at cost;
   the **inventory ledger** increases (moving-average cost is set here).
3. **Purchase invoice** (the supplier's bill). → **Journal:** *Dr GR/IR + Dr VAT Input / Cr AP*; an
   **AP open item** opens for the supplier. GR/IR nets to zero once received == billed.
4. **Supplier payment** (Purchasing → Pay supplier): choose the cash/bank account and allocate to the
   open item. → **Journal:** *Dr AP / Cr Cash-Bank*; the AP open item is settled.

Leave one bill **unpaid** to see it later in **AP aging** / open items.

### 4.3 Sales cycle (sell & ship)

Goal: ship stock, bill the customer, collect. Make sure the product has stock first (from 4.2).
Under **Sales**:

1. **Create a Sales Order** (customer, warehouse, lines). Save → **Submit** (auto-approves → `Approved`).
   *No GL yet.*
2. **Delivery** (ship the goods). → **Journal:** *Dr COGS / Cr Inventory* at **moving-average** cost;
   the inventory ledger decreases.
3. **Sales invoice** (bill the customer). → **Journal:** *Dr AR / Cr Revenue + Cr VAT Output*; an
   **AR open item** opens.
4. **Customer receipt** (Sales → Receive payment): choose the cash/bank account, allocate to the open
   item. → **Journal:** *Dr Cash-Bank / Cr AR*; the AR open item is settled.

Leave one invoice **unpaid** to populate **AR aging**. (A **sales return** from a posted invoice
raises a credit note that reverses revenue/VAT and returns goods to stock.)

### 4.4 Expenses (operating costs)

Under **Expenses** (this module now mirrors Sales/Purchasing — see [BUSINESS_RULES.md](BUSINESS_RULES.md)
BR-EXP-*):

- **Quick path — "Save & post":** record + post in one click. → **Journal:** *Dr Expense (+ Dr VAT
  Input) / Cr Cash-Bank* (paid) or *Cr AP* (on account, opens an AP open item).
- **Draft path — "Save as draft":** creates a `Draft` you can **Edit**, then **Submit** (posts), or
  **Cancel** (discard). Nothing hits the GL until you submit.
- **Reverse** (on a posted voucher): posts a **mirror journal** that offsets the original and marks the
  voucher `Reversed`; the original journal is kept for audit. An **on-account** voucher can be reversed
  only while its AP open item is **fully unpaid** (settle/unwind the payment first otherwise).

Posted vouchers are **immutable** — you correct them by reversal, not by editing (the auditable,
industry-standard pattern).

### 4.5 Inventory

Under **Inventory**:

- **Stock on hand** — live quantity and value per product/warehouse, derived from the inventory ledger.
- **Stock card** — the per-product movement history (receipts +, shipments −).
- **Valuation** — total stock value; it must equal the **Inventory control account (1200)** balance.
- Costing is **moving average** (a receipt at a new price re-averages the unit cost; shipments issue at
  the current average). See [INVENTORY_DESIGN.md](INVENTORY_DESIGN.md).

---

## 5. Reading the final accounting

Now the point of it all — go to **Accounting** and read the derived output. Everything below is
computed from the GL journals your documents posted.

- **Trial Balance** — every account with its debit/credit balance. **Total debits must equal total
  credits.** This is your first integrity check: if it balances, double-entry held across every posting.
- **Profit & Loss** — Revenue (4000) − COGS (5000) − Operating expenses (6xxx) = Net profit for the
  period. Sales invoices feed revenue; deliveries feed COGS; expense vouchers feed opex.
- **Balance Sheet** — Assets (Cash/Bank, AR, Inventory, VAT Input) = Liabilities (AP, VAT Output) +
  Equity (Retained earnings + current-period result). The accounting equation always holds.
- **Cash Flow** — cash movement over the period (payments in/out).
- **VAT report (PPN)** — VAT Output (from sales) vs VAT Input (from purchases/expenses); the difference
  is your PPN payable/creditable for the period.
- **General Ledger** — drill into a single account to see every line that hit it, with the source
  document. Great for "why is this balance what it is?".
- **AR / AP aging & open items** — who owes you / whom you owe, bucketed by age. The open-items total
  for AR **must equal** the AR control account (1100); AP open items **must equal** AP control (2100).
- **Dashboard** — cash & bank, AR/AP, overdue, revenue vs expense this month, inventory value, top
  receivables/payables — a one-glance health view.

### Interpreting a full cycle

After running §4.2–§4.4 with, say, one fully-paid sale and one unpaid sale:

- **Cash/Bank** moved by (customer receipts − supplier/expense payments).
- **AR (1100)** shows the one unpaid invoice; it matches AR open items and AR aging.
- **Inventory (1200)** = remaining stock value = inventory valuation report.
- **P&L** shows revenue and COGS from the delivered/invoiced sale, plus any expenses.
- **VAT report** shows Output (from the sales invoice) and Input (from the bill/expenses).

---

## 6. Integrity checks (do these after testing)

A healthy books satisfies **all** of these — they're the quickest way to prove a deploy is sound:

1. **Trial balance balances** — total debits == total credits.
2. **AR subledger == AR control (1100)** — sum of AR open items equals the account balance.
3. **AP subledger == AP control (2100)** — same for payables.
4. **Inventory ledger == Inventory control (1200)** — valuation report equals the account balance.
5. **A post + its reversal nets to zero** on every account (test with an expense voucher: post, then
   Reverse, and confirm the Trial Balance per-account balances return to where they were).

---

## 7. Fast path — scripted / API smoke test

For a repeatable check you can drive the same flow over the API. Authenticate, then call the
document endpoints in order; each returns the created id you feed into the next step:

```
POST /api/v1/auth/login                         → { data.accessToken }   (send as: Authorization: Bearer <token>)

# Purchasing
POST /api/v1/purchase-orders                     → poId
POST /api/v1/purchase-orders/{poId}/submit
POST /api/v1/purchase-orders/{poId}/goods-receipts
POST /api/v1/purchase-orders/{poId}/invoices
GET  /api/v1/ap/open-items?partyId={supplierId}  → open item id
POST /api/v1/supplier-payments

# Sales
POST /api/v1/sales-orders                         → soId
POST /api/v1/sales-orders/{soId}/submit
POST /api/v1/sales-orders/{soId}/deliveries
POST /api/v1/sales-orders/{soId}/invoices
GET  /api/v1/ar/open-items?partyId={customerId}   → open item id
POST /api/v1/customer-payments

# Expenses (draft workflow)
POST /api/v1/expense-vouchers/draft               → id
PUT  /api/v1/expense-vouchers/{id}                (edit)
POST /api/v1/expense-vouchers/{id}/submit         (posts)
POST /api/v1/expense-vouchers/{id}/reverse        (reverses)

# Read the results
GET  /api/v1/reports/trial-balance?fromDate=YYYY-01-01&toDate=YYYY-12-31
GET  /api/v1/reports/profit-loss?...   /balance-sheet?...   /vat?...   /general-ledger?accountId=...
GET  /api/v1/ar/aging     /api/v1/ap/aging     /api/v1/dashboard/summary
```

The line-quantity endpoints (deliveries/receipts/invoices) reference the **order line ids** returned
by `GET /api/v1/{sales-orders|purchase-orders}/{id}` — fetch the order after submit to read its line ids.

---

## 8. Common gotchas

- **"No stock to deliver"** — receive/purchase the product before selling it (a stock-tracked sale
  needs on-hand quantity for COGS).
- **Nothing posts on PO/SO submit** — correct: only *receipt/delivery/invoice/payment* post to the GL.
  Submit just runs approval (auto-approves when no rule matches).
- **No PPN fields** — the company isn't VAT-registered (PKP). Toggle it in **Settings → Company** if it
  should be.
- **"No posting into a closed period"** — the fiscal period is closed/locked; post into an open period
  (or reopen with the right permission).
- **Can't reverse an expense** — only a **Posted** voucher can be reversed; an on-account one must be
  fully unpaid first.
- **Buttons missing** — actions are permission-gated (`*.Create/Edit/Cancel/Post`); check the user's role.

---

## Related docs

[ACCOUNTING_DESIGN.md](ACCOUNTING_DESIGN.md) · [POSTING_RULES.md](POSTING_RULES.md) ·
[INVENTORY_DESIGN.md](INVENTORY_DESIGN.md) · [BUSINESS_RULES.md](BUSINESS_RULES.md) ·
[WORKFLOW_APPROVAL.md](WORKFLOW_APPROVAL.md) · [API_SPEC.md](API_SPEC.md) · [DEPLOYMENT.md](DEPLOYMENT.md)
