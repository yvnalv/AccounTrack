# Accountrack Changelog

## [2026-06-16 14:15:17 UTC]

CHG-0029 — Frontend: drive order-to-cash from the Sales Order detail (deliver + invoice)

- The Sales Order detail now drives the order-to-cash flow end-to-end from the UI:
  - **Deliver outstanding** (when Approved/PartiallyDelivered) → posts a delivery for all outstanding
    line quantities (stock issue + Dr COGS/Cr Inventory, atomic).
  - **Create invoice** (when delivered-but-uninvoiced) → posts a sales invoice for the delivered,
    uninvoiced quantities (Dr AR / Cr Revenue+VAT + AR open item).
  - Line table gains **Delivered / Invoiced** columns; **Deliveries** and **Invoices** document lists
    (with amount + a "Posted" badge once the GL journal exists) render below.
- **Backend:** `SalesOrderLineDto` now exposes `InvoicedQuantity` (so the UI knows what's left to
  bill); `lib/sales.ts` gains `deliveries`/`createDelivery`/`invoices`/`createInvoice`.
- **Verified:** frontend `npm run build` green; end-to-end smoke through the API — new SO → submit →
  deliver-all (DO posted) → invoice-all (SI 2.664 posted), detail reflects delivered/invoiced and
  lists both documents.
- Customer Payment (receipt + AR allocation) UI is the next slice. Note: hit and cleared the
  stale-host-bin trap (rebuild the host after a contract change before `dotnet run --no-build`).

---

## [2026-06-16 13:57:25 UTC]

CHG-0028 — Frontend: Sales Orders (list + detail + create) + reusable table/form kit

- First real CRUD module screen, in the dense-table register. Reusable building blocks every module
  will share:
  - **`DataTable`** (typed columns, named cell slots, numeric/tabular alignment, loading/empty,
    clickable rows), **`StatusBadge`** (semantic tone mapping for document statuses via `color-mix`),
    and form controls **`FormField` / `AppInput` / `AppSelect`** + a shared `.field-input` style.
- **Sales Orders list** (`/sales`) — DataTable of orders (number, customer name, date, status badge,
  total) with a "New" action; rows open the detail.
- **Sales Order detail** (`/sales/:id`) — header + status, line-items table (product names, qty,
  unit price, tax %, delivered, line total), totals, notes; **Submit for approval** when Draft.
- **Sales Order create** (`/sales/new`) — document form: customer/warehouse/date header + dynamic
  line-items editor (product, qty, unit price, tax %, live line/totals, add/remove) → `POST` →
  redirects to the new order's detail.
- API/types: `lib/sales.ts`, `lib/masterData.ts` (customers/warehouses/products + id→name maps),
  typed DTOs. Sidebar active-state now highlights on nested routes (prefix match; dashboard stays exact).
- **Verified:** `npm run build` green; dev smoke through the Vite proxy — list (3 orders), master-data
  loads, create (SO/202606/00004, grand 333) and detail all work end-to-end against the live API.
- See [docs/frontend/FRONTEND_ARCHITECTURE.md](docs/frontend/FRONTEND_ARCHITECTURE.md).

---

## [2026-06-16 13:34:15 UTC]

CHG-0027 — Frontend: Vue 3 scaffold — app shell, theme toggle, login, dashboard

- Scaffolded the **`frontend/`** web client (Vite 6 + Vue 3 + TS strict + Pinia + Vue Router 4 +
  Tailwind 3 + vue-i18n + Apache ECharts + axios + Lucide), per the locked design language.
- **Design tokens** (`tokens.css`) as CSS custom properties for **light + dark**, mapped into the
  Tailwind theme; the **theme toggle** flips `data-theme` on `<html>` (persisted; defaults to
  `prefers-color-scheme`). Brand teal `#007E6E`, Plus Jakarta Sans, tabular figures, id-ID money.
- **App shell** — dark sidebar (brand + ⌘K search placeholder + grouped nav with teal active pill),
  top bar (greeting/title, theme toggle, notifications, user + sign-out), routed content area.
- **Login** wired to the real API (`POST /api/v1/auth/login`) via a Vite dev proxy to the .NET host;
  Pinia auth store (token + user in `localStorage`, permission checks); router guard with `?redirect`;
  401 → clear session + back to login.
- **Dashboard** consuming `GET /api/v1/dashboard/summary`: KPI tiles (cash, AR, AP w/ overdue hints,
  net profit) + a revenue-vs-expense ECharts bar (themed from the CSS vars, black tooltip). Other nav
  targets are a "coming soon" placeholder.
- **Verified:** `npm run build` (vue-tsc typecheck + vite build) green; dev smoke — SPA shell serves
  on :5173, and login + dashboard work end-to-end **through the Vite proxy** to the live API
  (cash 1.001.110, AR 600.000, AP 1.110, month net 400.973,98).
- Docs: [docs/frontend/FRONTEND_ARCHITECTURE.md](docs/frontend/FRONTEND_ARCHITECTURE.md) (+ README/
  design-language updates). Notes: refresh-token rotation, i18n `id` locale, and self-hosted font are
  TODOs.

---

## [2026-06-16 13:17:11 UTC]

CHG-0026 — Accounting: dashboard summary read endpoint

- Added **`GET /api/v1/dashboard/summary`** (Accounting.View) — finance KPIs for the home dashboard,
  derived from the GL + AR/AP subledgers (never transactional tables): cash & bank balance, AR/AP
  outstanding, AR/AP overdue, and this month's revenue / expense / net profit, in the company's
  functional currency.
- `GetDashboardSummaryQuery` composes the existing read store (trial balance for cash + month P&L)
  and subledger repository (open items, overdue by due date vs. today). Cash & bank = GL balance of
  asset accounts in the cash/bank code band (10xx) by convention.
- **Tests:** 1 new (aggregates cash/AR/AP/overdue/month-P&L). Full suite now 186, green. Verified
  end-to-end against the dev data.
- Prerequisite for the upcoming frontend dashboard. See [docs/frontend/](docs/frontend/).

---

## [2026-06-16 11:45:26 UTC]

CHG-0025 — Sales: Customer Payment (allocate AR, Dr Cash-Bank / Cr AR)

- Added **Customer Payment** (Sales slice 2, final piece): record a receipt from a customer and
  allocate it to AR open items. In one cross-module atomic transaction it posts **Dr Cash-Bank /
  Cr AR control** (AR account resolved by posting rules and carrying the customer; the cash/bank GL
  account is chosen on the payment), allocates each AR open item via the subledger (settling /
  partially settling it), and records the payment linked to its journal.
- **Api:** `POST /api/v1/customer-payments`, `GET /api/v1/customer-payments/{id}`,
  `GET /api/v1/customer-payments?customerId=` (Sales.Post / Sales.View).
- **Persistence:** EF migration `AddCustomerPayments` (CustomerPayments / CustomerPaymentAllocations
  / sequence).
- **Tests:** 3 new (allocation total + zero guard; handler posts balanced Dr Cash/Cr AR and allocates
  each open item; subledger over-allocation fails the payment). Full suite now 185, green.
- **Verified end-to-end:** against the AR open item from the sales invoice (2,220) — partial receipt
  1,000 (Dr Cash 1,000 / Cr AR 1,000, item PartiallyPaid, outstanding 1,220) → receipt 1,220 (item
  Settled, outstanding 0); a further payment on the settled item was rejected.
- **Completes order-to-cash** (SO → Delivery → Sales Invoice → Customer Payment) and the MVP
  transactional backend: both procure-to-pay and order-to-cash run end to end with atomic
  cross-module posting and AR/AP subledger reconciliation. See [docs/STATUS.md](docs/STATUS.md).

---

## [2026-06-16 09:56:47 UTC]

CHG-0024 — Sales: Sales Invoice (AR/Revenue/VAT) + AR subledger

- Added **Sales Invoice** (Sales slice 2): bill a customer for goods delivered against a sales order.
  In one cross-module atomic transaction it posts **Dr AR control / Cr Revenue + Cr VAT Output**
  (accounts resolved by posting rules; AR line carries the customer as subledger party), opens an
  **AR subledger open item**, advances the SO's invoiced quantities, and records the invoice.
- **Three-way-match lite:** a line is invoiceable only up to *delivered-but-not-yet-invoiced*
  quantity (`UninvoicedDeliveredQuantity`), so revenue is recognised against goods actually shipped.
- **Api:** `POST /api/v1/sales-orders/{id}/invoices`, `GET .../invoices`,
  `GET /api/v1/sales-invoices/{id}` (Sales.Post / Sales.View).
- **Persistence:** EF migration `AddSalesInvoices` (SalesInvoices/Lines/sequence + `InvoicedQuantity`
  on SalesOrderLines).
- **Tests:** 4 new (invoice-quantity guard; handler posts a balanced Dr AR / Cr Revenue+VAT journal,
  opens the AR item, advances the SO; over-invoice guard; zero-tax omits the VAT line). Full suite
  now 182, green.
- **Verified end-to-end:** SO 4 @ 500 + PPN 11% → approved → delivered 4 → invoiced 4. Invoice net
  2,000 / VAT 220 / gross 2,220; balanced journal Dr AR 2,220 / Cr Revenue 2,000 + Cr VAT Output 220;
  AR open item 2,220 in the 1–30 aging bucket; over-invoice rejected.
- See [docs/POSTING_RULES.md](docs/POSTING_RULES.md), [docs/ACCOUNTING_DESIGN.md](docs/ACCOUNTING_DESIGN.md).

---

## [2026-06-16 09:28:19 UTC]

CHG-0023 — Sales: Delivery Order (stock issue + COGS) — cross-module atomic

- Added **Delivery Order** (Sales slice 2): ship goods against an approved sales order. In one
  cross-module atomic transaction (reusing the `ICrossModuleUnitOfWork`) it issues stock per line at
  moving-average cost (`IInventoryPosting.IssueAsync`, no negative stock), posts **Dr COGS / Cr
  Inventory** at the issue cost (accounts resolved by posting rules), advances the SO's delivery
  status (Approved → PartiallyDelivered → Delivered, per-line delivered/outstanding), and records
  the delivery order linked to its journal.
- **Domain:** `DeliveryOrder` + lines + sequence; `SalesOrderLine.Deliver` with received/outstanding
  guards (BR-SAL-2); over-delivery and non-approved guards.
- **Api:** `POST /api/v1/sales-orders/{id}/deliveries`, `GET .../deliveries`,
  `GET /api/v1/delivery-orders/{id}` (Sales.Post / Sales.View). SO line DTO now exposes
  `OutstandingQuantity` (parity with Purchasing).
- **Persistence:** EF migration `AddDeliveryOrders` (DeliveryOrders/Lines/sequence).
- **Tests:** 5 new (deliver part/all status, over-delivery; handler issues stock + posts balanced
  Dr COGS/Cr Inventory + advances SO; over-delivery and insufficient-stock guards). Full suite now 178, green.
- **Verified end-to-end:** seeded stock, SO 5 @ 300 + PPN 11% → approved → delivered 5; stock issued
  at moving-average 112.8205 → DO total 564.1025, balanced journal Dr COGS 564.1025 / Cr Inventory
  564.1025 (source Shipment), SO Delivered, on-hand decremented 39 → 34; a further delivery was rejected.
- Order-to-cash progress: SO → **Delivery (COGS)**. Next: Sales Invoice (AR/Revenue/VAT) + Customer
  Payment. See [docs/POSTING_RULES.md](docs/POSTING_RULES.md).

---

## [2026-06-16 09:12:30 UTC]

CHG-0022 — Sales module (slice 1): Sales Orders + approval integration

- Scaffolded the **Sales** module (Domain/Application/Infrastructure/Api, own `sales` EF schema +
  `InitialSales` migration, arch-fitness tests, solution + host wiring) — the order-to-cash side.
- **Sales Order** (slice 1): create a draft (customer + ship-from warehouse + lines with PPN), submit
  for approval; status advances via the Approval Workflow integration events (auto-approve when no
  rule matches, else PendingApproval → Approved/Rejected) — mirroring the Purchase Order flow and
  reusing `IApprovalService` + `ApprovalDecided`. Delivery (stock issue + COGS), invoicing (AR/
  Revenue/VAT) and customer payment are the next slices (line `DeliveredQuantity` reserved).
- Added `CustomerExistsAsync` to the `IMasterDataLookup` contract (+ implementation) for reference
  validation.
- The Sales DbContext binds to the shared cross-module connection and registers as an
  `ITransactionalDbContext` up front, so the upcoming atomic delivery/invoice slices need no DI change.
- **Api:** `GET /api/v1/sales-orders`, `GET /{id}`, `POST /` (Sales.Create), `POST /{id}/submit`
  (Sales.Create); reads gated by Sales.View.
- **Tests:** 13 new (10 Sales unit: totals, status transitions, create validation, submit/auto-approve,
  approval-event consumer; 3 arch-fitness). Full suite now 173, green.
- **Verified end-to-end:** created a customer, then SO 3 @ 250 + PPN 11% → SO/202606/00001 Draft
  (sub 750 / tax 82.5 / grand 832.5) → submit → Approved; an unknown-customer order was rejected.
- See [docs/MODULES.md](docs/MODULES.md).

---

## [2026-06-16 08:36:12 UTC]

CHG-0021 — Purchasing: Supplier Payment (allocate AP, Dr AP / Cr Cash-Bank)

- Added **Supplier Payment** (Purchasing slice 2, final piece): pay a supplier and allocate the
  amount to AP open items. In one cross-module atomic transaction it posts **Dr AP control / Cr
  Cash-Bank** (AP account resolved by posting rules and carrying the supplier; the cash/bank GL
  account is chosen on the payment), allocates each AP open item via the subledger (settling /
  partially settling it), and records the payment linked to its journal.
- Extended the `ISubledgerPosting` contract with `AllocateAsync`; the Accounting adapter delegates
  to the AP/AR allocation service (over-allocation and already-settled guards surface as failures
  that roll the whole payment back).
- **Api:** `POST /api/v1/supplier-payments`, `GET /api/v1/supplier-payments/{id}`,
  `GET /api/v1/supplier-payments?supplierId=` (Purchasing.Post / Purchasing.View).
- **Persistence:** EF migration `AddSupplierPayments` (SupplierPayments / SupplierPaymentAllocations
  / sequence).
- **Tests:** 3 new (allocation total + zero-amount guard; handler posts balanced Dr AP/Cr Cash and
  allocates each open item; subledger over-allocation fails the payment). Full suite now 160, green.
- **Verified end-to-end:** PO 5 @ 200 + PPN 11% → received → invoiced (AP open item 1,110) →
  partial payment 600 (Dr AP 600 / Cr Bank 600, item PartiallyPaid, outstanding 510) → pay 510
  (item Settled, outstanding 0); a further payment on the settled item was rejected.
- **Completes procure-to-pay:** PO → Goods Receipt → Purchase Invoice → Supplier Payment. See
  [docs/POSTING_RULES.md](docs/POSTING_RULES.md), [docs/ACCOUNTING_DESIGN.md](docs/ACCOUNTING_DESIGN.md).

---

## [2026-06-16 08:22:54 UTC]

CHG-0020 — Purchasing: Purchase Invoice (AP/VAT, clear GR-IR) + AP subledger

- Added **Purchase Invoice** (Purchasing slice 2): bill a supplier for goods received against a PO.
  In one cross-module atomic transaction (reusing CHG-0019's `ICrossModuleUnitOfWork`) it posts
  **Dr GR/IR + Dr VAT Input / Cr AP control** (accounts resolved by posting rules, AP line carrying
  the supplier as subledger party), opens an **AP subledger open item**, advances the PO's invoiced
  quantities, and records the invoice linked to its journal + open item.
- **Three-way-match lite:** a line can only be invoiced up to what has been *received and not yet
  invoiced* (`UninvoicedReceivedQuantity`), so the GR/IR accrual is cleared by exactly what was billed.
- New cross-module contract `ISubledgerPosting` (`Modules.Contracts.Accounting`,
  `OpenPayableAsync`/`OpenReceivableAsync`); Accounting exposes a save-less adapter resolving the
  company's functional currency.
- **Api:** `POST /api/v1/purchase-orders/{id}/invoices`, `GET .../invoices`,
  `GET /api/v1/purchase-invoices/{id}` (Purchasing.Post / Purchasing.View).
- **Persistence:** EF migration `AddPurchaseInvoices` (PurchaseInvoices/Lines/sequence +
  `InvoicedQuantity` on PurchaseOrderLines).
- **Tests:** 4 new (invoice-quantity guard; handler posts a balanced Dr GR-IR+VAT / Cr AP journal,
  opens the AP item, advances the PO; over-invoice guard; zero-tax omits the VAT line). Full suite
  now 157, green.
- **Verified end-to-end:** PO 10 @ 100 + PPN 11% → received 10 → invoiced 10. Invoice net 1,000 /
  VAT 110 / gross 1,110; balanced journal Dr GR-IR 1,000 + Dr VAT Input 110 / Cr AP 1,110; the GR/IR
  accrual for this PO cleared to zero; AP open item 1,110 outstanding, shown in the 1–30 aging bucket;
  PO status Received.
- Completes the core **procure-to-pay** posting chain (PO → Goods Receipt → Purchase Invoice).
  Remaining: Supplier Payment (allocate AP open items, Dr AP / Cr Cash-Bank). See
  [docs/POSTING_RULES.md](docs/POSTING_RULES.md), [docs/ACCOUNTING_DESIGN.md](docs/ACCOUNTING_DESIGN.md).

---

## [2026-06-14 15:01:50 UTC]

CHG-0019 — Purchasing: Goods Receipt + cross-module atomic posting

- Built the **cross-module atomic transaction** foundation (INTEGRATION_EVENTS.md §2): a request-scoped
  shared database connection (`ISharedDbConnection`) and an `ICrossModuleUnitOfWork` that opens one
  transaction, enlists every participating module context (`ITransactionalDbContext`), persists them
  all, and commits — or rolls everything back on failure. No MSDTC (single shared connection).
- New synchronous cross-module contracts in `Modules.Contracts` (now referencing SharedKernel for
  `Result`): `IInventoryPosting`, `IGeneralLedgerPoster`, `IPostingAccountResolver`, plus
  `ICrossModuleUnitOfWork`. Inventory and Accounting expose save-less adapters; Purchasing, Inventory
  and Accounting bind their DbContext to the shared connection.
- **Goods Receipt** (Purchasing slice 2): receive goods against an approved purchase order. In one
  atomic transaction it writes the inventory ledger (moving average), posts **Dr Inventory / Cr
  GR-IR** via posting rules (accounts resolved, never hardcoded), advances the PO receipt status
  (Approved → PartiallyReceived → Received with per-line received/outstanding quantities), and records
  the goods-receipt document linked to its journal.
- **Api:** `POST /api/v1/purchase-orders/{id}/goods-receipts`, `GET .../goods-receipts`,
  `GET /api/v1/goods-receipts/{id}` (Purchasing.Post / Purchasing.View). PO line DTO now exposes the
  line id + received/outstanding quantities.
- **Persistence:** EF migration `AddGoodsReceipts` (GoodsReceipts/GoodsReceiptLines/sequence +
  `ReceivedQuantity` on PurchaseOrderLines). JournalLine now carries an optional subledger party.
- **Tests:** 13 new (GR domain receipt/over-receipt/status; handler orchestration: posts balanced
  journal + advances PO, over-receipt and non-approved guards). Full suite now 153, green.
- **Verified end-to-end, including atomicity:** received 4 of 10 → stock 4 @ 100, GR doc total 400 with
  its journal, PO PartiallyReceived (4/6), balanced Dr Inventory 400 / Cr GR-IR 400 in the GL. Then,
  with the fiscal period **closed**, a further receipt failed and **everything rolled back** — stock
  still 4, PO unchanged, no GR document — proving the transaction is atomic across the three modules.
- See [docs/INTEGRATION_EVENTS.md](docs/INTEGRATION_EVENTS.md), [docs/POSTING_RULES.md](docs/POSTING_RULES.md).

---

## [2026-06-14 14:10:06 UTC]

CHG-0018 — Accounting: AR / AP subledgers (open items, allocation, aging)

- Added the **AR and AP subledgers** (ADR-0011, docs/ACCOUNTING_DESIGN.md §5): open-item tracking
  per customer/supplier with payment allocation and aging — the open-item layer that reconciles to
  the GL control accounts and that invoice/payment posting will move in step with the GL.
- **Domain:** `SubledgerOpenItem` aggregate (Type AR/Payable, party, source document, due date,
  original / settled / computed outstanding, status Open → PartiallyPaid → Settled) with
  `SubledgerAllocation` children. Guards: positive amounts, currency match, no over-allocation
  (`BR-ACC-7`), no allocation to a settled item.
- **Service** (`ISubledgerService`): `OpenItemAsync` + `AllocateAsync`, save-less so a future
  invoice/payment handler can move the subledger and post the journal atomically.
- **Features / Api:** opening-balance + allocation commands and open-item / aging queries, exposed
  symmetrically under `/api/v1/ar/*` and `/api/v1/ap/*` — `GET open-items`, `GET aging`
  (Accounting.View); `POST open-items`, `POST open-items/{id}/allocations` (Accounting.Post).
  Aging buckets: Current / 1–30 / 31–60 / 61–90 / 90+ past due, per party + grand totals.
- **Persistence:** EF migration `AddSubledgers` (`SubledgerOpenItems`, `SubledgerAllocations` in the
  accounting schema).
- **Tests:** 6 new (partial/full allocation status, over-allocation guard, invalid open, service
  error translation, aging bucketing). Full suite now 147, green. Verified end-to-end: recorded an
  AR invoice, partial allocation → PartiallyPaid (outstanding 600,000), aging placed it in 1–30,
  and an over-allocation was rejected.
- Completes the **Accounting slice 2** posting/subledger layer (with CHG-0017). See
  [docs/ACCOUNTING_DESIGN.md](docs/ACCOUNTING_DESIGN.md).

---

## [2026-06-14 13:22:34 UTC]

CHG-0017 — Accounting: posting-rule / account-determination engine

- Added the configurable **account-determination engine** (ADR-0024, docs/POSTING_RULES.md): the
  "engine room" that maps a business event + purpose to a GL account so accounts are configuration
  per company, never hardcoded. Prerequisite for event-driven auto-posting (Sales/Purchasing slice 2).
- **Domain:** `PostingRule` (company-scoped EventType + RuleKey + AccountId, optional dimension
  selectors — ProductCategory / Warehouse / TaxCode / BankAccount — with a `Specificity` score and
  `Matches` logic), `PostingRuleKeys` (the well-known key catalog + required-keys + control-key map),
  and `PostingSelector`.
- **Resolver** (`IPostingRuleResolver`): most-specific matching rule wins; company-wide default
  (`*`, no selectors) is the fallback; unresolved → fails loudly (`BR-ACC-6`,
  `ACCOUNTING.POSTING_RULE_UNRESOLVED`), never a silent wrong account.
- **Health check:** verifies every required key has a valid default rule pointing at an active,
  postable account, and that control-account keys (AR/AP/Inventory) point at matching control accounts.
- **Api:** `GET /api/v1/posting-rules`, `GET /api/v1/posting-rules/health` (Accounting.View),
  `POST /api/v1/posting-rules` upsert (Accounting.Post, idempotent repoint).
- **Seed:** 13 default rules for the dev company per POSTING_RULES.md §2 (CashBank resolved per
  chosen bank/cash account via selector, so not seeded as a single default). EF migration
  `AddPostingRules` (accounting schema).
- **Tests:** 7 new (resolver fallback / specificity / non-match / fail-loud; health green / missing
  key / wrong control type). Full suite now 141, green. Verified end-to-end: health returns
  `isHealthy: true` with the 13 seeded rules; upsert is idempotent.
- See [docs/POSTING_RULES.md](docs/POSTING_RULES.md) and [docs/ACCOUNTING_DESIGN.md](docs/ACCOUNTING_DESIGN.md).

---

## [2026-06-14 13:00:35 UTC]

CHG-0016 — Accounting: Profit & Loss and Balance Sheet statements

- Added the **Profit & Loss** and **Balance Sheet** financial statements, derived from the GL
  (ADR-0008 — never from transactional tables). Verified end-to-end: posted a cash sale + its COGS,
  then P&L showed Revenue 1,000,000 − Expenses 600,000 = Net Profit 400,000, and the Balance Sheet
  showed Assets 400,000 = Liabilities 0 + Equity 400,000 (current earnings), Balanced = true.
- **Application:** `GetProfitAndLossQuery` (period revenue/expense + net profit) and
  `GetBalanceSheetQuery` (as-of assets/liabilities/equity; net income to date shown as current
  earnings within equity — year-end close to retained earnings is a later phase). Both reuse the
  existing GL read store; no schema change.
- **Api:** `GET /api/v1/reports/profit-loss` and `GET /api/v1/reports/balance-sheet`
  (under the `/reports` group, gated by Accounting.View).
- **Tests:** 2 report handler tests (net-profit math, balance-sheet balancing). Full suite now 134, green.
- First increment of **Accounting slice 2**; remaining: posting rules / account determination,
  AR/AP subledgers, period-close snapshots, Cash Flow.
- See [docs/ACCOUNTING_DESIGN.md](docs/ACCOUNTING_DESIGN.md).

---

## [2026-06-14 06:03:10 UTC]

CHG-0015 — Purchasing module (slice 1): Purchase Orders + cross-module integration

- Implemented the first **transactional vertical** — Purchase Orders — wired through the whole
  foundation. Verified end-to-end: created a PO (Master-Data-validated supplier/product/warehouse;
  totals 10,000,000 + 11% PPN = 11,100,000), submitted it → the Approval engine created a pending
  request and the PO went `PendingApproval`; Process Tracker recorded "Submitted for approval" and
  the submitter was notified; a second user approved → the `ApprovalDecided` event advanced the PO
  to **Approved** and added an "Approved" milestone.
- **Cross-module contracts (ADR-0007):** added `IApprovalService` (implemented by Approval over its
  submit use case) and `IMasterDataLookup` (implemented by Master Data) to `Modules.Contracts`, so
  Purchasing requests approval and validates references without depending on module internals.
- **Domain:** `PurchaseOrder` (+ lines, status workflow Draft → PendingApproval → Approved/Rejected,
  per-company number sequence, sub/tax/grand totals), errors.
- **Application:** CreatePurchaseOrder (validates supplier/product/warehouse, resolves currency),
  SubmitPurchaseOrder (calls `IApprovalService`, auto-approves when no rule matches), queries, and an
  `ApprovalDecided` consumer that advances PO status via integration events.
- **Infrastructure:** `PurchasingDbContext` (own `purchasing` schema) + configs, repository, DI
  (event-consumer subscription), factory, `InitialPurchasing` migration (verified on real SQL Server).
- **Api:** `GET/POST /api/v1/purchase-orders`, `GET /{id}`, `POST /{id}/submit` — gated by
  Purchasing.View / Purchasing.Create.
- **Tests:** 13 Purchasing unit tests (totals, status workflow, submit→approval, event consumer) +
  architecture-boundary tests. Full suite now 132, green.
- **Scope:** slice 1 (Purchase Orders only). Slice 2: Goods Receipt (inventory ledger + Dr Inventory
  / Cr GR-IR — requires the cross-module atomic-transaction infrastructure), Purchase Invoice
  (AP/VAT), Supplier Payment.
- See [docs/MODULES.md](docs/MODULES.md), [docs/STATUS.md](docs/STATUS.md).

---

## [2026-06-14 05:24:37 UTC]

CHG-0014 — Notification module (in-app) — Phase 1 foundation complete

- Implemented the **Notification** module: in-app `Notification` (per-user title/body/read state),
  a consumer that subscribes to the approval integration events and notifies the document's
  submitter, and `GET /api/v1/notifications` (+ `?unreadOnly=true`) / `POST /{id}/read`. Own
  `notification` schema + `InitialNotification` migration. Verified end-to-end: submitting a PO and
  having it approved produced "submitted for approval" and "approved" notifications for the
  submitter, and mark-read worked — a *second* consumer firing on the same events alongside Process
  Tracker.
- **Phase 1 foundation modules are now complete** (Identity, Company, Audit, Approval, Process
  Tracker, Notification + integration events). The only outstanding Phase-1 item is the dedicated
  cross-tenant isolation *integration* test suite.
- **Tests:** 3 Notification unit tests + architecture-boundary tests. Full suite now 117, green.
- See [docs/MODULES.md](docs/MODULES.md), [docs/STATUS.md](docs/STATUS.md).

---

## [2026-06-14 05:12:24 UTC]

CHG-0013 — In-process integration events + Process Tracker module

- Added the **in-process integration-event mechanism** (ADR-0007): `IIntegrationEvent` + event
  records in `Modules.Contracts`, `IIntegrationEventPublisher` / `IIntegrationEventHandler<T>` in
  Application.Abstractions, and a best-effort dispatcher in Infrastructure.Common (a failing handler
  is logged, not fatal — eventual semantics; durable outbox is a later hardening).
- **Approval** now publishes `ApprovalSubmitted` and `ApprovalDecided` after committing.
- Implemented the **Process Tracker** module: `ProcessEvent` (append-only per-document lifecycle
  milestone), a consumer that subscribes to both approval events and appends milestones, a
  `GET /api/v1/documents/{type}/{id}/timeline` query, own `process` schema + `InitialProcessTracker`
  migration. Verified end-to-end: submitting a PO recorded "Submitted for approval" and approving it
  added "Approved" on the document timeline — driven entirely by integration events across modules.
- **Tests:** 5 Process Tracker unit tests (event→milestone mapping) + architecture-boundary tests.
  Full suite now 111, green.
- See [docs/INTEGRATION_EVENTS.md](docs/INTEGRATION_EVENTS.md), [docs/WORKFLOW_APPROVAL.md](docs/WORKFLOW_APPROVAL.md).

---

## [2026-06-14 04:47:07 UTC]

CHG-0012 — Approval Workflow module + GUID-key platform fix

- Implemented the Phase 1 **Approval Workflow** — a generic, document-agnostic engine. Verified
  end-to-end: created a conditional 2-level-capable definition, submitted a document → Pending,
  the submitter's approval was blocked by segregation of duties (BR-APR-2), a second eligible user
  approved → Approved (with the action recorded), and a non-matching document auto-approved.
- **Domain:** `ApprovalDefinition` (+ `ApprovalCondition` numeric thresholds, `ApprovalStep` with
  User/Role approvers), `ApprovalRequest` (steps snapshotted at submit) + `ApprovalAction`,
  eligibility + SoD logic, enums, errors.
- **Application:** CreateDefinition, SubmitForApproval (picks the first matching active definition
  or auto-approves), DecideApproval (approve/reject with SoD + eligibility), queries (my pending,
  request detail, definitions).
- **Infrastructure:** `ApprovalDbContext` (own `approval` schema) + configs, repositories, DI,
  design-time factory, `InitialApproval` migration (verified on real SQL Server).
- **Api:** `/api/v1/approval-definitions` (gated by the new `Approval.Manage` permission) and
  `/api/v1/approval-requests` (submit, mine, detail, approve, reject).
- **Platform fix (ADR-0028):** `BaseDbContext` now configures GUID `Id` keys as
  `ValueGeneratedNever` (domain-generated). This fixes EF mis-detecting a newly-constructed child
  added to a *loaded* aggregate as an existing row (it was emitting a 0-row UPDATE → concurrency
  exception, first hit when approving adds an `ApprovalAction`). Column DDL unchanged → no migration
  impact; all 103 tests still green.
- **Identity:** the dev seeder now grants the Administrator role any newly-added catalog
  permissions on startup (so the admin stays fully privileged across releases); `ICurrentUser`
  gained `Roles` (from JWT) for role-based approver eligibility.
- **Tests:** 12 Approval unit tests (condition matching, multi-level advance, reject, SoD,
  eligibility, auto-approve) + Approval architecture-boundary tests. Full suite now 103, green.
- See [docs/WORKFLOW_APPROVAL.md](docs/WORKFLOW_APPROVAL.md), [ADR-0028](docs/DECISIONS.md).

---

## [2026-06-14 04:08:53 UTC]

CHG-0011 — Add STATUS.md milestone tracker ("where we are / what's next")

- Added [docs/STATUS.md](docs/STATUS.md) as the canonical resume point: current snapshot (last
  change, commit, test count), milestone checklist by phase (✅/🟡/◻️ with CHG refs), the deferred
  slice backlog (Accounting slice 2, Inventory slice 2, cross-module atomic posting, frontend), the
  recommended next step, and how-to-resume commands + gotchas.
- Linked it from `README.md`, the docs index, and the `CLAUDE.md` documentation structure; added a
  CONTRIBUTING rule to update STATUS.md when a module/slice lands.
- Docs only — no code changes.

---

## [2026-06-13 13:44:53 UTC]

CHG-0010 — Inventory engine (slice 1): ledger + moving-average costing

- Implemented the **Inventory** backbone. Verified end-to-end against a real database: received
  10@100 then 10@120 → on-hand 20 @ avg 110; issued 5 → cost 550 (avg unchanged); over-issue
  rejected (BR-INV-3); on-hand value 15×110=1650; full stock card; all ledger writes auto-audited.
- **Domain:** `InventoryTransaction` (immutable, append-only, the source of truth — ADR-0014),
  `StockCostBucket` (moving weighted-average per Company×Warehouse×Product — ADR-0015 with
  receive/issue math + negative-stock guard), `MovementType`/`MovementSource`, errors.
- **Application:** `IInventoryLedger` + `InventoryLedgerService` (receive/issue, returns cost
  applied for future COGS posting); Receive / Adjust (in/out) / Transfer commands (cost travels
  between warehouses); on-hand and stock-card queries.
- **Infrastructure:** `InventoryDbContext` (own `inventory` schema) + configs (decimal precision,
  unique bucket per company×warehouse×product), repositories, DI, design-time factory,
  `InitialInventory` migration (verified on real SQL Server).
- **Api:** `POST /api/v1/stock/receipts|adjustments|transfers`, `GET /api/v1/stock/on-hand|card`
  — gated by Inventory.View / Inventory.Adjust / Inventory.Transfer.
- **Tests:** 11 Inventory unit tests (moving-average math incl. fractional + negative-stock; ledger
  service receive/issue/insufficient) + Inventory architecture-boundary tests. Full suite now 88, green.
- **Scope:** slice 1. Slice 2 (next for inventory): GL posting on stock moves (Dr/Cr Inventory/COGS/
  Variance via the accounting engine), stock opname, the per-company negative-stock setting, and
  back-dating recompute. Atomic cross-module posting (Sales issue + COGS) is designed when Sales lands.
- See [docs/INVENTORY_DESIGN.md](docs/INVENTORY_DESIGN.md), [docs/MODULES.md](docs/MODULES.md).

---

## [2026-06-13 13:23:30 UTC]

CHG-0009 — Master Data module (products, parties, warehouses, tax codes)

- Implemented the **Master Data** module — the reference data that unblocks Sales/Purchasing/
  Inventory (which will post into the accounting engine).
- **Domain:** `UnitOfMeasure`, `ProductCategory`, `Product` (code/SKU, base UoM, stock/sell/
  purchase flags), `Customer`, `Supplier`, `Warehouse`, `TaxCode` (fractional rate). All carry a
  unique-per-company code; added an `IHasCode` shared-kernel marker.
- **Application:** create + list use cases per aggregate over a generic `ICodedRepository<T>`
  (keeps each use case tiny); product creation validates the base UoM exists.
- **Infrastructure:** `MasterDataDbContext` (own `masterdata` schema) + configs (per-company
  unique code indexes), generic EF repository, dev seeder (PCS unit, GENERAL category, MAIN-WH
  warehouse, PPN11 tax code), DI, design-time factory, `InitialMasterData` migration (verified to
  apply to a real SQL Server).
- **Api:** `GET`/`POST` for `/api/v1/units-of-measure`, `/product-categories`, `/products`,
  `/customers`, `/suppliers`, `/warehouses`, `/tax-codes` — gated by MasterData.View / MasterData.Manage.
- **Tests:** 9 Master Data unit tests (domain normalization/validation + CreateProduct handler) +
  Master Data architecture-boundary tests. Full suite now 74, green.
- See [docs/MODULES.md](docs/MODULES.md).

---

## [2026-06-13 13:02:09 UTC]

CHG-0008 — Accounting engine (slice 1): chart of accounts, fiscal periods, double-entry journals, trial balance

- Implemented the Phase 2 **Accounting** backbone across clean-architecture layers. Verified
  end-to-end against a real database: posted a balanced journal, rejected an unbalanced one
  (BR-ACC-1), produced a balancing Trial Balance, reversed the entry (net-zero), and saw all of it
  captured automatically by the audit log.
- **Domain:** `Account` (+ `AccountType`/`NormalBalance`/`ControlType`), `FiscalYear`/`FiscalPeriod`
  (open/close/lock), `JournalEntry`/`JournalLine` (balanced double-entry, immutable, reversal-only),
  per-company `JournalNumberSequence`, errors.
- **Application:** ports + `IJournalPoster`; CreateAccount/GetAccounts; CreateFiscalYear/Close/Reopen;
  PostJournal, ReverseJournal, GetJournalEntry; GetTrialBalance (derived from the GL, ADR-0008).
- **Infrastructure:** `AccountingDbContext` (own `accounting` schema) + configs (owned `Money`
  columns, `DateOnly` periods), repositories, GL trial-balance read store, default Indonesian-SMB
  chart + current fiscal-year seeder, DI, design-time factory, `InitialAccounting` migration.
- **Api:** `/api/v1/accounts`, `/api/v1/fiscal-years`, `/api/v1/fiscal-periods/{id}/close|reopen`,
  `/api/v1/journal-entries` (post/get/reverse), `/api/v1/reports/trial-balance`. Permission-gated
  (Accounting.View/Post/PeriodClose/PeriodReopen, MasterData.Manage). JSON enums as strings.
- **Cross-module:** added the `Modules.Contracts` project with `ICompanyDirectory` (implemented by
  Company Management) so Accounting reads a company's functional currency without coupling.
- **Tests:** 16 Accounting unit tests (journal invariants, reversal, posting service:
  balance/period/account rules) + Accounting architecture-boundary tests. Full suite now 62, green.
- **Scope:** slice 1 only. Slice 2 (next): posting rules / account determination (POSTING_RULES.md),
  AR/AP subledgers, period-close snapshots, and P&L / Balance Sheet / Cash Flow reports.
- See [docs/ACCOUNTING_DESIGN.md](docs/ACCOUNTING_DESIGN.md), [docs/MODULES.md](docs/MODULES.md).

---

## [2026-06-13 12:35:38 UTC]

CHG-0007 — Sync design docs with implemented foundation

- Documentation-only pass to bring the design docs in line with the implemented modules
  (CLAUDE.md "documentation must remain synchronized").
- **ARCHITECTURE.md:** updated the solution layout with implemented/planned (✅/◻️) markers, the
  new `Web.Common` building block, the `CompanyManagement` naming note, and why AuditLog has no
  Domain project; noted atomic shared-table audit in the cross-cutting table.
- **DATABASE.md:** documented the three entity bases (`Entity` / `TenantScopedEntity` /
  `TenantOwnedEntity`), `PagedResult<T>`, and the shared append-only `AuditEntry` / `audit.AuditEntries`
  table (ADR-0026).
- **MODULES.md:** added an implementation-status table.
- **ROADMAP.md:** checked off Phase 1 items 1–4; flagged the cross-tenant integration test suite
  as still outstanding.
- **DECISIONS.md:** added [ADR-0027](docs/DECISIONS.md) (build with the .NET 9 SDK while targeting
  net8.0).
- No code changes.

---

## [2026-06-13 12:25:32 UTC]

CHG-0006 — Audit Log module (automatic, atomic before/after capture)

- Implemented the Phase 1 **Audit Log** with automatic, tamper-evident change capture
  (ADR-0006/0026, SECURITY.md §4). Verified end-to-end against a real database (a login's
  `LastLoginAtUtc` change was captured and returned by the API).
- **Shared kernel:** `AuditEntry` (append-only primitive: tenant/company, entity type+id, action,
  before/after JSON, user, timestamp) + `AuditAction`; plus a general `PagedResult<T>`.
- **Infrastructure.Common:** `AuditCaptureInterceptor` records inserts/updates/deletes and adds an
  `AuditEntry` to the **same transaction** as the change (runs before soft-delete conversion).
  `BaseDbContext` maps the shared `audit.AuditEntries` table, excluded from each module's
  migrations.
- **AuditLog module:** `AuditDbContext` owns the table + `InitialAudit` migration; tenant-scoped
  read store + `GET /api/v1/audit-entries` (filtered, paged), gated by the new `Audit.View`
  permission.
- **Wiring:** the capture interceptor is registered in the Identity and Company DbContexts; the
  host registers the module and creates the audit table before modules that write to it.
- **Shared kernel ergonomics:** (none beyond the above).
- **Tests:** audit-capture interceptor tests (insert + update before/after) and AuditLog
  architecture-boundary tests. Full suite now 43, green.
- See [docs/SECURITY.md](docs/SECURITY.md), [docs/DATABASE.md](docs/DATABASE.md),
  [ADR-0026](docs/DECISIONS.md).

---

## [2026-06-13 12:09:43 UTC]

CHG-0005 — Company Management module (tenants, companies, settings)

- Implemented the Phase 1 **Company Management** module across clean-architecture layers
  (Domain / Application / Infrastructure / Api).
- **Domain:** `Tenant` (tenancy root), `Company` (tenant-scoped; functional currency, fiscal-year
  start month, time zone, tax id), `CompanySetting` (company-owned key/value), domain errors.
- **Application:** CreateCompany, UpdateCompany, SetCompanySetting commands + GetCompanies /
  GetCompanyById queries (MediatR + FluentValidation, returning `Result`).
- **Infrastructure:** `CompanyDbContext` (own `company` schema) + configurations, repository,
  dev tenant/company seeder (well-known GUIDs matching the Identity dev admin's grant), DI,
  design-time factory, and the `InitialCompany` migration (verified to apply to a real SQL Server).
- **Api:** `GET/POST/PUT /api/v1/companies`, `GET /api/v1/companies/{id}`,
  `PUT /api/v1/companies/{id}/settings` (create/update/settings gated by `Admin.Companies`).
- **Host:** registers the module and seeds Company before Identity at startup so the dev admin's
  tenant/company exist.
- **Shared kernel:** added an implicit `Error` → `Result` conversion for ergonomics.
- **Naming:** module uses `Accountrack.CompanyManagement.*` namespaces to avoid the `Company`
  type vs namespace-segment collision.
- **Tests:** 10 Company unit tests (domain invariants + CreateCompany handler) and Company
  architecture-boundary tests. Full suite now 39, green.
- See [docs/MODULES.md](docs/MODULES.md), [docs/DATABASE.md](docs/DATABASE.md).

---

## [2026-06-13 11:52:14 UTC]

CHG-0004 — Introduce changelog and changelog policy

- Added this `CHANGELOG.md` as the project's immutable historical record, in reverse-chronological
  order (newest on top) using sequential `CHG-NNNN` ids and UTC timestamps.
- Backfilled entries CHG-0001..CHG-0003 for the work delivered so far.
- Added a **Changelog** policy section to `CLAUDE.md` (ordering, id, timestamp, and AI-agent rules)
  and listed `CHANGELOG.md` in the documentation structure.
- Referenced the changelog from `README.md` and `docs/README.md`; recorded the decision as
  [ADR-0025](docs/DECISIONS.md).
- Docs only — no code or schema changes.

---

## [2026-06-13 11:42:07 UTC]

CHG-0003 — Identity module (authentication, RBAC, JWT, refresh rotation)

- Implemented the Phase 1 **Identity** module end-to-end across clean-architecture layers
  (Domain / Application / Infrastructure / Api). Commit `204f82b`.
- **Domain:** `User`, `Role`, `Permission`, `RefreshToken` (+ join entities), `Email` value
  object, permission catalog, domain errors.
- **Application:** Login, RefreshToken (rotation + reuse-detection family revocation), Logout,
  CreateUser — MediatR handlers with FluentValidation, returning `Result`.
- **Infrastructure:** `IdentityDbContext` (own `identity` schema) + EF configurations, PBKDF2
  password hasher, HS256 token service, repositories (auth lookups use the reviewed tenant-filter
  bypass), permission/dev-admin seeder, DI, and the `InitialIdentity` migration (verified to apply
  to a real SQL Server).
- **Api:** `POST /api/v1/auth/login·refresh·logout`, `GET /api/v1/auth/me`, `POST /api/v1/users`.
- **Shared kernel:** added `TenantScopedEntity` / `ITenantScoped` (tenant-only, for Users/Roles);
  extended `BaseDbContext` query filters and the auditing interceptor accordingly.
- **Web.Common (new building block):** standard response envelope, `Result`→HTTP mapping, and the
  exception middleware (moved out of the host for reuse by module Api layers).
- **Host:** JWT bearer authentication (claims unmapped), HttpContext-backed `ICurrentUser` /
  `ITenantContext` replacing the placeholders, a permission-as-policy provider, and Identity module
  registration with optional startup migrate + seed (off by default).
- **Tests:** 20 Identity unit tests + Identity architecture-boundary tests (total suite 26, green).
- Decisions: [ADR-0019](docs/DECISIONS.md) (RBAC + SoD), [ADR-0020](docs/DECISIONS.md) (JWT +
  rotating refresh). See [docs/SECURITY.md](docs/SECURITY.md), [docs/MODULES.md](docs/MODULES.md).

---

## [2026-06-13 08:38:00 UTC]

CHG-0002 — Modular-monolith solution skeleton and shared kernel

- Scaffolded the .NET 8 solution and Phase 1 building blocks. Commit `907f2f0`.
- `Accountrack.sln` + `Directory.Build.props` (net8.0, nullable, warnings-as-errors).
- **SharedKernel:** `Entity`, `TenantOwnedEntity`, `ValueObject`, `Money`, `Result`/`Error`,
  domain-event base, audit/soft-delete/tenancy marker interfaces (no dependencies).
- **Application.Abstractions:** CQRS contracts, ambient-context ports
  (`ITenantContext`/`ICurrentUser`/`IClock`), logging + validation pipeline behaviors.
- **Infrastructure.Common:** `BaseDbContext` with global query filters (soft-delete + tenant),
  auditing/tenancy `SaveChanges` interceptor, outbox message.
- **Accountrack.Api host:** error-envelope middleware, response envelope, MediatR pipeline,
  Swagger, `/health` and `/api/v1/ping` (verified returning the standard envelope).
- **ArchitectureTests:** NetArchTest boundary rules ([ADR-0023](docs/DECISIONS.md)).
- See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

---

## [2026-06-13 07:31:13 UTC]

CHG-0001 — Architecture and design documentation foundation

- Established the full `docs/` set defining the system before implementation, plus a
  stack-tailored `.gitignore`. Commit `2fef2a1`.
- Authored 22 documents + index and an ADR template: PRD, ROADMAP, ARCHITECTURE, MODULES,
  INTEGRATION_EVENTS, DATABASE, API_SPEC, ERROR_HANDLING, ACCOUNTING_DESIGN, POSTING_RULES,
  INVENTORY_DESIGN, WORKFLOW_APPROVAL, SECURITY, MULTI_TENANCY, BUSINESS_RULES, DECISIONS,
  GLOSSARY, CODING_STANDARDS, TESTING, DEPLOYMENT, CONTRIBUTING.
- Recorded 24 architectural decisions (ADR-0001..ADR-0024), including modular monolith,
  shared-DB multi-tenancy, double-entry GL as source of truth, AR/AP subledgers, GR/IR clearing,
  moving-average inventory, Indonesia-first PPN, single functional currency, and RBAC + SoD.
- Updated `CLAUDE.md` with the confirmed decisions and new non-negotiable rules; refreshed
  `README.md`.
