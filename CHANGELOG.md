# Accountrack Changelog

## [2026-07-08 13:43:09 UTC]

CHG-0117 — Transactional document detail pages (delivery, invoice, payment, receipt, bill)

- **Six read-only document detail pages**, turning previously dead-end rows into a fully navigable
  web of documents:
  - **Sales:** Delivery Order (`sales/deliveries/:id`), Sales Invoice (`sales/invoices/:id`),
    Customer Payment (`sales/payments/:id`).
  - **Purchasing:** Goods Receipt (`purchasing/receipts/:id`), Purchase Invoice / Bill
    (`purchasing/invoices/:id`), Supplier Payment (`purchasing/payments/:id`).
- Each page shows a header (number + posted badge + party/date), line items with totals, notes, and
  cross-links. Delivery/receipt pages label amounts as **cost of goods** (COGS / Dr Inventory-Cr
  GR-IR) and link to the source order; invoice pages offer the **PDF** and a link to the order;
  payment pages resolve the **cash/bank account** name and (best-effort) the **allocated invoice**
  numbers via the party's open items.
- **Wiring:** the Deliveries list now opens the delivery (not its SO); the Sales/Purchase Order
  detail delivery/receipt/invoice numbers are now clickable; the customer/supplier detail
  *Payments* rows open the payment detail.
- Frontend only — all six backend detail endpoints already existed. New shared `docDetail.*`
  strings + `common.back` (en + id); new `DeliveryOrder` / `CustomerPayment` / `GoodsReceipt` /
  `SupplierPayment` TS types and `getDelivery` / `getCustomerPayment` / `getReceipt` /
  `getSupplierPayment` API methods. Builds clean (vue-tsc + vite).

---

## [2026-07-07 14:54:16 UTC]

CHG-0116 — Actionable balances + product history/detail page

- **Pay from the party detail.** The customer detail shows a **Receive payment** button and the
  supplier detail a **Pay supplier** button (shown when there's an outstanding balance and the user
  has `Sales.Post` / `Purchasing.Post`). They open the payment screen **preselected** to that party,
  with their open items ready to allocate — the receive-payment / pay-supplier views now read a
  `?customerId` / `?supplierId` query param.
- **Product detail page.** Product rows are now clickable → a detail page that **traces
  purchase/sales history**: header (category, unit, costing), 4 KPI cards (in stock, stock value,
  average cost, sale price), a **stock-by-warehouse** table, and a **movement history** table
  (receipts in / issues out / adjustments / transfers with source, signed qty and running balance),
  reusing the inventory stock-card labels.
- New route `masterDataProductDetail`; Edit/Deactivate stay row actions (`@click.stop`). Reads
  existing endpoints (`/stock/card`, `/stock/on-hand`, `/ar|ap/open-items`); cross-module reads
  degrade gracefully. Frontend only; `party.*` / `productDetail.*` strings (en + id). Builds clean.

---

## [2026-07-07 14:26:01 UTC]

CHG-0115 — Master data: customer / supplier / warehouse detail pages (phase 2)

- **Clickable rows** on the Customers, Suppliers and Warehouses lists now open a detail page (Edit
  and Deactivate stay as row actions via `@click.stop`).
- **Customer detail** — profile (tax id, terms, credit limit, price list), 4 KPI cards (receivable,
  overdue, #orders, lifetime sales) and sections: **Open invoices** (AR), **Sales orders**
  (click-through to the order) and **Payments received**.
- **Supplier detail** — mirror with **Open bills** (AP), **Purchase orders** and **Payments made**;
  KPIs payable / overdue / #POs / lifetime purchases.
- **Warehouse detail** — KPIs (stock value, SKUs, units on hand) and a **stock-contents** table
  (product, on-hand, avg cost, value) with a total.
- New lib calls `salesApi.customerPayments` / `purchasingApi.supplierPayments`; consumes existing
  `/ar|ap/open-items`, `/sales-orders`, `/purchase-orders`, `/stock/on-hand`. All cross-module reads
  degrade gracefully without the relevant permission. Routes `masterData{Customer,Supplier,Warehouse}Detail`.
  Frontend only; `party.*` / `warehouseDetail.*` strings (en + id). Builds clean.

---

## [2026-07-07 14:04:31 UTC]

CHG-0114 — Master data as dedicated pages + insight columns (phase 1)

- **Sidebar restructure.** Products, Customers, Suppliers and Warehouses are now their own top-level
  sidebar pages under a new **Master data** section; Units, Categories, Tax codes and Price lists
  remain grouped on a slim **Setup** page. Route names are unchanged (⌘K palette still works).
- **Consistent layout — 4 insight cards** on every master-data list (was 3), matching the other
  modules.
- **Products:** new **In stock** column (summed across warehouses) + cards Total · Active ·
  **Stock value** · **Out of stock**.
- **Customers:** new **Owes us** (AR outstanding) column, red when overdue + cards Total · Active ·
  **Total receivable** · **Overdue**.
- **Suppliers:** new **We owe** (AP outstanding) column + cards Total · Active · **Total payable** ·
  **Overdue**.
- **Warehouses:** insight cards added (Total · Active · **Stock value** · **SKUs**) + **SKUs** and
  **Stock value** columns.
- Reads existing endpoints (`/stock/on-hand`, `/ar/aging`, `/ap/aging`); the cross-module fetches
  **degrade gracefully** if the user lacks Inventory/Accounting.View. Frontend only; no backend or
  schema change. `nav.*` / master-data column strings (en + id). Builds clean.
- Phase 2 (next): clickable rows → **Customer / Supplier / Warehouse detail pages** (transaction
  history, stock contents).

---

## [2026-07-07 13:11:43 UTC]

CHG-0113 — Deliveries list + clarify the delivery COGS figure

- **New Delivery Orders list** (Sales → Deliveries) — a company-wide list of delivery orders with DO
  number, customer, date, **cost of goods (COGS)** and posted status; clicking a row opens its sales
  order. Backend: `GET /api/v1/delivery-orders` (`GetDeliveriesQuery`, `Sales.View`) +
  `IDeliveryOrderRepository.ListAsync`; `DeliveryOrderListItemDto` with the customer name resolved.
- **Clarifies the delivery amount.** On the Sales Order detail, the Deliveries card now has column
  headers and a note that the figure is the **cost of goods shipped (COGS)** — posted Dr COGS /
  Cr Inventory from moving-average/FIFO cost — **not** the sale price (revenue is recognized at
  invoicing). This addresses confusion where the COGS looked like a random auto-generated number.
- Frontend: `DeliveriesView`, route `salesDeliveries`, a Deliveries button on the Sales list,
  `deliveries.*` + `sales.detail.cogs*` strings (en + id). Full backend suite green (350); frontend
  builds clean.

---

## [2026-07-07 12:55:57 UTC]

CHG-0112 — Frontend: richer product list (category, UoM, prices, costing)

- The **Products** master-data table now shows meaningful columns beyond code/name: **Category**,
  **Unit** (base UoM), **Sale price**, **Purchase price**, **Costing method**, and **Stock-tracked**,
  alongside Status. Category name and UoM code are resolved (searchable + exported), not raw ids.
- Sale/Purchase price render as money (or "—" when unset); costing shows Moving average / FIFO.
- Frontend only; no backend or schema change. Builds clean (vue-tsc + vite).

---

## [2026-07-07 12:31:03 UTC]

CHG-0111 — Pricing: product base price + shared discount lists (ADR-0036, supersedes ADR-0035)

- **Reworks CHG-0110's pricing model** after feedback that a price list per customer (a row per
  product) was hard to add and maintain. Adopts the mainstream SMB-ERP model.
- **Base price on the product** — `Product.SalePrice` + `PurchasePrice` (nullable). These are the
  default and auto-fill order lines directly, so the common case is **one price per product, no list**.
- **Price lists become shared discount rules** — a list now carries a `DiscountPercent` off the base
  price plus optional per-product **fixed overrides**; one list (e.g. "Wholesale −10%") is shared by
  many customers/suppliers. The **company-default-list** concept is removed (the base price is the
  default).
- **Resolution** for `(type, party, product)`: override → % off base → **product base price** →
  manual. The order form resolves only the party's adjustments and overlays them on the product base.
- **Backend:** `Product.SalePrice`/`PurchasePrice`; `PriceList.DiscountPercent` (dropped `IsDefault`);
  rewritten `ResolvePrices`; migration adds the three columns and drops `IsDefault`. Resolution
  re-tested (base only, %-discount, override-wins, overrides-only).
- **Frontend:** base-price inputs on the product form; the Price Lists screen now edits a discount %
  and per-product overrides; SO/PO lines auto-fill from the product base overlaid by the party list;
  `priceLists.*` + product price strings updated (en + id).
- **Docs:** **ADR-0036** (supersedes ADR-0035, now marked Superseded); BUSINESS_RULES BR-PRICE-1..5
  revised; STATUS. **Also resolved committed merge-conflict markers left in DECISIONS.md by the
  concurrent ADR-0034/ADR-0035 merge** (index + body now list ADR-0034/0035/0036 cleanly).
- Full backend suite green; frontend builds clean.

- **Products can now carry sell/buy prices** via **price lists** (Master Data). A price list is typed
  Sales or Purchase and holds a per-product unit price; one list per type may be the company
  **default**, and a customer/supplier may point at its own list that overrides the default.
- **Order lines auto-fill.** When a customer/supplier is chosen on a Sales/Purchase order, the form
  resolves the applicable prices (company default overlaid by the party's list) and prefills each
  line's unit price on product-select — **still editable**. No match → the line stays manual, exactly
  as before. Pricing has **no accounting impact** (the entered price posts as it always did).
- **Backend:** `PriceList` + `PriceListItem` entities; `Customer.SalesPriceListId` /
  `Supplier.PurchasePriceListId` assignment; `IPriceListRepository`; CRUD + item upsert/delete +
  `ResolvePrices` (productId→price map) under `/api/v1/price-lists` (MasterData.* permissions).
  Migration adds the two tables + two nullable FK columns. Resolution unit-tested (default only,
  party override, empty).
- **Frontend:** a **Price Lists** screen under Master Data (list/create/edit + a product-price items
  editor); a price-list selector on the customer/supplier edit forms; Sales/Purchase order auto-fill;
  `lib/pricing.ts` + types; `priceLists.*` strings (en + id).
- **v1 scope:** default + per-party lists. No quantity breaks, date-effective versioning, discounts,
  or multi-currency price lists yet (later slices on the same model). Full backend suite green;
  frontend builds clean. Docs: **ADR-0035**, BUSINESS_RULES BR-PRICE-*, DECISIONS index (also
  backfilled ADR-0033).

> Note: `CHG-0109` (inventory FIFO, ADR-0034) is on a sibling branch merging concurrently; this entry
> uses `CHG-0110`. Reconcile ordering/numbering on merge.

---

## [2026-07-07 04:37:21 UTC]

CHG-0108 — Frontend: surface the Audit trail + document Process-Tracker timeline

- **Audit trail screen** — a new `Audit.View`-gated tab in Settings that lists the existing,
  previously UI-less audit log (CHG-0006). Filter by record type and date range; paged newest-first
  (50/page). Each row shows when, the action (Created/Updated/Deleted badge), the record
  type + short id, and the acting user; expanding a row parses `changesJson` into a
  field-by-field **before → after** table (snapshot for insert/delete, old/new for updates).
  New `lib/audit.ts`, `types/audit.ts` (incl. the `PagedResult` envelope), `AuditTrail.vue`,
  wired into `SettingsView.vue`; `settings.audit.*` strings (en + id). Consumes
  `GET /api/v1/audit-entries` (no backend change).
- **Document lifecycle timeline** — the previously UI-less Process Tracker (CHG-0013) now renders on
  the Sales Order, Purchase Order and Expense Voucher detail pages. A vertical timeline shows each
  milestone (Submitted / Auto-approved / Approved / Approval advanced / Rejected) with a relative
  time (absolute on hover), acting user, and any note. New `lib/processTracker.ts`,
  `types/processTracker.ts`, `DocumentTimeline.vue`; `timeline.*` strings (en + id, with known
  milestones localized and an English fallback). Consumes
  `GET /api/v1/documents/{documentType}/{documentId}/timeline` (no backend change).
- Frontend-only; no backend, schema, or migration change. `Audit.View` is already seeded to the
  Administrator, Accountant and Viewer roles; the timeline endpoint needs only authentication.
- Users are shown as short ids (no lightweight name-lookup endpoint is available to `Audit.View`
  holders, who need not hold `Admin.Users`). Builds clean (`vue-tsc --noEmit` + `vite build`).
- Closes the last "surface built-but-hidden backend in the UI" follow-up in STATUS.md.

---

## [2026-07-06 16:45:00 UTC]

CHG-0107 — Frontend: live in-app notifications (the bell)

- **Wires up the previously dead notification bell** in the top bar to the existing Notification
  module (CHG-0014). Clicking the bell opens a quiet popover listing the current user's
  notifications newest-first; unread rows are tinted (`accent-soft`) with an accent dot and bold
  title, each showing a relative timestamp ("2 hours ago" / "2 jam lalu").
- An **unread-count badge** sits on the bell (capped at `9+`). Clicking a row marks it read; a
  **"Mark all read"** action clears them all. Marking is optimistic and reverts on failure.
- The bell **polls every 60 s** while mounted (no websocket in the stack yet) via a small Pinia
  store; the popover closes on outside-click / Escape.
- New: `NotificationBell.vue`, `stores/notifications.ts`, `lib/notifications.ts`, `NotificationDto`
  type, a locale-aware `timeAgo` helper in `lib/format.ts`, and `notifications.*` strings (en + id).
  Consumes `GET /api/v1/notifications` + `POST /api/v1/notifications/{id}/read` (no backend change).
- Frontend builds clean (`vue-tsc --noEmit` + `vite build`).

> Note: `CHG-0105` (returns detail pages) and `CHG-0106` (draft/create exactly-once) are on sibling
> branches merging concurrently; this entry uses `CHG-0107`. Reconcile ordering on merge.
## [2026-07-06 15:00:41 UTC]

CHG-0107 — Frontend: surface back-dating on the Inventory Adjust/Opname forms

- **Reject reasons are now visible.** The Adjust/Opname modal previously showed a generic axios error
  (e.g. "Request failed with status code 409") on a business-rule failure. It now shows the server's
  message — so ADR-0033 back-dating rejects read as their intent (e.g. "Back-dating this movement
  would drive stock negative for a later movement", the cross-transfer and closed-period rejects).
- New shared `apiErrorMessage(error, fallback)` helper in `lib/api.ts` extracts
  `response.data.message` from a rejected request (business failures are 4xx, so axios rejects before
  `unwrap` runs); wired into the Inventory view.
- **Back-dating guidance** on both date fields: a persistent hint that an earlier date recomputes the
  moving-average cost of later movements, plus a live amber warning when the chosen date is in the
  past. `inventory.backdate.*` + `inventory.actionFailed` strings in **en** and **id**.
- Frontend builds clean (`vue-tsc --noEmit` + `vite build`). Verified against the running API that a
  back-dated reject returns the human-readable `message` the modal now displays.

---

## [2026-07-06 14:46:55 UTC]

CHG-0106 — Docs: restore lost CHG-0105 entry; de-duplicate STATUS snapshot line

- **Restored `CHG-0105`** below — its entry was dropped resolving the CHANGELOG conflict when the
  return-detail-pages branch (PR #13) merged after the three concurrent branches (PRs #10–#12); it is
  re-inserted in its correct chronological slot (historical entries are immutable — this repairs an
  accidental loss, as CHG-0101 did for CHG-0098).
- **STATUS.md** — collapsed three stacked `As of:` lines (left by the same concurrent merges) back to
  a single snapshot line (`CHG-0106`).
- Documentation only; no code or schema impact.

---

## [2026-07-06 13:15:00 UTC]

CHG-0105 — Frontend: standalone return-detail pages (Sales + Purchasing)

- **New read-only detail pages** for sales credit notes and purchase debit notes
  (`SalesReturnDetailView`, `PurchaseReturnDetailView`): header with number + credit/debit-note +
  posted badges, the party and return date, a line table (qty, unit price, tax %, restock/de-stock
  cost, line total), subtotal/tax/grand-total plus the at-cost restock/de-stock figure, and notes.
- **Return list rows are now clickable** (`clickable` + `rowClick`) and route to the detail; a source
  **Invoice PDF** download and a link to the originating **Sales/Purchase order** are provided.
- New API-client calls `salesApi.getReturn` / `purchasingApi.getReturn` (`GET /sales-returns/{id}`,
  `/purchase-returns/{id}`), detail types `SalesReturn`/`PurchaseReturn` (+ line types), two routes,
  and `returns.detail.*` strings in the **en** and **id** locales.
- Frontend builds clean (`vue-tsc --noEmit` + `vite build`). Verified the detail endpoint's DTO shape
  matches the new types against the running API (created a real sales return and fetched it). Completes
  the Returns UI (the last STATUS follow-up for returns).

---

## [2026-07-06 12:20:00 UTC]

CHG-0104 — Back-dated in-period inventory recompute (ADR-0033, Option A)

- **Implements ADR-0033.** Posting a stock movement dated before an existing movement for the same
  `(Company × Warehouse × Product)` now replays the whole cost bucket in chronological order:
  running quantity/average and every later issue's cost are recomputed and **restated in place** (the
  running snapshot is a rebuildable projection, ADR-0014), and the bucket is set to the recomputed
  final state.
- **GL stays correct without touching posted journals.** The COGS/variance change of already-posted
  later issues is corrected by **one net adjusting journal** — `Dr/Cr Inventory ↔ COGS/Variance`,
  accounts resolved via the posting-rule engine (ADR-0024) — dated at the back-dated movement, so the
  GL period guard rejects a back-date into a closed period. Original journals remain immutable
  (ADR-0009).
- **Scope (v1):** the cross-module paths that carry a GL context — Goods Receipt, Delivery,
  Adjustment, Opname, Returns. **Manual Receive and Transfer reject** back-dating (`BR-INV-4`); a
  back-date before a later **transfer/production** movement (cross-bucket cost cascade, `BR-INV-5`) or
  one that would drive a later movement negative (`BR-INV-3`) is rejected.
- **New:** `MovingAverageReplay` (pure replay engine), `InventoryTransaction.Restate`,
  `StockCostBucket.SetState`, chronological + max-date repository queries, `IInventoryLedger.IsBackDatedAsync`.
  `InventoryLedgerService` gains the recompute path + the delta journal (posting-rule resolved).
- **Tested:** the ADR worked example as pure-replay unit tests; a ledger-level test asserting the
  Dr Inventory / Cr COGS 60 000 correction; full suite green (unit + 35 architecture-fitness + 28
  PostgreSQL integration). **Verified live:** a back-dated cheaper receipt recomputed a later issue and
  left on-hand 140 @ 9 000 = 1 260 000 with the trial balance balanced.
- Docs: `DECISIONS.md` (ADR-0033 accepted, PR #11), `INVENTORY_DESIGN.md` §5, `MODULES.md`, `STATUS.md`.

> Note: `CHG-0102`/`CHG-0103` are reserved by the concurrent idempotency (PR #10) and ADR-0033 design
> (PR #11) branches; this entry uses `CHG-0104`. Merge order: #10 → #11 → this.
## [2026-07-06 11:42:49 UTC]

CHG-0103 — ADR-0033 accepted: back-dated in-period inventory recompute (design only)

- **Decision recorded (no code yet).** Accepted [ADR-0033](docs/adr/0033-inventory-backdated-recompute.md)
  giving effect to ADR-0017's "in-period back-dating triggers a forward recompute": **Option A —
  forward-replay the affected moving-average cost bucket and post one net delta adjusting journal**
  for the COGS/inventory-value difference, never editing posted journals (ADR-0009) or immutable
  ledger facts. Bounded to the open period (ADR-0010/0017); the running snapshot columns are treated
  as a rebuildable projection (ADR-0014); adjusting accounts resolve via the posting-rule engine
  (ADR-0024); replay + adjustments commit atomically and are serialized on the bucket RowVersion
  (ADR-0021).
- **Scope note:** v1 is single-bucket replay; a back-date landing before a stock transfer out of the
  bucket is rejected (cross-bucket cascade is a later enhancement).
- Files: `docs/adr/0033-inventory-backdated-recompute.md` (new), `docs/DECISIONS.md`. Documentation
  only — implementation to follow under a subsequent CHG.

> Note: `CHG-0102` is reserved by the concurrent idempotency exactly-once change (PR #10); this
> entry uses `CHG-0103` to avoid a duplicate id across the two in-flight branches.
## [2026-07-06 10:48:44 UTC]

CHG-0102 — Idempotency exactly-once for atomic posting flows (ADR-0021)

- **Closed the crash-window double-post gap.** The idempotency key is now written **inside the same
  database transaction as the business effects** for every command that commits through the
  cross-module coordinator (`CrossModuleUnitOfWork`): Goods Receipt, Purchase/Sales Invoice,
  Supplier/Customer Payment, Purchase/Sales Return, Delivery Order, Expense post, and stock
  Adjust/Opname. Previously the key was saved on a separate connection *after* commit, so a crash in
  the window between the two could let a retry re-execute and double-post.
- **Mechanism.** New per-request `IIdempotencyScope` (`AmbientIdempotencyScope`) carries the scoped
  key from `IdempotencyBehavior` to the coordinator, which persists it via the new
  `IIdempotencyStore.WriteInTransactionAsync` on the shared connection/transaction before commit and
  marks the scope written; the behavior then skips its legacy separate-connection `SaveAsync`.
  Concurrent replays serialize on the `platform.IdempotencyKeys` primary key (`ON CONFLICT DO
  NOTHING`): the loser rolls back its own effects and returns the winner's id.
- **Unchanged when no key is present** — every non-idempotent path is byte-for-byte identical. The
  per-module create/draft commands (Create SO/PO, Create Expense Draft, stock Receive) keep the
  at-least-once fallback (a crash there can only duplicate a cancellable *draft*, no GL/ledger effect).
- **Tests.** Extended `IdempotencyBehaviorTests` (key published to the scope; no double-write when the
  coordinator already persisted it). Full suite green (unit + architecture-fitness + PostgreSQL
  integration). Verified live: two identical POSTs sharing an `Idempotency-Key` returned the same id
  and created exactly one document.
- Files: `Idempotency.cs`, `IdempotencyStore.cs`, `AmbientIdempotencyScope.cs` (new),
  `CrossModuleUnitOfWork.cs`, `IdempotencyBehavior.cs`, `Program.cs`; docs: `DECISIONS.md` (ADR-0021),
  `STATUS.md`.

---

## [2026-07-05 15:00:00 UTC]

CHG-0101 — Docs sync: restore lost CHG-0098 entry; refresh STATUS.md & MODULES.md

- **Restored `CHG-0098`** in this file — its header was dropped and its body absorbed into the
  `CHG-0099` entry during a branch-merge conflict; split back into its own entry in the correct
  chronological slot (historical entries are immutable — this repairs an accidental loss).
- **STATUS.md** snapshot refreshed from CHG-0094 → CHG-0100: now reflects the live VPS deployment
  (owner's Nginx + Let's Encrypt SAN cert, reused dockerized PostgreSQL), auto-deploy CI/CD, nightly
  backups, the Expenses draft/reversal feature + UI, and the General Ledger filter fix; test count and
  "Latest" narrative updated.
- **MODULES.md** — Expenses row + detail updated for the draft workflow (create/edit/submit/cancel) +
  reversal; the ADR-0029 CRUD note corrected (draft edit/cancel now implemented for Sales/Purchasing
  and Expenses, not "planned").
- **API_SPEC.md** — illustrative resource catalog gains the previously-omitted **Expenses** group
  (`/expense-categories`, `/expense-vouchers` + submit/cancel/reverse/draft) and `/reports/general-ledger`.
- Documentation-only; no code or schema impact.

---

## [2026-07-05 14:20:00 UTC]

CHG-0100 — Ops: automated nightly DB backups + auto-deploy CI/CD

- **Auto-deploy:** refactored the GitHub Actions into three workflows. A reusable **`test.yml`**
  (`workflow_call`: backend `dotnet build/test` against a Postgres service + frontend
  typecheck/build) is now the single quality gate. **`ci.yml`** runs it on pull requests.
  **`deploy.yml`** now triggers **automatically on every push to `main`** (and still supports the
  manual `workflow_dispatch` with a `tag` input): it runs `test` as a hard gate, then builds+pushes
  the API/SPA images to GHCR, then SSH-deploys to the VPS (`docker compose pull` + `up -d`). Broken
  code can't ship because the test job must pass first; the `production` environment can add a
  required-reviewer approval to turn auto-deploy into approve-then-deploy.
- **Backups:** new [`scripts/backup-postgres.sh`](scripts/backup-postgres.sh) — `pg_dump`s each
  database (`accountrack`, `postgresdb`) from the dockerized `postgres` container to gzipped,
  timestamped files (atomic `.partial` → rename), with configurable retention (default 14 days) and
  old-dump pruning. `DEPLOYMENT.md §8` documents the one-time `chmod` + nightly cron
  (`CRON_TZ=Asia/Jakarta`, 02:00), the restore command, and offsite/PITR follow-ups.
- **Docs:** `DEPLOYMENT.md §5` updated for the reusable-test + auto-deploy topology; §8 rewritten
  from aspirational to the concrete backup/restore runbook.
- No application code or schema change.

---

## [2026-07-05 14:05:00 UTC]

CHG-0099 — Fix: General Ledger account filter (frontend)

- The GL report's **account filter now applies on selection** (a `watch` on the selected account
  reloads the report), instead of silently requiring the separate "Apply" button — the behaviour that
  read as "the filter doesn't work". Dates keep the explicit Apply button (so typing a date doesn't
  fire a request per keystroke).
- The account dropdown **no longer hides control accounts** — it previously filtered to
  `allowPosting` accounts, which would exclude AR/AP/Inventory; a ledger must let you drill into any
  account. All accounts are now selectable.
- Fixed the default date range to format both bounds from the **local** calendar (it mixed a local
  `monthStart` with a UTC `toISOString()` `today`, which could invert the range near midnight/month
  boundaries and render an empty ledger).
- Backend was verified correct end-to-end (account + date filtering, incl. through the SPA nginx
  proxy); this was a frontend-only issue. Frontend typecheck clean.

---

## [2026-07-02 18:05:00 UTC]

CHG-0098 — CI/CD: GitHub Actions (CI on PR/main + manual GHCR deploy to VPS)

- **`.github/workflows/ci.yml`** (automatic, on PR + push to `main`): backend `dotnet build -c Release`
  (warnings-as-errors) + `dotnet test` against a PostgreSQL 16 **service container** (sets
  `ACCOUNTRACK_TEST_PG` so the cross-tenant isolation integration tests run rather than skip); frontend
  `npm ci` + `npm run build` (`vue-tsc --noEmit && vite build`). Concurrency-cancels superseded runs.
- **`.github/workflows/deploy.yml`** (manual `workflow_dispatch`, image `tag` input): builds the API
  (`Dockerfile.api`) and SPA (`./frontend`) images and pushes them to **GHCR**
  (`ghcr.io/<owner>/accountrack-api` + `-web`, tagged with the input tag and the commit SHA, Buildx
  GHA layer cache); then a `deploy` job (GitHub **`production`** environment, for optional required-
  reviewer approval) SSHes into the VPS and runs `docker compose pull` + `up -d` for the two services.
  The VPS **builds nothing** — it pulls ready images (protects the RAM-tight box). EF migrations apply
  on API restart.
- **Docs:** `DEPLOYMENT.md §5` rewritten from aspirational to the concrete implemented pipeline —
  required repo secrets (`VPS_HOST` / `VPS_USER` / `VPS_SSH_KEY` / `VPS_APP_DIR`), switching the VPS
  compose from `build:` to `image:` refs with an `ACCOUNTRACK_TAG` pin, making GHCR packages public
  or logging in with a `read:packages` PAT, and deploy/rollback steps. `VPS_DEPLOYMENT_GUIDE.md` gains
  §14, a concise "enable automated deploys" activation checklist.
- No application code or schema change.

---

## [2026-07-02 17:30:00 UTC]

CHG-0097 — Docs: VPS deployment guide (deploy alongside an existing Docker stack)

- New [docs/VPS_DEPLOYMENT_GUIDE.md](docs/VPS_DEPLOYMENT_GUIDE.md) — a complete, reproducible runbook
  for deploying Accountrack onto a VPS that already runs a Docker Compose stack with its own
  PostgreSQL and an Nginx reverse proxy (e.g. alongside n8n). Covers, end to end: merging the two
  `accountrack-*` services into the existing compose (reusing the existing `postgres` via a dedicated
  role/database + the `api` network alias); cloning the repo with the exact-path/case gotchas;
  creating the DB role/database; writing and locking down `.env` (`chmod 600`, `docker compose config`
  check); the DNS A record; expanding the existing multi-domain (SAN) Let's Encrypt cert with
  `certbot --standalone --expand` and persisting stop/start renewal hooks; adding the Nginx server
  block (shared cert, reload-order caveat); build/up, verify, and turning off re-seeding; updating to
  a new version; inspecting the VPS database from local pgAdmin over an SSH tunnel; and a
  troubleshooting table of every issue encountered during a real deployment. Linked from the docs index.
- Documentation-only change; no code or schema impact.

---

## [2026-07-02 16:20:00 UTC]

CHG-0096 — Expenses UI (draft workflow + reversal) and end-to-end guide

- **Frontend (Vue):** the Expenses screen now matches the Sales/Purchasing pattern.
  - The voucher list is clickable; rows open a detail page.
  - New `ExpenseVoucherCreateView` — a create/edit form with **Save as draft** and **Save & post**
    (edit mode reuses it via `?edit=<id>`).
  - New `ExpenseVoucherDetailView` — lines, totals, payment/journal metadata, and status-aware
    actions: **Edit / Submit / Cancel** (Draft) and **Reverse** (Posted, with a date + reason
    dialog). Buttons are permission-gated (`Expenses.Edit/Cancel/Post`).
  - API client (`lib/expenses.ts`) + types extended (get/createDraft/update/submit/cancel/reverse,
    `ExpenseVoucher` detail incl. `reversalJournalEntryId`); router routes `expenses/new` and
    `expenses/:id`; `StatusBadge` gains a `Reversed` tone; EN + ID translations added.
  - Typecheck clean; production build succeeds.
- **Docs:** new [docs/END_TO_END_GUIDE.md](docs/END_TO_END_GUIDE.md) — a click-through walkthrough of a
  full business cycle (master data → purchasing → sales → expenses → inventory), a document→journal
  cheat-sheet, how to read every financial report, integrity checks, an API smoke-test sequence, and
  common gotchas. Linked from the docs index.

---

## [2026-07-02 15:05:00 UTC]

CHG-0095 — Expense vouchers: draft workflow + reversal (full parity with Sales/Purchasing)

- **Draft lifecycle** added to the Expenses module so a voucher can be entered, reviewed, and
  corrected before it touches the GL, matching how Sales Orders / Purchase Orders already work:
  - `POST /api/v1/expense-vouchers/draft` — create a **Draft** (no approval, no journal).
  - `PUT /api/v1/expense-vouchers/{id}` — edit a Draft's header + lines (Draft-only).
  - `POST /api/v1/expense-vouchers/{id}/submit` — run approval; auto-approve → post atomically,
    else hold as PendingApproval (returns "Posted"/"Pending").
  - `POST /api/v1/expense-vouchers/{id}/cancel` — discard a Draft.
  - The existing one-shot `POST /api/v1/expense-vouchers` (record + auto-post) is unchanged, so the
    quick-entry path and its tests/UI keep working.
- **Reversal** of a posted voucher (`POST /api/v1/expense-vouchers/{id}/reverse`): posts a mirror
  journal (debits ↔ credits) dated on the reversal date, moves the voucher to **Reversed**, and
  leaves the original journal intact (posted docs immutable — BR-EXP-4). An on-account voucher is
  reversible only while its AP open item is **fully unpaid** (else `EXPENSES.REVERSAL_HAS_PAYMENTS`);
  a successful reversal settles the payable. Verified end-to-end: post+reverse nets to zero on every
  account balance and the trial balance stays balanced.
- **Domain:** `ExpenseVoucherStatus` gains `Reversed`/`Cancelled`; new `EditDraft`, `Cancel`,
  `MarkReversed`; new `ReversalJournalEntryId` column (migration `AddExpenseVoucherReversal`) exposed
  on `ExpenseVoucherDto`. Poster refactored to share journal-line assembly between post and reverse.
- **RBAC:** new catalog permissions `Expenses.Create` / `Expenses.Edit` / `Expenses.Cancel` (granted
  to Administrator automatically and added to the Accountant role); submit/reverse stay under
  `Expenses.Post`. Segregation of duties preserved (create ≠ post).
- **Rules:** BR-EXP-4 expanded (reversal mechanics + AP-unpaid guard); new **BR-EXP-7** (draft
  workflow). Tests: 5 new Expenses unit tests (reversal + draft guards); full unit/architecture
  suites green.

---

## [2026-07-02 11:20:00 UTC]

CHG-0094 — Migrate database provider from SQL Server to PostgreSQL (ADR-0032)

- Replaced `Microsoft.EntityFrameworkCore.SqlServer` with `Npgsql.EntityFrameworkCore.PostgreSQL`
  (8.0.10) in all 13 Infrastructure projects + the integration-test project; swapped every
  `UseSqlServer(...)` to `UseNpgsql(...)` across module DI and design-time factories. Kept EF Core
  and the relational model — the switch is confined to the Infrastructure layer (Domain/Application/
  API unchanged).
- **Concurrency token:** SQL Server `rowversion` has no PostgreSQL equivalent, so the per-entity
  `byte[] RowVersion` is now a provider-agnostic `bytea` `IsConcurrencyToken()` whose value is bumped
  by `AuditingSaveChangesInterceptor` on every insert/update. The `byte[]` shape and the opaque
  client token contract are unchanged (amends ADR-0021).
- **Platform stores** `IdempotencyStore`/`InboxStore` rewritten from T-SQL to PostgreSQL
  (`NpgsqlConnection`, `CREATE … IF NOT EXISTS`, `INSERT … ON CONFLICT DO NOTHING`, `now()`); the
  shared cross-module connection uses `NpgsqlConnection`.
- **Migrations regenerated** per module for Npgsql (greenfield, no production data): `uuid`,
  `numeric(p,s)`, `timestamp with time zone`, `bytea`. Soft-delete partial unique indexes converted
  to PostgreSQL syntax (`"IsDeleted" = false`). Identifier casing kept PascalCase (quoted).
- **Docker/config:** `docker-compose.yml` + `docker-compose.dev.yml` now run `postgres:16-alpine`
  (named volume, `pg_isready` healthcheck, `restart: unless-stopped`, internal network, not
  published); connection strings, `.env.example`, and `appsettings.Development.json` switched to
  Npgsql format. Integration-test fixture repointed to PostgreSQL.
- **Validated** against local PostgreSQL 18: build (warnings-as-errors) + all unit/architecture tests
  pass; all 28 integration tests (incl. cross-tenant isolation) pass against a live DB; all 12
  module migrations apply cleanly (73 tables / 13 schemas).

---

## [2026-07-01 16:29:49 UTC]

CHG-0093 — Local development Docker stack (docker-compose.dev.yml)

- Added `docker-compose.dev.yml`: a self-contained local stack (its **own** SQL Server container +
  API + SPA) that runs the whole app in Docker on `localhost`, isolated from the host SQL Server
  (stays on 1433) and the dev servers (:5080/:5173). Uses a containerised DB because the dev
  connection is Windows-auth (`Trusted_Connection`), which a Linux container can't use.
- Ports bound to `127.0.0.1`: SPA `:8090`, API `:8081` (Swagger — `ASPNETCORE_ENVIRONMENT=Development`),
  SQL `:1434` (`sa`). First boot auto-migrates + seeds a working company + admin. Secrets come from a
  gitignored `.env` (dev defaults documented; nothing must be changed to run locally).
- **Verified on local Docker:** `docker compose -f docker-compose.dev.yml up -d --build` → DB healthy →
  API migrated + seeded → SPA + Swagger reachable → login (`admin@accountrack.local`) works → 21 GL
  accounts + PPN11 seeded.

---

## [2026-07-01 16:10:42 UTC]

CHG-0092 — Docs: integrate Accountrack into an existing reverse-proxy compose

- Added `docs/DEPLOYMENT.md §0.1` — how to drop the Accountrack services (`accountrack-db` /
  `accountrack-api` / `accountrack-web`) into an **existing** docker-compose stack that already has an
  Nginx TLS reverse proxy serving other subdomains (e.g. n8n). The web container uses `expose` (not a
  published port) so the existing Nginx proxies to it by name; includes the service block, the
  `accountrack_mssql` volume, the `.env` keys, an Nginx `server` block for the subdomain, and a note
  that SQL Server needs ~2 GB RAM (VPS should have ≥ 4 GB total). Docs-only; no code change.

---

## [2026-07-01 16:01:01 UTC]

CHG-0091 — Docker deployment stack (self-hosted, single VPS)

- Added the deployment artifacts the app previously lacked, so it can actually be shipped:
  `Dockerfile.api` (multi-stage .NET 8 publish → non-root ASP.NET runtime), `frontend/Dockerfile`
  + `frontend/nginx.conf` (build the SPA, serve it, reverse-proxy `/api` to the API — same origin,
  no CORS), `docker-compose.yml` (SQL Server + api + web on an internal network with a persistent
  DB volume), `.env.example`, `.dockerignore`s, and `appsettings.Production.json` (quieter logging).
- **Reverse-proxy-friendly:** the stack does **not** terminate TLS or bind 80/443 — only the `web`
  container publishes one port (`WEB_PORT`, default 8090) for an existing external proxy to forward a
  subdomain to. Fits a VPS already hosting other apps behind Nginx Proxy Manager / Traefik / Caddy.
- **First-boot provisioning:** with `SEED_ENABLED=true` the API migrates every module schema and
  seeds the permission catalog, standard roles, a **working company** (chart of accounts, posting
  rules, PPN 11%, system accounts) and the initial administrator from `ADMIN_EMAIL`/`ADMIN_PASSWORD`.
- **Security hardening:** the app now **refuses to start outside Development** if `Jwt:SigningKey` is
  missing or `< 32` chars (previously it silently fell back to an all-zero key). All secrets come from
  env / `.env` (gitignored); no secrets baked into images; containers run non-root.
- **Verified locally with Docker:** `docker compose up -d --build` → SQL Server healthy → API migrated
  + seeded → SPA served and `/api` proxied on one port → login as the seeded admin succeeds
  (Administrator) → the company has **21 GL accounts + PPN11** seeded (usable for posting).
- Runbook added to `docs/DEPLOYMENT.md §0`. Known follow-up: public `/register` sign-up does not yet
  provision a new company's accounting (use the seeded company); formal Prod pipelines should apply
  migrations as a discrete, backed-up step rather than auto-migrate on boot.

---

## [2026-07-01 15:17:37 UTC]

CHG-0090 — Frontend silent refresh-token rotation (no surprise logouts)

- The SPA previously discarded the refresh token and **bounced the user to login the moment the access
  token expired**. Now the access token is refreshed silently in the background: on a `401` for a
  normal request, the axios interceptor exchanges the stored **refresh token** at `POST /auth/refresh`,
  persists the rotated pair, and **retries the original request** — the user never sees a logout. Only
  when there is no valid refresh token (both expired) does it clear the session and route to login.
- **Rotation-safe under concurrency:** a single in-flight refresh is shared across simultaneous `401`s,
  and a request that 401s after another already rotated the token just retries with the current token —
  so the (single-use) refresh token is never spent twice, which would otherwise revoke the whole
  token family.
- **Proper logout:** sign-out now calls `POST /auth/logout` to **revoke the refresh token server-side**
  (best-effort) before clearing locally; the refresh token is stored/cleared alongside the access token.
- Backend already issued rotating refresh tokens — no backend change. Frontend builds (vue-tsc + vite);
  suite remains **329** green. **Verified (e2e):** a 401 → `/auth/refresh` returns a **rotated** pair →
  retry succeeds (200); reusing the old refresh token is rejected (401); logout revokes it.

CHG-0089 — List export honors active filters (ADR-0031)

- **Export now reflects the active search/filter.** Previously "Export" always downloaded the whole
  list regardless of what the user had filtered to. Now every list export sends exactly the rows the
  table is currently showing — after the client-side search — so the CSV/XLSX contains only those rows.
- **Mechanism (no per-entity backend work, no new frontend dependency):** `DataTable` exposes its
  filtered rows via `v-model:filtered`; a new `exportTable(columns, rows, name, format)` helper posts
  the visible columns + filtered rows to a single generic **`POST /api/v1/export`** endpoint, which
  renders them to CSV/XLSX through the existing ClosedXML/CSV `TableExport` path. The endpoint only
  formats the caller's own already-loaded data (nothing new is exposed) and is auth-gated.
- Wired across all 8 list views with an export button: **customers, suppliers, warehouses, products,
  sales orders, purchase orders, inventory on-hand, expenses**. Columns exported match the on-screen
  table (raw values — numbers/enums — which suit data export). The former per-entity `*/export`
  endpoints remain but are no longer called by the UI.
- Frontend builds (vue-tsc + vite); backend suite **329** green. **Verified (e2e):** `POST /export`
  returns exactly the posted rows as CSV, a valid XLSX (PK zip) for `format=xlsx`, and 401 without a
  token.

CHG-0088 — Settings tabs + modal overflow fix (UX)

- **Modal overflow fixed (all modals).** `AppModal` capped its panel at the viewport
  (`max-h-[calc(100dvh-2rem)]`) as a flex column with a **scrollable body** and pinned header/footer.
  Previously a tall modal (notably **Add/Edit role**, with its full permission matrix) grew past the
  screen and pushed the Save/Cancel footer out of reach. Because every dialog in the app shares
  `AppModal` (17 call sites), this one change fixes them all. Added an optional `size` prop
  (`sm`/`md`/`lg`/`xl`); the role modal now uses `lg` so the permission matrix has room.
- **Settings reorganized into tabs.** The stacked cards (Company, Users, Roles, Event delivery,
  Profile, Preferences) are now a tab bar, one tab per category, matching the Master-Data tab style.
  Admin-only tabs (Users, Roles, Event delivery) appear only with the matching permission; selecting a
  tab mounts just that section. No new i18n — tab labels reuse the existing section titles (EN/ID).
- Frontend builds (vue-tsc + vite). No backend change; suite remains **329** green.

CHG-0087 — Optimistic concurrency on master-data & Chart-of-Accounts edits (ADR-0021)

- Extended the cross-request lost-update guard from CHG-0086 to **all master-data edits**
  (customers, suppliers, warehouses, products, units of measure, product categories, tax codes) and
  **Chart-of-Accounts** edits. Each edit screen now round-trips the record's `rowVersion`; a stale
  save is rejected with **409 `CONCURRENCY_CONFLICT`** rather than silently overwriting another user's
  change. The 409 mapping + `ConcurrencyConflictException` infrastructure from CHG-0086 is reused.
- **Backend leverage:** the generic `ICodedRepository<T>` gained one `SetExpectedVersion` method that
  covers all seven master-data aggregates; the `IAccountRepository` got the same. Each `Update*`
  command takes an optional `RowVersion` (omitted = unchanged behaviour), and every list DTO now
  exposes `RowVersion` so the client has the token to echo back.
- **Frontend:** a shared `isConflict(error)` helper keys off the **`CONCURRENCY_CONFLICT` error code**
  (not the 409 status) so a duplicate-code conflict on *create* — which is also 409 — is not
  mislabelled as "changed by someone else". All seven master-data views + the CoA view capture the
  loaded `rowVersion`, send it on update, and show a localized conflict message (EN/ID).
- **Tests:** +2 (customer + account update handlers set the expected version when supplied). Full
  suite **329** green; frontend builds.
- **Verified (e2e):** for both a Customer and a (newly created) GL account — editing with the loaded
  version succeeded and advanced it; re-editing with the stale version returned **409
  CONCURRENCY_CONFLICT**.

---

## [2026-06-30 16:02:21 UTC]

CHG-0086 — Optimistic concurrency on Sales & Purchase Order edits (ADR-0021)

- Editing a draft Sales Order or Purchase Order is now **guarded against lost updates across
  requests**. The detail/edit payload carries the row's concurrency token (`rowVersion`); on save the
  client echoes back the version it loaded, and if another user changed the draft in the meantime the
  update is rejected with **409 `CONCURRENCY_CONFLICT`** instead of silently overwriting their work.
  (EF Core's rowversion already guarded the in-request load→save window; this adds the cross-request
  check that matters when two people edit the same draft.)
- **Provider-agnostic translation:** a new `ConcurrencyConflictException` (SharedKernel) is thrown by
  `BaseDbContext.SaveChangesAsync` when EF raises `DbUpdateConcurrencyException`, and the web
  middleware maps it to a 409 with code `CONCURRENCY_CONFLICT` — so the Application layer never
  references EF (ADR-0023). The dispatcher/attempt-cap constant lives in one place via the existing
  `RowVersion` token already mapped on every entity.
- The update commands accept an optional `RowVersion`; when omitted, behaviour is unchanged (no
  cross-request check), so existing API callers are unaffected. `SalesOrderDto`/`PurchaseOrderDto`
  expose `RowVersion`; the repositories gained `SetExpectedVersion(...)`.
- **Frontend:** the SO/PO edit screens capture the loaded `rowVersion`, send it on update, and surface
  a clear "changed by someone else — reload" message on 409 (EN/ID).
- **Tests:** +4 (Sales & Purchasing update handlers set the expected version when supplied, skip it
  otherwise). Full suite **327** green; frontend builds.
- **Verified (e2e):** loaded a draft SO (v1) → edit with v1 succeeded and advanced to v2 → re-editing
  with the stale v1 returned **409 CONCURRENCY_CONFLICT** → editing with the fresh v2 succeeded again.

---

## [2026-06-25 14:36:33 UTC]

CHG-0085 — Frontend: outbox dead-letter panel in Settings

- Surfaced the dead-letter endpoints from CHG-0084 in the UI: a new **"Event delivery"** card in
  **Settings** (shown only with `Approval.Manage`) lists integration events the dispatcher gave up on
  — event name, when it occurred, attempt count, and the last error — with a **Retry** action per row
  that requeues the event; on success the row drops out and the list refreshes after the dispatcher
  has had time to redeliver (~2.5s). A healthy/empty state ("All events delivered — nothing failed")
  is shown when there is nothing to triage.
- New `OutboxDeadLetters.vue` (matching the existing `UsersManager`/`RolesManager` settings pattern),
  `approvalApi.deadLetters()` + `retryDeadLetter()`, the `DeadLetterEvent` type, and EN/ID strings
  under `settings.outbox.*`.
- Frontend builds (vue-tsc + vite). Verified against the running API: a seeded dead-lettered
  `ApprovalDecided` is returned by the list endpoint and clears after Retry.

---

## [2026-06-25 11:53:44 UTC]

CHG-0084 — Outbox dead-letter visibility + retire in-process publisher (ADR-0007)

- **Dead-letter triage for the outbox.** When the dispatcher exhausts its retry cap (10 attempts) a
  message was silently stranded with no way to see or recover it. Two new **`Approval.Manage`-gated**
  endpoints close that gap: `GET /api/v1/approval/outbox/dead-letter` lists stranded events for the
  current tenant (friendly event name, occurred-at, attempts, last error), and
  `POST /api/v1/approval/outbox/dead-letter/{id}/retry` requeues one (resets attempts, clears the
  error) so the dispatcher redelivers it on the next pass. Both are tenant-scoped in the repository —
  the outbox table itself stays unfiltered so the background dispatcher keeps draining every tenant.
- **Retired the now-dead in-process publisher.** With CHG-0083 every integration event flows through
  the durable outbox, so `IIntegrationEventPublisher`/`IntegrationEventPublisher` (and its DI
  registration) had no callers and a stale "outbox is a later hardening" comment. Removed. The
  consumer side (`IIntegrationEventHandler<T>`, resolved by the dispatcher) is unchanged.
- **Shared attempt cap.** Extracted `OutboxDefaults` (BatchSize/MaxAttempts/PollInterval) into
  `Application.Abstractions` so the dispatcher and the dead-letter view agree on one definition of
  "dead-lettered" (`Attempts >= MaxAttempts`) instead of a private dispatcher constant.
- **Tests:** +3 (`OutboxAdmin` handlers: list maps the assembly-qualified type to a bare event name and
  passes the shared cap; retry succeeds when requeued; retry returns not-found otherwise). Full suite
  **323** green; arch-fitness still green after the publisher removal.
- **Verified (e2e):** seeded a dead-lettered `ApprovalDecided` (Attempts 10); the list endpoint
  returned it as `ApprovalDecided` with its error; retry returned 200, and ~2s later the dispatcher
  redelivered it (ProcessTracker recorded one "Approved" milestone), the row flipped to `PROCESSED`
  (Attempts 0, Error cleared), and the dead-letter list went empty.

---

## [2026-06-24 15:50:02 UTC]

CHG-0083 — Durable transactional outbox for approval events (ADR-0007)

- Approval events (`ApprovalSubmitted`, `ApprovalDecided`) are no longer dispatched in-process,
  best-effort. They are now **staged in a transactional outbox** that commits in the **same database
  transaction** as the approval request/decision (`approval.OutboxMessages`), so an event can never be
  lost if a downstream consumer or the process fails after the decision is saved. This closes the
  "no outbox yet" caveat called out in CHG-0082 — expense posting-on-approval is now durable.
- A background **`OutboxDispatcherService`** polls pending rows (batch 50, 2s interval, 10-attempt cap)
  and delivers each message in its **own DI scope** so per-message tenant + module contexts stay
  isolated. Delivery is **at-least-once**; a new platform de-dup store (`platform.InboxState`, keyed by
  `(Handler, EventId)`) makes it **effectively exactly-once per handler** so retries never double-apply
  the append-only ProcessTracker/Notification consumers (no consumer code changed).
- Background delivery has no HTTP principal, so a settable **`IAmbientTenant`** restores the
  originating tenant/company per message; the request-time `ITenantContext` now falls back to it when
  there is no HTTP request, keeping tenant stamping/filtering correct off the request path.
- Reflection-invoked handler failures are unwrapped (`TargetInvocationException` → inner) so the
  recorded outbox error is the real cause, not the wrapper.
- **Migration** `ApprovalOutbox` (creates `approval.OutboxMessages`); `platform.InboxState` is created
  at startup like the idempotency store. Both use their own connection, never the cross-module tx.
- **Tests:** +6 (`OutboxProcessor`: delivers + restores tenant + marks processed; skips a handler
  already in the inbox; records failure without marking processed on a throwing handler; unknown event
  type; multiple handlers each applied once; plus the Approval handler tests now assert the decision is
  enqueued). Full suite **320** green.
- **Verified (e2e):** submitting a document for approval returns immediately with an **empty timeline**
  (delivery is async); ~1s later the dispatcher records exactly **one** "Auto-approved" milestone, and
  it stays at one across subsequent 2s polls. The outbox row is marked `PROCESSED` (Attempts 0) and
  `platform.InboxState` holds one row per consumer (ProcessTracker + Notification) for that event id.

---

## [2026-06-24 15:03:19 UTC]

CHG-0082 — Expense voucher approvals (threshold-gated posting)

- Expense vouchers now flow through the **approval engine** before posting (ADR-0030, BR-EXP-5). On
  create, the voucher is submitted to the engine with its `Total`: when **no approval definition
  matches** it is **auto-approved and posted immediately** (unchanged behaviour); when a definition
  matches (e.g. a per-amount threshold) it is held as **PendingApproval** with no GL journal, and is
  **posted only once approved**. Rejection marks it **Rejected** and it never posts.
- Voucher gains a lifecycle: `ExpenseVoucherStatus` (Draft → PendingApproval → Posted/Rejected) +
  `ApprovalRequestId`. The GL posting logic is extracted into a shared `IExpenseVoucherPoster` used by
  both the auto-approve path and the new `ApprovalDecided` consumer, so the posting rules live in one
  place. Reuses the existing definitions/conditions — no new threshold concept — so an admin configures
  expense approval from **Approvals** like any other document type.
- **Migration** `ExpenseApprovalStatus` (Status + ApprovalRequestId); existing vouchers backfilled to
  `Posted`. DTOs and the Expenses list now expose **status** with a colour-coded badge (EN/ID).
- **Caveat (no outbox yet):** posting-on-approval runs in the best-effort in-process event consumer; a
  posting failure after approval leaves the voucher PendingApproval for retry rather than rolling back
  the approval (consistent with the rest of the platform until a durable outbox lands).
- **Tests:** +5 (auto-approve posts; a matching rule holds it pending without posting; consumer posts
  on approval, marks rejected, is idempotent on an already-posted voucher, ignores other doc types).
  Full suite **315** green; frontend builds.
- **Verified (e2e):** with a rule "ExpenseVoucher Total ≥ 5,000,000 → Administrator", a 100k voucher
  auto-posted; an 8M voucher stayed PendingApproval (no journal); after an admin (≠ submitter) approved
  it, it posted. Segregation of duties held (clerk submitted, admin approved).

---

## [2026-06-23 14:08:29 UTC]

CHG-0081 — Split master-data permissions into Create / Edit / Delete (+ seeding fix)

- The coarse **`MasterData.Manage`** permission is replaced by distinct **`MasterData.Create`**,
  **`MasterData.Edit`**, **`MasterData.Delete`** (deactivate) across all master-data endpoints
  (products, customers, suppliers, warehouses, units, categories, tax codes) **and the Chart of
  Accounts** — closing the CHG-0071 gap where CoA reused `MasterData.Manage` (ADR-0029, BR-X-7/8,
  segregation of duties). Create→`POST`, Edit→`PUT`, Delete→`PUT …/active`.
- **Frontend:** the shared `RowActions` (edit/deactivate) is gated by `MasterData.Edit` /
  `MasterData.Delete`, and every master-data + CoA **New** button by `MasterData.Create`, so users see
  only the actions they hold.
- **Seeding fix:** `EnsureAdminHasAllPermissions` now scopes to the **dev tenant's** Administrator
  role. With public sign-up, other tenants also have an Administrator role, and the previous
  unfiltered `FirstOrDefault` could grant newly-added catalog permissions to the wrong tenant's admin
  (which is exactly what hid the new master-data permissions from the dev admin until fixed).
- **Tests:** full suite **310** green (incl. integration after the seeder change); frontend builds.
- **Verified (e2e):** a custom **CreatorOnly** role (only `MasterData.Create`) can create a supplier
  but is **403** on edit and on deactivate; the Administrator (after re-seed) holds Create/Edit/Delete
  and can do all three.
- **Note:** the legacy `MasterData.Manage` permission row may persist in existing databases; it is no
  longer referenced by any endpoint and is harmless (the Administrator role shows as “full access”).

---

## [2026-06-22 16:18:25 UTC]

CHG-0080 — Excel (.xlsx) master-data import

- The two-step master-data import (dry-run preview → commit) now accepts **Excel `.xlsx`** files in
  addition to CSV, for all four entities (products, customers, suppliers, warehouses) — handy for a
  freshly signed-up org loading data straight from a spreadsheet (ADR-0031). A new `ExcelReader`
  (Web.Common, ClosedXML) converts the first worksheet to CSV at the API boundary, so the entire
  existing parse/validate/preview/commit pipeline is reused unchanged. Numbers, booleans and dates are
  emitted in an invariant, parser-friendly form; the upload is detected by extension/content-type.
- **Frontend:** the Import picker on each master-data list now accepts `.xlsx` as well as `.csv`
  (messaging is already format-neutral; the CSV template opens directly in Excel).
- **Tests:** +3 (`ExcelReader` converts typed cells — number `0.11`, boolean `true`, integers;
  comma-containing values round-trip via quoting; a blank sheet yields empty). Full suite **310**
  green; frontend builds.
- **Verified (e2e):** exported 12 products to a real `.xlsx`, then re-imported it — preview parsed all
  12 rows with 0 errors and commit updated 12.

---

## [2026-06-22 16:07:02 UTC]

CHG-0079 — Period-close balance snapshots (rebuildable)

- Closing a fiscal period now writes a **`PeriodBalances`** snapshot — each account's cumulative
  debit/credit as of the period end — so month-end positions and opening balances are available
  without re-summing the whole ledger (ADR-0022). The snapshot is **always re-derivable from the GL**:
  `POST /api/v1/fiscal-periods/{id}/balances/rebuild` recomputes it (`Accounting.PeriodClose`), and
  reopening a period **drops** it, so it can never drift. `GET /api/v1/fiscal-periods/{id}/balances`
  reads it (`Accounting.View`). New `PeriodBalance` entity (migration `AddPeriodBalances`) with the
  account code/name denormalized for a stable point-in-time record.
- **Frontend:** the Fiscal Periods screen gains a **Balances** action on closed/locked periods — a
  modal listing the snapshot (account, debit, credit, totals) with a **Rebuild** button. EN/ID.
- **Tests:** +4 (close snapshots non-zero mapped rows & clears any prior; reopen clears; rebuild
  recomputes; unknown period rejected). Full suite **307** green; frontend builds.
- **Verified (e2e):** closing Feb-2026 captured an **11-account, balanced** snapshot
  (Dr = Cr = 12,005,131,064.29); reopening cleared it to 0 rows; rebuilding recomputed a balanced
  snapshot.

---

## [2026-06-21 15:35:20 UTC]

CHG-0078 — Public organization sign-up

- A public, anonymous **`POST /api/v1/auth/register`** lets a new business onboard itself: it
  provisions a **brand-new tenant + first company**, seeds the **6 standard roles** for it (via the
  shared `StandardRoleDefinitions.BuildSystemRoles`), creates the registrant as the tenant's
  **Administrator**, and returns an auth token pair (**auto sign-in**). Cross-module by contract: a
  new `ICompanyProvisioning` (implemented in Company Management) creates the tenant/company; Identity
  seeds roles + the admin user. Email is checked up-front so a tenant is never provisioned without its
  administrator.
- **Frontend:** a public **Sign-up page** (`/register`) — organization + company + currency, then the
  admin's name/email/password — linked both ways with the login page; on success it stores the session
  and lands on the dashboard.
- **Tests:** +2 (register provisions a tenant, seeds all 6 roles, makes the registrant Administrator;
  a taken email is rejected without provisioning anything). Full suite **303** green; frontend builds;
  architecture-fitness still green (Identity.Application → Modules.Contracts only).
- **Verified (e2e):** registering a new org returns an Administrator token with all 34 permissions and
  exactly one company; the new tenant sees **only its own company and zero of the demo tenant's data**
  (multi-tenant isolation); 6 roles are seeded for it; a duplicate email returns 409 `EMAIL_EXISTS`;
  the new admin can sign in again.
- **Known limitation:** the company and the admin/roles are saved in two steps (Identity and Company
  contexts aren't enlisted in the cross-module transaction), so a rare failure between them could
  leave an empty tenant; the up-front email check covers the common case. Atomic provisioning is a
  future hardening.

---

## [2026-06-21 15:10:53 UTC]

CHG-0077 — User management (Settings → Users)

- Admins can now **manage users** end to end (`Admin.Users`): `GET /api/v1/users` (list with role +
  company grants), `PUT /api/v1/users/{id}` (rename + **replace roles and company access**), and
  `PUT /api/v1/users/{id}/active` (enable/disable). Create already existed (`POST /users`). Domain
  gains `User.Rename` / `ReplaceRoles` / `ReplaceCompanies`.
- **Frontend:** Settings → **Users** (visible with `Admin.Users`) — a user list (role chips, active
  state) with a create/edit modal that assigns **roles** (checkboxes) and **company access**, plus an
  enable/disable toggle. EN/ID.
- **Tests:** +3 (update renames & replaces roles/companies; unknown user → `USER_NOT_FOUND`; disable a
  user). Full suite **301** green; frontend builds.
- **Verified (e2e):** created a Sales user → their token carries exactly the Sales permissions (8) and
  not `Accounting.Post`; that user is **forbidden (403)** from `GET /roles` (role-based enforcement
  proven on the negative path); changing the role to Accountant grants `Accounting.Post` on next login;
  disabling the user blocks login (403).
- **Next:** public organization sign-up.

---

## [2026-06-21 15:02:52 UTC]

CHG-0076 — Dynamic roles & access management (Settings → Roles)

- **Standard roles seeded per tenant** with a researched permission matrix (`StandardRoleDefinitions`):
  **Administrator** (full), **Accountant**, **Sales**, **Purchasing**, **Warehouse**, **Viewer**.
  Previously only Administrator existed. A shared `BuildSystemRoles` provisions the same set, ready for
  the upcoming organization sign-up.
- **Roles CRUD API** (`Admin.Roles`): `GET/POST/PUT/DELETE /api/v1/roles` and `GET /api/v1/permissions`
  (the catalog, grouped by module). Domain gains `Role.Rename` + `Role.ReplacePermissions`. Guards:
  the **Administrator** role is immutable (always full access — prevents lock-out); **built-in roles**
  keep their name and can't be deleted (only their permissions are editable); a role assigned to
  users can't be deleted (`ROLE_IN_USE`).
- **Frontend:** Settings → **Roles & access** (visible with `Admin.Roles`) — a role list (built-in
  badge, user/permission counts) with an editor modal whose **permission matrix is grouped by module**
  with per-group select-all, plus create/rename/delete for custom roles. EN/ID.
- **Tests:** +7 (create with permission filtering; duplicate-name; Administrator edit blocked; system
  role keeps name but swaps permissions; delete system/in-use blocked; delete unused custom). Full
  suite **298** green; frontend builds.
- **Verified (e2e):** 6 roles seed with correct permission sets; created/updated/deleted a custom role;
  edited a built-in role's permissions (name preserved); editing Administrator → 409
  `ROLE_IS_ADMINISTRATOR`; deleting a built-in role → 409 `ROLE_IS_SYSTEM`.
- **Next:** user management (assign roles + company access) and public organization sign-up.

---

## [2026-06-21 14:42:01 UTC]

CHG-0075 — Distinct Edit / Cancel permissions for Sales & Purchasing documents

- Editing and cancelling a draft order are now **separate permissions** from creating one (ADR-0019,
  BR-X-8, segregation of duties). `PUT /sales-orders/{id}` now requires **`Sales.Edit`** and
  `POST /sales-orders/{id}/cancel` requires **`Sales.Cancel`** (previously both reused `Sales.Create`);
  the Purchasing equivalents require **`Purchasing.Edit`** / **`Purchasing.Cancel`**. New catalog
  entries `Sales.Cancel`, `Purchasing.Cancel`, `Purchasing.Delete` (the system Administrator role
  auto-grants them on startup).
- **Frontend:** the **Edit** button on a draft order is gated by `*.Edit` and the **Cancel** button by
  `*.Cancel` (in addition to status), so a user without the right is not shown the action.
- **Tests:** full suite **291** green; frontend builds.
- **Verified (e2e):** after seeding, the admin token carries `Sales.Cancel` / `Purchasing.Cancel` /
  `Purchasing.Delete`; cancelling a draft sales order through the now `Sales.Cancel`-gated endpoint
  succeeds (status → Cancelled).
- **Remaining (CRUD governance):** split master-data `MasterData.Manage` into distinct Create/Edit/
  Delete (deactivate) permissions, and give Chart-of-Accounts edit its own permission (today it reuses
  `MasterData.Manage`).

---

## [2026-06-21 14:30:03 UTC]

CHG-0074 — Returns: credit/refund a settled invoice + returns list screens

- **Settled-invoice returns (refund path).** A sales/purchase return can now be posted against an
  invoice that is **already paid** (in full or part). The return amount is split: it is applied to
  the invoice's still-**outstanding** AR/AP up to its balance, and any **excess is refunded** —
  Cr Cash-Bank (customer refund) for sales, Dr Cash-Bank (supplier refund) for purchases — via a
  chosen `refundCashAccountId`. If a refund is needed but no account is given, the post is rejected
  (`SALES.REFUND_ACCOUNT_REQUIRED` / `PURCHASING.REFUND_ACCOUNT_REQUIRED`, 422). Previously this
  threw because the AR/AP open item was settled. The journal stays balanced and the subledger only
  moves by the applied portion. (BR-SAL-8, BR-PUR-7.)
- **Contract:** `ISubledgerPosting.GetOutstandingAsync(openItemId)` exposes an open item's remaining
  balance to the Sales/Purchasing return handlers (implemented in the subledger service + adapter).
- **Returns list screens.** New `GET /api/v1/sales-returns` and `/purchase-returns` (all credit/
  debit notes, with customer/supplier name resolved) back two new searchable list views, reached via
  a **Returns** button on the Sales and Purchase order lists (routes `salesReturns` / `purchaseReturns`).
  EN/ID strings. The return dialogs gain an optional **Refund account** picker and now surface the
  server's message on failure.
- **Tests:** +4 (settled sales/purchase invoice refunds cash & skips AR/AP allocation; refund without
  an account is rejected). Existing return tests stub the new outstanding read. Full suite **291**
  green; frontend builds.
- **Verified (e2e):** full SO→deliver→invoice→pay→return and PO→receive→bill→pay→return chains —
  returning a fully-paid invoice without a refund account returns 422; with one it posts a cash
  refund; both list endpoints show the new notes with resolved party names.
- **Remaining (returns):** standalone return-detail page; partial-refund UX hint in the dialog.

---

## [2026-06-21 13:54:05 UTC]

CHG-0073 — Inventory: per-company negative-stock policy (BR-INV-3)

- The negative-stock policy is now a real **per-company setting** (`Inventory.AllowNegativeStock`,
  default **false** = disallow) instead of a hardcoded constant (ADR-0016). It is resolved once, at
  the single choke point `InventoryLedgerService.IssueAsync`, so it applies **uniformly** to sales
  deliveries, purchase returns, stock adjustments, transfers, and opname. The `allowNegative` flag
  was removed from the `IInventoryLedger` / `IInventoryPosting` contracts and all callers — modules
  no longer decide policy; inventory owns it.
- **Cross-module read:** `ICompanyDirectory` gains `GetBoolSettingAsync(companyId, key, fallback)`;
  `CompanySettingKeys.AllowNegativeStock` centralizes the key. The company read DTO now carries
  `allowNegativeStock` (list + detail), so the UI can reflect it.
- **Frontend:** Settings → Company gains an **Allow negative stock** toggle (admins only,
  `Admin.Companies`); saved via the existing `PUT /companies/{id}/settings`. EN/ID strings + hints.
- **Tests:** +1 (issue below zero succeeds when the company setting permits it); existing ledger/
  delivery/return/adjustment tests updated for the slimmer signature. Full suite **287** green;
  frontend builds.
- **Verified (e2e):** with the policy **off**, over-issuing stock is rejected
  (`INVENTORY.INSUFFICIENT_STOCK`, 409); after toggling the setting **on**, the same issue succeeds
  and drives on-hand negative; `GET /companies` reflects `allowNegativeStock`. (Dev stock restored.)
- **Remaining (Inventory slice 2):** back-dating recompute of cost buckets (BR-INV-5).

---

## [2026-06-21 11:05:22 UTC]

CHG-0072 — Expenses: record on account (Cr AP) + category edit/deactivate

- An expense voucher can now be recorded **on account** (unpaid) against a supplier instead of being
  paid immediately from cash/bank (ADR-0030). The atomic posting branches on the credit side:
  **paid** → Dr Expense (+ Dr VAT Input) / **Cr Cash-Bank**; **on account** → Dr Expense (+ Dr VAT
  Input) / **Cr Accounts Payable**, and it **opens an AP subledger item** for the supplier (due date
  tracked), reconciled to the AP control account like a purchase invoice. `POST /api/v1/expense-vouchers`
  now takes either `cashAccountId` (paid) or `supplierId` + `dueDate` (on account) — exactly one,
  enforced (`422` otherwise).
- **Expense categories** are now manageable: **edit** (name + posting-rule key) and
  **activate/deactivate** (ADR-0029; Code immutable). `PUT /api/v1/expense-categories/{id}` and
  `/{id}/active` (`Expenses.Manage`).
- **Frontend:** the New-expense modal gains a **Pay now / On account** toggle — on account swaps the
  cash-account field for a supplier + due-date picker; the list flags on-account vouchers with an
  **On account** badge. A new **Categories** manager (inline create / rename / activate-deactivate).
  EN/ID strings.
- **Tests:** +2 (on-account voucher credits AP & opens a payable with the supplier as subledger party;
  unknown supplier rejected). Full suite **286** green; frontend builds.
- **Verified (e2e):** an on-account voucher posted a balanced journal, set `apOpenItemId`, and left
  `cashAccountId` null; a paid voucher still credits cash; supplying both cash account and supplier is
  rejected (`422`); a category was renamed and deactivated.
- **DB:** migration `ExpenseOnAccount` (nullable `CashAccountId`; new `SupplierId`, `DueDate`,
  `ApOpenItemId`).

---

## [2026-06-21 10:44:18 UTC]

CHG-0071 — Chart-of-Accounts edit + activate/deactivate

- A GL account can now be **edited** (rename + toggle direct postability) and **activated/
  deactivated** (ADR-0029) — Code and Type stay immutable. `PUT /api/v1/accounts/{id}` and
  `/accounts/{id}/active` (`MasterData.Manage`). Guards: a **system** account (seeded, required by
  posting rules) can't be deactivated (`ACCOUNT_IS_SYSTEM`); an account with **posted GL activity**
  can't be deactivated (`ACCOUNT_IN_USE`).
- **Frontend:** a new **Accounts** tab in Accounting — a searchable, paginated Chart-of-Accounts list
  (code, name with Control/System chips, type, postable, status) with create, edit (name +
  allow-posting), and activate/deactivate (hidden for system accounts). EN/ID.
- **Tests:** +4 (rename + posting toggle; deactivate unused; system + in-use deactivation rejected).
  Full suite **284** green; frontend builds.
- **Verified (e2e):** created/renamed an account and toggled posting; deactivating a system account
  (1200/5000) returns `ACCOUNT_IS_SYSTEM`.
- **Remaining (CRUD completion):** distinct `*.Edit`/`*.Delete`/`*.Cancel` permissions (today CoA
  edit reuses `MasterData.Manage`).

---

## [2026-06-21 10:02:55 UTC]

CHG-0070 — Edit draft Sales / Purchase Orders before submit

- Completes the **BR-X-8** "draft may be edited or cancelled" half: a **still-draft** Sales or
  Purchase Order can now have its header **and lines** edited before submission.
  `PUT /api/v1/sales-orders/{id}` and `PUT /api/v1/purchase-orders/{id}` (`Sales.Create` /
  `Purchasing.Create`); a new `EditDraft` aggregate method replaces lines and recalculates totals.
  Editing an approved/decided order is rejected with `NOT_DRAFT` (corrections go via returns).
- **Frontend:** an **Edit** button on a draft order's detail opens the order form in edit mode
  (prefilled header + lines; "Save changes" → `PUT`, then back to the detail). Reuses the create
  view via an `?edit=<id>` query. EN/ID strings.
- **Tests:** +5 (domain edit replaces header/lines & recalculates, rejected once submitted; Sales &
  Purchasing update handlers edit a draft / reject a submitted order). Full suite **280** green;
  frontend builds.
- **Verified (e2e):** editing a draft replaced 1 line with 2 and recalculated the grand total
  (111,000 → 666,000); editing after submit returns `SALES.NOT_DRAFT`.

---

## [2026-06-21 09:18:40 UTC]

CHG-0069 — List search, full-width content, VAT tab gated

- **Search on every list.** The shared `DataTable` gained an optional `searchable` box that filters
  across the visible columns (case-insensitive) before pagination, with a "no matches" state and
  page reset on query change. Enabled on Sales orders, Purchase orders, Inventory, Products,
  Customers, Suppliers, Tax codes, Units, Categories, Warehouses, Expenses, and Approvals — so users
  can find a row instead of scrolling. (Pagination — 12/page — applies to the filtered results.)
- **Full-width content.** The main container no longer caps at 1600px left-aligned; it now fills the
  area (centered, max 1920px) and **reflows as the sidebar collapses/expands**, so the screen is used.
- **VAT report tab is hidden for non-PKP companies** (Accounting tabs now read the company VAT flag);
  the tab row also wraps on narrow screens.
- EN/ID (`common.noResults`). Frontend builds; backend unchanged.

---

## [2026-06-21 08:41:05 UTC]

CHG-0068 — Optional VAT: company VAT-registration (PKP) flag

- Tax is now **opt-in per company**, reflecting that most Indonesian SMEs are **not** registered for
  VAT (only a **PKP — Pengusaha Kena Pajak** charges/reclaims PPN). New `Company.IsVatRegistered`
  (default **false**; EF migration backfills existing rows to false; the seeded demo company is
  **PKP=true** so the VAT flows/reports stay rich). Exposed on `CompanyInfo`, the company DTO, and the
  update command/endpoint.
- **Frontend:** a **"Registered for VAT (PKP)"** toggle in Settings → Company. A new app-wide
  `company` store surfaces the flag; the Sales/Purchase order create forms and the Expense voucher
  form now **default tax to 0% and hide the tax column/PPN checkbox** when the company is not
  registered (when registered, today's 11% behaviour is unchanged).
- **PDFs** omit the **VAT (PPN)** total line when a document has no tax, so non-PKP invoices/POs/bills
  read cleanly.
- No posting-logic change — a 0-rate line already produces no VAT journal, so accounting integrity is
  untouched; switching PKP status only affects new documents (BR-X / ADR-0012).
- **Tests:** company domain test asserts the new flag + non-PKP default. Full suite **275** green;
  frontend builds. e2e: toggling the flag flips form behaviour; a 0-tax order posts subtotal=grand,
  tax 0.

---

## [2026-06-21 07:52:18 UTC]

CHG-0067 — Collapsible + responsive sidebar (desktop rail + mobile drawer)

- The sidebar can now be **collapsed to an icon-only rail** on desktop via a toggle (state persisted
  in localStorage); labels, group headers, brand text and the ⌘K search text hide, leaving centered
  icons with hover tooltips, and an expand button at the foot of the rail.
- **Responsive for phones/tablets:** below `lg` the sidebar becomes an **off-canvas drawer** opened
  by a new hamburger button in the top bar, with a tap-to-dismiss backdrop; it auto-closes on
  navigation. The top bar title truncates and the user name/email hide on the smallest screens so the
  header never overflows.
- New `layout` Pinia store (`collapsed` + `mobileOpen`); `nav.collapse/expand/menu` strings (EN/ID).
  Frontend builds.

---

## [2026-06-21 07:14:33 UTC]

CHG-0065 — Compact money on dashboard/insight cards (overflow fix)

- Added `formatMoneyShort` (K / M / B / T suffixes, id-ID decimals, negatives in parentheses) and
  applied it to the dashboard KPI tiles, the top-debtor/creditor amounts, and the per-menu insight
  "total value" cards, so large IDR figures no longer overflow the cards — e.g. `IDR 5,9B` instead
  of `IDR 5.895.334.250`, `(IDR 34,7M)` for a loss. Full-precision values remain in tables, tooltips,
  and reports.

---

## [2026-06-21 06:58:10 UTC]

CHG-0064 — Per-menu insight cards above lists

- A reusable compact **`InsightCards`** strip now sits above the main list screens, summarising the
  list at a glance (computed client-side from the loaded rows, no extra calls):
  - **Sales orders / Purchase orders:** count, total value, drafts, delivered/received.
  - **Inventory on-hand:** stocked items, total value.
  - **Products / Customers / Suppliers:** total, active, inactive.
  - **Expenses:** vouchers, total value.
- Color-toned values (accent for money, positive/negative for active/inactive) for quick scanning.
  EN/ID. Frontend builds.

---

## [2026-06-21 06:30:44 UTC]

CHG-0063 — Insight-rich dashboard + table pagination

- **Dashboard rebuilt for decision-making.** The summary endpoint now returns, alongside cash / AR /
  AP / month P&L: **inventory value**, **prev-month** revenue & expense (for MoM deltas), **overdue
  counts**, a **6-month revenue / expense / profit trend**, **AR & AP aging buckets**
  (current / 1–30 / 31–60 / 61–90 / 90+), **expense composition by account** for the month, and the
  **top 5 debtors / creditors by name** (party names resolved via `IMasterDataLookup`).
- **Frontend dashboard** (ECharts, already bundled): six insight tiles (with a revenue MoM delta),
  a **combo bar+line** revenue/expense/profit trend, an **expense-breakdown donut** (color-coded
  categorical palette), **AR & AP aging** bar charts (green→red severity colors), and **top
  receivables/payables** mini-bar lists. Theme- and dark-mode-aware; money tooltips, compact axes.
- **Shared `DataTable` now paginates** (client-side, 12/page by default, prev/next + "showing X–Y of
  N"), so every list screen (master data, sales, purchasing, inventory, expenses, approvals) paginates
  automatically. Snaps to page 1 on filter/reload.
- **Tests:** dashboard handler test extended to assert the new insights (trend, aging, inventory
  value, top parties, expense composition). Full suite **275** green; frontend builds.
- **Verified (e2e):** the seeded company returns a populated trend, aging, named expense categories,
  and named top debtors/creditors.

---

## [2026-06-21 05:42:09 UTC]

CHG-0062 — Inventory valuation report (reconciles to the GL)

- New **Inventory valuation** report: on-hand value by product at moving-average cost (aggregated
  across warehouses), with the **GL Inventory control-account balance it reconciles to** (BR-INV-7) —
  the ledger (stock buckets) is the source of truth; the GL figure is read via a new cross-module
  contract `IGeneralLedgerBalances` (Accounting), surfaced with a difference + Reconciled flag (a
  sub-unit moving-average rounding residue is tolerated).
- `GET /api/v1/stock/valuation` and `/stock/valuation/pdf` (`Inventory.View`). The PDF reuses the
  report renderer (brand logo, grand-total + reconciliation rows); product names via `IMasterDataLookup`.
- **Frontend:** an **Inventory valuation** screen (value-by-product table, total → GL balance →
  difference with a Reconciled badge, PDF download), reached from a **Valuation report** link on the
  Stock-on-hand screen and the command palette. EN/ID.
- **Tests:** +2 (aggregates value by product & reconciles; flags a difference when the GL disagrees).
  Full suite **275** green; frontend builds; arch-fitness intact (new contract lives in Modules.Contracts).
- **Verified (e2e):** against the seeded company the report values 12 products, total
  **Rp 1,984,611,701.84** ties to the GL Inventory account (TB 1200) within a 0.01 rounding residue →
  Reconciled; PDF downloads as valid `application/pdf`.
- This completes the **financial-report suite** (TB / P&L / Balance Sheet / Cash Flow / VAT / GL /
  AR-AP aging / **inventory valuation**).

---

## [2026-06-21 05:01:30 UTC]

CHG-0061 — Cancel draft Sales / Purchase Orders (ADR-0029)

- **Cancel** a draft (or pending-approval) Sales Order or Purchase Order:
  `POST /api/v1/sales-orders/{id}/cancel` (`Sales.Create`) and
  `POST /api/v1/purchase-orders/{id}/cancel` (`Purchasing.Create`). New `Cancel*OrderCommand` +
  handlers return a clean `NOT_CANCELLABLE` conflict once the order is approved/decided — those are
  immutable and must be reversed via returns, never cancelled.
- **Domain:** added `CanCancel` (Draft or PendingApproval) and tightened `Cancel()` on both order
  aggregates to honor it — fixing a latent gap where a delivered/received order could be cancelled.
- **Frontend:** a **Cancel order** (danger) button on the Sales-Order and Purchase-Order detail
  headers, shown only while the order is cancellable, with a confirm prompt. EN/ID strings.
- **Tests:** +6 (domain cancel-from-draft / cancel-from-pending / decided-throws; handler cancel +
  not-cancellable, for both modules). Full suite **273** green; frontend builds.
- **Verified (e2e):** cancelled a draft PO and a draft SO (→ Cancelled); cancelling an approved PO is
  rejected with `PURCHASING.NOT_CANCELLABLE`.
- **Remaining (CRUD completion):** draft document line-edit before submit; Chart-of-Accounts edit;
  distinct `*.Edit`/`*.Delete`/`*.Cancel` permissions.

---

## [2026-06-21 04:06:52 UTC]

CHG-0060 — Master-data CRUD completion — Units, Categories, Tax codes

- **Edit + activate/deactivate** for the remaining master data, bringing it to parity with
  customers/suppliers/warehouses/products (ADR-0029, soft-delete only — rows are never physically
  removed; Code stays the immutable natural key):
  - **Units of measure** and **Product categories** gained an `IsActive` flag (EF migration backfills
    existing rows to active) plus `Update`/`Activate`/`Deactivate`; **Tax codes** gained `Update`
    (name + rate, fraction-validated) and `Activate`.
  - New `Update*`/`Set*Active` commands + handlers; `PUT /{id}` and `PUT /{id}/active` endpoints for
    `units-of-measure`, `product-categories`, `tax-codes` (`MasterData.Manage`). List DTOs now expose
    `IsActive`.
- **Frontend:** three new Master-data tabs — **Units**, **Categories**, **Tax codes** — each a
  list + create/edit modal + activate/deactivate (Tax codes edits the rate as a percentage). EN/ID.
- **Tests:** +5 (domain edit/activate, tax-rate validation, Update/SetActive handlers, not-found).
  Full suite **267** green; frontend builds.
- **Verified (e2e):** created/renamed/deactivated a unit, edited a tax code's name + rate, and
  created/renamed a category; seeded rows backfilled to active.
- **Remaining (CRUD completion):** Chart-of-Accounts edit; distinct `*.Edit`/`*.Delete` permissions;
  status-gated edit/cancel for draft documents.

---

## [2026-06-21 02:34:18 UTC]

CHG-0059 — Year-end close to Retained Earnings + Fiscal-periods screen

- **Year-end close** (`POST /api/v1/fiscal-years/{id}/close`, `Accounting.PeriodClose`): posts a
  closing journal (`JournalSource.PeriodClose`) that zeros every Revenue and Expense account for the
  year and carries the net result to **Retained Earnings** (account resolved from the posting-rule
  engine, never hardcoded) — a profit credits RE, a loss debits it. It then marks the fiscal year
  closed and **locks every period**. The closing entry is dated at year-end, so the final period must
  still be open; an already-closed year is rejected. Nothing-to-close years finalize without a journal.
- New repo `GetFiscalYearByIdAsync`; new errors `FISCAL_YEAR_NOT_FOUND` / `FISCAL_YEAR_ALREADY_CLOSED`
  / `FINAL_PERIOD_NOT_OPEN` (BR-ACC-8). `FiscalYear.Close()` finalizes + locks periods.
- **Frontend:** a new **Periods** tab in Accounting (+ route + command-palette entry) — create a
  fiscal year, close/reopen individual periods, and run the year-end close with a confirmation and a
  net-income-carried result message. Period status badges (Open/Closed/Locked). EN/ID strings.
- **Tests:** +3 (closing entry zeros P&L and carries net income to RE & locks; already-closed
  rejected; final-period-must-be-open). Full suite **262** green; frontend builds.
- **Verified (e2e):** closing 2026 zeroed P&L (rev/exp → 0), moved Retained Earnings by exactly the
  net loss (−8,000,000,000 → −7,904,171,701.83), kept the trial balance **balanced**, locked all
  periods; re-close → `FISCAL_YEAR_ALREADY_CLOSED` and a new posting into the locked year →
  `PERIOD_CLOSED`.
- **Remaining (Accounting slice 2):** period-close balance snapshots (rebuildable, ADR-0022).

---

## [2026-06-20 13:58:21 UTC]

CHG-0058 — General Ledger / Account-detail report — report + PDF + screen

- New **General Ledger detail** report: every posted journal line over a period — optionally for
  a single account — grouped by account, with the per-account **opening balance carried forward**
  into a running balance, plus period debit/credit totals and a closing balance. Balances are
  signed debit − credit, consistent with the Trial Balance, so the GL drills down to and reconciles
  with it. Each line carries its entry no., source, and source-document id for drill-down.
- New read-store query `GetGeneralLedgerAsync` (posted lines, optional account filter, ordered by
  account then date/entry). `GET /api/v1/reports/general-ledger` and `/general-ledger/pdf`
  (`Accounting.View`); opening balances reuse the trial-balance-to-date.
- **Frontend:** a **General ledger** tab in Accounting (+ route + command-palette entry) with an
  account filter (all/specific), period filters, per-account tables (opening → entries with running
  balance → closing), and PDF download. EN/ID strings.
- **Tests:** +2 (opening balance carried into a running balance; from-less grouping/ordering with
  zero opening). Full suite **259** green; frontend builds.
- **Verified (e2e):** the GL ties exactly to the Trial Balance (Inventory Variance 5100 closing =
  TB balance); period-scoped opening + net == closing for Bank; single- and all-account PDFs render
  as valid `application/pdf`.

---

## [2026-06-20 13:12:40 UTC]

CHG-0057 — Inventory slice 2 — GL posting on stock moves + stock opname

- **Stock adjustments now post to the GL atomically.** An increase posts Dr Inventory /
  Cr Inventory Variance, a decrease the reverse — accounts resolved from the posting-rule
  engine (never hardcoded), valued at the moving-average cost applied by the ledger. The
  inventory ledger entry and its journal commit together via `ICrossModuleUnitOfWork`
  (`LedgerSource.StockAdjustment`), or roll back together. A zero-value change posts nothing.
- **Stock opname (physical count)** — new `POST /api/v1/stock/opname`: records a counted
  quantity, computes the variance against system on-hand, and posts a reconciling adjustment
  (in/out) with its GL journal. An exact match records nothing; an increase is valued at the
  supplied unit cost or, by default, the current moving-average cost. (`Inventory.Adjust`.)
- **Warehouse transfers** remain GL-neutral under a single Inventory control account (cost
  travels with the goods) — documented, not changed.
- **Frontend:** per-row **Adjust** and **Count** actions on the Stock-on-hand screen open a
  modal (direction/qty/cost/reason for adjust; counted-qty + variance feedback for opname),
  refreshing on-hand after posting. EN/ID strings.
- **Tests:** +4 (adjustment debit/credit direction & accounts; opname shortfall posts a
  decrease; exact-match posts nothing). Full suite **257** green; frontend builds.
- **Verified (e2e):** an adjustment-out and an opname shortfall both moved Inventory (1200)
  and Inventory Variance (5100) by the moving-average cost, the trial balance stayed
  **balanced**, and the stock card shows both movements. Variance account = exact sum.
- **Remaining (Inventory slice 2):** per-company negative-stock setting, back-dating recompute.

---

## [2026-06-20 12:34:05 UTC]

CHG-0056 — Cash Flow Statement (indirect method) — report + PDF + screen

- New **Cash Flow Statement** completing the core financial-report set (TB / P&L /
  Balance Sheet / VAT / **Cash Flow**). Derived from the GL by the **indirect
  method**: starts from net income and adjusts for the period movement of every
  non-cash balance-sheet account — non-cash assets and operating liabilities as
  Operating working capital, equity movements (e.g. owner capital) as Financing.
  Cash & bank (the 10xx code band) is the reconciling target. By the double-entry
  identity the three sections always sum to the actual change in cash, so the
  statement reconciles opening + net change == closing.
- `GET /api/v1/reports/cash-flow` and `/reports/cash-flow/pdf` (`Accounting.View`);
  the PDF reuses the report renderer (brand logo, section/subtotal/grand-total rows).
- **Frontend:** a **Cash flow** tab in Accounting (+ command-palette entry + route)
  with Operating / Investing / Financing sections, a net-change → opening → closing
  reconciliation block with a Reconciled badge, period filters, and PDF download.
  EN/ID strings.
- **Tests:** +2 (indirect-method reconciliation; equity-as-financing classification).
  Full suite **253** green; frontend builds.
- **Verified (e2e):** against the seeded company the statement reconciles exactly —
  net change in cash == closing cash == dashboard cash & bank (Rp 5,895,334,250),
  `isReconciled: true`; PDF downloads as valid `application/pdf`.
- **Remaining (Accounting slice 2):** period-close balance snapshots, year-end close
  to retained earnings. Investing (non-current assets) and financing-debt detail
  refine when those accounts exist.

---

## [2026-06-20 11:52:10 UTC]

CHG-0055 — Dev seed script — realistic demo data (`scripts/seed_dummy_data.py`)

- New developer utility that populates the dev company with a coherent dummy
  dataset by driving the real HTTP API, so the result stays internally consistent
  (GL balances, AR/AP subledgers reconcile to control accounts, inventory ledger
  matches movements, dashboard reflects real figures).
- Creates 12 products / 8 customers / 6 suppliers; an owner's-capital opening
  journal + one fully-paid opening-stock PO per product; purchase orders and sales
  orders in mixed lifecycle states with ~⅓ of bills/invoices left open (so AP and
  AR are populated); and a dozen expense vouchers. Recent sales are dated in the
  current month for live dashboard revenue.
- Stdlib-only (no pip); master data keyed by stable codes and re-looked-up on
  conflict, so re-runs are safe. Documented in [scripts/README.md](scripts/README.md).
- Verified after a clean DB reset + base seed: trial balance **balanced**; dashboard
  shows positive cash, open AR/AP, and this-month revenue; all 12 products carry
  on-hand stock. Tooling only — no application code or test changes.

---

## [2026-06-20 11:18:33 UTC]

CHG-0054 — Purchase-document PDFs + brand-logo polish (ADR-0031)

- **Purchase Order** PDF (`GET /api/v1/purchase-orders/{id}/pdf`) and **Purchase Invoice (bill)** PDF
  (`GET /api/v1/purchase-invoices/{id}/pdf`), both `Purchasing.View`, reusing the shared `PdfDocument`
  model + `PdfRenderer`. New `PurchasingPdf` handlers resolve supplier/product names via
  `IMasterDataLookup` and the company block via `ICompanyDirectory`. The PO renders our company as the
  seller / the supplier as recipient; the bill renders the supplier as seller / our company as Bill-To,
  with invoice/due dates and the optional supplier reference number in the meta block.
- **Brand logo** embedded in every PDF: the teal Accountrack mark (SVG, kept in sync with
  `docs/frontend/brand/logo-mark.svg`) now sits above the seller name on documents and beside the
  company name on financial reports, via QuestPDF's vector `.Svg()` (crisp at any zoom, no raster
  asset). PDFs continue to use QuestPDF's bundled Lato face (clean, professional; embedding the brand
  UI font Plus Jakarta Sans remains an optional later refinement).
- **Frontend:** a **PDF** button on the Purchase Order detail header and a **PDF** action per bill in
  the invoices list (`downloadFile`). EN/ID strings.
- Full suite **251** green; e2e: PO PDF (~75 KB), bill PDF (~80 KB), and a re-checked Trial Balance
  report PDF (~61 KB) all download as valid `application/pdf` with the brand logo.

---

## [2026-06-20 10:29:52 UTC]

CHG-0053 — Financial-report PDFs — Trial Balance / P&L / Balance Sheet / VAT (ADR-0031)

- New **report PDF** layer reusing QuestPDF: a format-neutral `PdfReport` model (SharedKernel) with
  section-header / subtotal / grand-total row styles, rendered by `PdfRenderer.RenderReport`
  (company header, period subtitle, accent table header, emphasised totals, page-numbered footer).
- PDFs for **Trial Balance**, **Profit & Loss**, **Balance Sheet**, and **VAT (PPN)** via
  `GET /api/v1/reports/{trial-balance|profit-loss|balance-sheet|vat}/pdf` (`Accounting.View`). The PDF
  handlers reuse the existing report queries through `ISender`, so the figures match the on-screen
  reports exactly; the company header (name + NPWP + functional currency) comes from `ICompanyDirectory`.
- **Frontend:** a **PDF** button next to **Apply** on each of the four report screens, passing the
  current period/as-of filters. EN/ID strings.
- Full suite **251** green; e2e: all four reports download as valid `application/pdf`.

---

## [2026-06-20 10:10:47 UTC]

CHG-0052 — PDF documents — Invoice + Quotation (ADR-0031)

- Added **PDF generation** via **QuestPDF** (Community License, set at startup). A format-neutral
  `PdfDocument` model (SharedKernel) is assembled by handlers and rendered by a modern, reusable
  `PdfRenderer` (Web.Common): brand-teal title + table header, generous whitespace, From / Bill-To
  blocks, right-aligned tabular money, zebra line rows, an emphasised grand total, notes, and a
  page-numbered footer.
- **Documents:** **Sales Invoice** PDF (`GET /api/v1/sales-invoices/{id}/pdf`) and **Quotation** PDF
  rendered from a sales order (`GET /api/v1/sales-orders/{id}/quotation-pdf`), both `Sales.View`.
  Product/customer names resolved via `IMasterDataLookup`; seller block (name + NPWP) comes from the
  company — `ICompanyDirectory.CompanyInfo` extended with `Name`/`LegalName`/`TaxId`.
- **Frontend:** a **Quotation PDF** button on the Sales Order detail header and a **PDF** action per
  invoice in its invoices list; a shared `downloadFile` helper. EN/ID strings.
- **Tests:** +1 (`PdfRenderer` produces a valid `%PDF`). Full suite **251** green.
- **Verified (e2e):** invoice + quotation download as `application/pdf` (`%PDF-1.7`, ~72 KB) with the
  company header (name + tax id), line items, and totals.
- **Next:** financial-report PDFs (TB/P&L/BS/VAT) and purchase-document PDFs reuse the same renderer.

---

## [2026-06-20 09:51:08 UTC]

CHG-0051 — Excel (.xlsx) export + export across every list menu (ADR-0031)

- Added **Excel (.xlsx)** as an export format via **ClosedXML** (MIT) and rolled **CSV/Excel export**
  out to **all list menus**, not just master data.
- **Building block:** a format-neutral `TabularData` payload (SharedKernel) + a `TableExport`
  renderer (Web.Common) that streams CSV or XLSX based on `?format=csv|xlsx`. Master-data exports
  were refactored onto it (so they gain Excel for free); the import paths are unchanged.
- **New list exports** (`GET …/export?format=`): sales orders, purchase orders, inventory on-hand,
  and expense vouchers. Sales/purchasing/inventory resolve party/product/warehouse **names** via a
  new `IMasterDataLookup.ResolveNamesAsync` cross-module helper. Gated by each module's `View`
  permission; document **import** stays master-data-only (posted docs are immutable, ADR-0029).
- **Frontend:** a shared **ExportMenu** (Export ▾ → Excel / CSV) replaces the single export button on
  the four master-data screens and is added to sales/purchasing/inventory/expenses lists; a shared
  `downloadExport(path, name, format)` helper. EN/ID strings.
- **Tests:** +2 (`TableExport` — CSV matches the shared writer; XLSX is a valid workbook with header +
  rows, reopened with ClosedXML). Full suite **250** green.
- **Verified (e2e):** customers/sales-orders XLSX download with the correct content-type + PK/zip
  signature; CSV default works; inventory/purchasing/expenses export headers correct with names
  resolved.
- **Next:** PDF for financial reports + documents (planned via QuestPDF — Community License free
  under USD 1M revenue).

---

## [2026-06-20 09:23:24 UTC]

CHG-0050 — CSV import/export for suppliers, products, warehouses (ADR-0031)

- Extended the CSV import/export pattern (CHG-0049) to the remaining master data: **suppliers**,
  **warehouses**, and **products** — each with template / dry-run preview / all-or-nothing commit
  (match on Code) / export, gated by `MasterData.Import` / `MasterData.Export`.
- **Products** resolve **UoM and category by code** (not id): base UoM is required to create and
  **immutable on update**; an unknown UoM/category is an error row. Booleans accept
  true/false/yes/no/1/0. Export emits the UoM/category **codes** so it round-trips back through import.
- **Frontend:** factored the import flow into a shared `useCsvImport` composable + `CsvImportModal`
  component; all four master-data screens (customers/suppliers/warehouses/products) now share the
  Template / Export / Import toolbar + preview modal. Customers refactored onto the shared pieces.
- **Tests:** +4 (supplier create/update by code; warehouse missing-name; product UoM/category
  resolution + create; unknown-UoM error). Full suite **248** green.
- **Verified (e2e):** product preview flagged an unknown UoM (1 error) and blocked the commit; a
  valid file created 2; export round-trips UoM/category codes + flags; supplier/warehouse exports OK.
- Master-data CSV import/export is now complete for all four entities. Excel (.xlsx) + PDF and async
  large files remain the next layers.

---

## [2026-06-20 05:30:22 UTC]

CHG-0049 — Data import/export foundation — CSV, Customers (ADR-0031)

- New cross-cutting **import/export** capability, CSV first. A dependency-free RFC-4180 reader/writer
  in SharedKernel (`Csv`); shared per-row result contracts in `Application.Abstractions.Import`.
- **Customers** (the reference entity, BR-IMP-*):
  - **Template** — `GET /api/v1/customers/import/template` (downloadable CSV with headers + sample).
  - **Dry-run preview** — `POST .../import/preview` (multipart) returns per-row Create/Update/Error
    + counts; nothing is written.
  - **Commit** — `POST .../import/commit` is **all-or-nothing** (any invalid row blocks the whole
    import); matches existing rows by **Code** (update) else creates. The same parse+validate pass
    backs preview and commit, so the preview is exact.
  - **Export** — `GET /api/v1/customers/export` streams the list as CSV.
- Permission-gated by new **MasterData.Import / MasterData.Export** (seeded + granted to admin),
  tenant-scoped.
- **Frontend:** Customers screen toolbar — Template / Export / Import; Import opens a preview modal
  (per-row table + counts) and commits only when there are no errors. EN/ID strings.
- **Tests:** +6 (CSV parse/write incl. quoted fields; import preview classification; commit
  all-or-nothing + create/update by code). Full suite **244** green.
- **Verified (e2e):** template downloads; preview classified 2 create + 1 error; an invalid file was
  blocked; a valid file created 2 then re-committed as 2 updates; export round-trips.
- **Scope:** CSV + Customers first. Excel (.xlsx) + PDF, the other entities, and async large-file
  imports are the next layers.

---

## [2026-06-20 05:15:52 UTC]

CHG-0048 — Expenses module — operating-expense vouchers (ADR-0030)

- New **Expenses** module (Domain/Application/Infrastructure/Api, schema `expenses`) for recording
  day-to-day operating costs without manual journals (BR-EXP-*).
- **Expense categories** (seeded: Electricity, Transport, Rent, Supplies, Salaries, Other) each carry
  a **posting-rule key**; the expense GL account is resolved through the posting-rule engine
  (ADR-0024), never hardcoded. Accounting seed adds expense accounts 6000–6900 + default rules.
- **Expense voucher** (paid from a cash/bank account) posts atomically (one cross-module transaction):
  **Dr Expense per category** (collapsed per account) **+ Dr VAT Input** (where creditable) **/ Cr
  Cash-Bank** — and is idempotency-keyed.
- **API:** `GET/POST /api/v1/expense-categories`, `GET /api/v1/expense-vouchers`,
  `GET /api/v1/expense-vouchers/{id}`, `POST /api/v1/expense-vouchers`
  (`Expenses.View/Manage/Post`). New RBAC permissions seeded + granted to the admin. EF migration
  `InitialExpenses`. Module registered in the bootstrapper; idempotency-table creation moved after
  module migrations (so a first-run/empty database is created before it is used).
- **Frontend:** new **Expenses** nav item + screen (voucher list + create modal with dynamic lines,
  category/cash-account pickers, per-line PPN toggle, live total) + ⌘K entry; EN/ID strings.
- **Tests:** new `Accountrack.Expenses.UnitTests` (4 — voucher totals incl. VAT; handler debits each
  expense account + VAT and credits cash, balanced; same-category lines collapse; missing/inactive
  category rejected). Full suite **238** green; arch-fitness clean for the new module.
- **Verified (e2e):** posting a voucher (Electricity 500k @0% + Transport 100k @11%) created journal
  EXP/202606/00001 (grand 611,000); P&L expenses 0 → 600,000 split per account, VAT to VAT Input.
- **Scope:** expense recording, paid via cash/bank. On-account (Cr AP) and full Payroll remain a
  later phase.

---

## [2026-06-20 04:38:20 UTC]

CHG-0047 — Purchase returns (debit notes) — procure-to-pay (BR-PUR-7)

- New **Purchase Return / debit note** against a **posted purchase invoice**, mirroring sales returns.
  Atomically (one cross-module transaction): issues each line out of stock at moving-average cost
  (Cr Inventory), reverses billing (Dr AP control / Cr VAT Input), books any **cost-vs-price
  variance** to the inventory-variance account, reduces the invoice's AP open item, and records the
  debit note. Bounded per line by invoiced-not-yet-returned qty.
- **Domain:** `PurchaseReturn` aggregate + `PurchaseReturnNumberSequence` (DRN/ prefix);
  `ReturnedQuantity` / `ReturnableQuantity` + `Return()` on `PurchaseInvoiceLine`.
- **API:** `POST /api/v1/purchase-invoices/{id}/returns`, `GET /api/v1/purchase-orders/{id}/returns`,
  `GET /api/v1/purchase-returns/{id}` (Purchasing.Post / Purchasing.View). EF migration
  `AddPurchaseReturns` (3 tables + `ReturnedQuantity` column).
- **Frontend:** the Purchase Order detail page gets a **Return** action per posted bill (modal with
  per-line returnable quantities) and a **Returns** card; EN/ID strings.
- **Tests:** +5 Purchasing (domain return bounds; handler reverses billing + de-stocks, balanced
  journal incl. price-variance case, AP allocation; rejects unposted invoice / over-return). Full
  suite **234** green.
- **Verified (e2e):** PO → receive → bill (22,200) → return 4 — stock 10→6, VAT Input reversed by
  exactly the credited tax (−880), debit note DRN totals (net 8,000 / tax 880 / gross 8,880 /
  cost 8,000), bill returnable 10→6, over-return rejected, return listed on the order.
- **Known limitation** (same as sales returns): the debit reduces the invoice's outstanding payable,
  so a return cannot exceed what is still owed on that bill; debiting a fully-paid bill (supplier
  refund / credit) is a later enhancement. **Returns are now complete on both sides** (sales + purchasing).

---

## [2026-06-20 04:13:17 UTC]

CHG-0046 — Sales returns (credit notes) — order-to-cash (BR-SAL-8)

- New **Sales Return / credit note** against a **posted sales invoice**. Atomically (one cross-module
  transaction): restocks each line at its **original delivered cost** (Dr Inventory / Cr COGS),
  reverses billing (Dr Revenue + Dr VAT Output / Cr AR control), reduces the invoice's AR open item,
  and records the credit note. Returns are bounded per invoice line by invoiced-not-yet-returned qty.
- **Domain:** `SalesReturn` aggregate + `SalesReturnNumberSequence` (CRN/ prefix); `ReturnedQuantity`
  / `ReturnableQuantity` + `Return()` on `SalesInvoiceLine`. New `LedgerSource`/`JournalSource`
  values `SalesReturn`/`PurchaseReturn` for drill-down.
- **API:** `POST /api/v1/sales-invoices/{id}/returns`, `GET /api/v1/sales-orders/{id}/returns`,
  `GET /api/v1/sales-returns/{id}` (Sales.Post / Sales.View). EF migration `AddSalesReturns`
  (3 tables + the `ReturnedQuantity` column). Fixed a latent bug: `ListBySalesOrderAsync` now
  `Include`s delivery lines (delivery `TotalCost` summaries were 0).
- **Frontend:** the Sales Order detail page gets a **Return** action per posted invoice (modal with
  per-line returnable quantities) and a **Returns** card listing the SO's credit notes; EN/ID strings.
- **Tests:** +4 Sales (domain return bounds; handler reverses billing + restocks at cost, balanced
  journal, AR allocation; rejects unposted invoice / over-return). Full suite **229** green.
- **Verified (e2e):** SO → deliver → invoice (55,500) → return 4 then 6 — stock restored 90→94→100,
  COGS reversal cost 6,000 on the second return, VAT Output reduced by exactly the credited tax,
  invoice returnable 10→6→0, over-return rejected, both credit notes listed on the order.
- **Known limitation:** the credit reduces the invoice's outstanding receivable (allocation), so a
  return cannot exceed what is still owed on that invoice; crediting a fully-paid invoice (cash
  refund / customer credit) is a later enhancement. Purchase returns are still pending.

---

## [2026-06-20 03:12:53 UTC]

CHG-0045 — Master-data CRUD: Edit + activate/deactivate (ADR-0029)

- Master data now supports **Edit** and **deactivate/reactivate** (soft, reversible) for customers,
  suppliers, warehouses, and products — completing list+create into full CRUD. Code (the natural key)
  and a product's base UoM are immutable after creation; "delete" is deactivation, never a physical
  row removal (BR-X-7).
- **Domain:** `Update(...)` + `Activate()`/`Deactivate()` on Customer, Supplier, Warehouse, Product.
- **Application:** `Update*Command` (+ validators) and `Set*ActiveCommand` handlers for the four
  entities; not-found returns the entity's `*_NOT_FOUND` error.
- **API:** `PUT /api/v1/{entity}/{id}` (edit) and `PUT /api/v1/{entity}/{id}/active` (activate/
  deactivate), permission-gated by `MasterData.Manage`. (Distinct `*.Edit`/`*.Delete` permissions
  remain a later refinement per ADR-0029.)
- **Frontend:** edit reuses each create modal (code/base-UoM disabled in edit); a new shared
  `RowActions` cell (Edit · Activate/Deactivate) and a Status badge column on all four master-data
  tables; bilingual labels (EN/ID). New `lib/masterData` update/setActive methods.
- **Tests:** +6 (domain update/activate, immutable code/UoM, update-handler not-found + save,
  set-active). Full suite **225** green.
- **Verified (e2e):** create → `PUT` edit (name/taxId/terms/credit changed) → `PUT .../active`
  deactivate, confirmed via list (`isActive: false`).

---

## [2026-06-19 12:42:31 UTC]

CHG-0044 — Docs: scope expansion — CRUD policy, Expenses module, Import/Export

- Recorded three product/architecture decisions (docs only; no code):
  - **ADR-0029 — Edit/Delete policy:** master data gets Edit + deactivate (soft-delete, never
    physical; can't deactivate while referenced); transactional docs are status-gated (drafts
    editable, posted immutable → reversal/return only). New BR-X-7/BR-X-8.
  - **ADR-0030 — Expenses module:** operating-expense vouchers (electricity, transport, rent,
    salaries-as-cash…), categories → expense GL via posting rules, automatic atomic posting
    (Dr Expense [+ VAT Input] / Cr Cash-Bank or AP), approvals; payroll stays Phase 3. New BR-EXP-*.
  - **ADR-0031 — Data Import/Export:** CSV/Excel import with per-entity templates + validated dry-run
    + commit; CSV/Excel list export; PDF for documents/reports; permissioned, tenant-scoped, audited;
    master data first. New BR-IMP-*.
- Propagated to CLAUDE.md (new Expenses module, Data Import & Export, Record Management policy
  sections), MODULES.md (status rows + module/capability entries + CRUD-status note), ROADMAP.md
  (Phase 2 items 14–16), PRD.md (functional scope), BUSINESS_RULES.md, and STATUS.md backlog.

---

## [2026-06-19 12:23:48 UTC]

CHG-0043 — VAT (PPN) report — Output − Input

- New report `GET /api/v1/reports/vat?fromDate&toDate` (ADR-0012): VAT Output (PPN Keluaran, collected
  on sales) minus VAT Input (PPN Masukan, paid on purchases) for a period; net > 0 is payable to the
  tax office, net < 0 is an overpayment carried forward.
- Derived from the GL (posted journal lines only), with the VAT accounts resolved from the
  posting-rule engine (keys `VATOutput`/`VATInput`) so it follows configuration, not hardcoded codes.
  If a company hasn't configured those rules the report returns the engine's *unresolved* error.
- Backend: `GetVatReportQuery` + handler; new `IAccountingReadStore.GetAccountMovementsAsync` (per-
  account debit/credit sums over a period for given accounts).
- Frontend: new **VAT (PPN)** tab under Accounting (period filter + Output/Input/Net card), a
  command-palette entry, and EN/ID strings.
- **Verified (e2e):** report math matches the GL — posting a balanced journal of +110 Output / +55
  Input shifted the report by exactly that (net −48,259.20 → −48,204.20). Full suite 219 tests green;
  frontend build green.

---

## [2026-06-19 12:13:05 UTC]

CHG-0042 — Cross-tenant data-isolation integration suite (MULTI_TENANCY.md §9)

- New `Accountrack.IntegrationTests` project covering the non-negotiable that a tenant can never see
  another tenant's data (#33). 28 tests, all green against a real SQL Server.
- **Behavioral isolation** (real SQL Server provider, exercises the global query filters + the
  tenancy-stamping interceptor exactly as in production), probed via Master Data's `Customer`:
  - cross-tenant query returns zero foreign rows;
  - the active-company filter isolates companies within a single tenant;
  - insert stamps TenantId/CompanyId from the ambient context (app code never sets them);
  - modifying another tenant's row (reached via `IgnoreQueryFilters`) throws *Tenant mismatch*;
  - inserting with no established tenant context is rejected.
- **Model conventions** (offline, no DB): reflects over **all 11 module DbContexts** and asserts every
  tenant-scoped entity has a global query filter and every soft-deletable entity filters `IsDeleted`
  — catching a new entity that forgets the tenant base class or a context that skips
  `ApplyAccountrackConventions`. A guard test asserts ≥11 contexts are discovered so the data-driven
  tests can't silently pass on an empty set.
- **Infra note:** TESTING.md prescribes Testcontainers; Docker is unavailable in this environment, so
  the fixture targets a local/CI SQL Server (env `ACCOUNTRACK_TEST_SQL`, default localhost) and
  **skips** the behavioral tests when none is reachable (the offline model-convention tests always
  run). The fixture creates/drops a throwaway `Accountrack_IsolationTests` database.
- Full suite now **219 tests** (was 191), zero failures.

---

## [2026-06-19 11:59:59 UTC]

CHG-0041 — Frontend: Settings screen (company / profile / preferences)

- New **Settings** screen (`/settings`) replacing the last placeholder, so every nav item now has a
  real UI. Three cards:
  - **Company** — read-only code, functional currency, fiscal-year-start (localised month name);
    editable display name / legal name / tax ID (NPWP) / time zone via `PUT /api/v1/companies/{id}`.
    Editing is gated on the `Admin.Companies` permission (read-only notice otherwise). When the user
    has more than one company, a selector picks which to edit.
  - **Profile** — name / email / roles from the session.
  - **Preferences** — light/dark theme toggle and EN/ID language selector (persisted), reusing the
    existing theme store + i18n.
- New `lib/company.ts` + `types/company.ts`; added a **Settings** entry to the ⌘K command palette.
- i18n: full `settings` block in EN + ID.
- **Verified:** `npm run build` (vue-tsc + vite) green; e2e against the running API — `GET /companies`
  lists the company, `PUT` updates name/legal-name/tax-id (200, persisted on re-fetch), dev company
  restored to its seeded state afterward.

---

## [2026-06-18 16:46:08 UTC]

CHG-0040 — Idempotency for posting/create commands (ADR-0021)

- Added command-level idempotency so a retried request never double-posts. New
  `IdempotencyBehavior` MediatR pipeline behavior (registered between Logging and Validation):
  for commands marked `IIdempotentCommand` returning `Result<Guid>`, it derives a key
  `{tenant}:{commandType}:{Idempotency-Key}` and short-circuits a replay with the original id.
- New abstractions in `Accountrack.Application.Abstractions.Idempotency`: `IIdempotentCommand`
  marker, `IIdempotencyContext` (request key), `IIdempotencyStore`. SQL store
  (`Infrastructure.Common`) persists keys in `platform.IdempotencyKeys` over its own short-lived
  connection (never the shared cross-module transaction); table ensured at startup.
- Host wiring: `HttpContextIdempotencyContext` reads the `Idempotency-Key` header; store registered
  as a singleton bound to the Default connection.
- Marked commands: `CreateSalesOrderCommand`, `CreatePurchaseOrderCommand`,
  `PostGoodsReceiptCommand`, `PostPurchaseInvoiceCommand`, `PostSupplierPaymentCommand`,
  `PostDeliveryOrderCommand`, `PostSalesInvoiceCommand`, `PostCustomerPaymentCommand`,
  `PostJournalCommand`.
- Frontend: axios request interceptor sends a fresh `Idempotency-Key` (crypto.randomUUID) on every
  POST/PUT/PATCH so a transport retry replays rather than double-posts.
- Tests: new `Accountrack.BuildingBlocks.UnitTests` with 5 behavior tests (first call records;
  replay returns stored id without running the handler; no key / non-idempotent command bypass the
  store; failed results are not recorded). All green; full solution builds.
- **Verified (e2e):** posting the same journal twice with one `Idempotency-Key` returned the
  identical id (no second journal); a new key produced a new journal.
- **Known limitation** (documented in ADR-0021): the key is recorded after commit, leaving a narrow
  crash-between-commit-and-save window. Exactly-once needs the key written in the same transaction —
  a future hardening step with the durable outbox.

---

## [2026-06-18 15:13:50 UTC]

CHG-0039 — Frontend: Approvals screen (pending list + approve/reject)

- New **Approvals** screen (`/approvals`) listing the current user's pending approval requests
  (`GET /api/v1/approval-requests/mine`): document type, reference, and level progress
  (current/max), with **Approve** / **Reject** actions.
- Decision modal with an optional comment → `POST .../approve` or `.../reject`, then refreshes the
  list. Localised (EN/ID), incl. document-type labels.
- API/types: `lib/approval.ts`, `types/approval.ts`. Replaces the last non-Settings nav placeholder.
- **Verified:** frontend `npm run build` green; `/mine` smoke returns success (empty in dev, since no
  approval rules are seeded → submissions auto-approve; the screen shows its empty state).
- All core modules now have real UIs (Dashboard, Sales, Purchasing, Inventory, Accounting, Master
  data, Approvals); Settings remains a placeholder.

---

## [2026-06-18 14:57:38 UTC]

CHG-0038 — Frontend: ⌘K command palette

- Added a **command palette** opened with **⌘K / Ctrl+K** (or by clicking the sidebar search, now
  wired up). Type to fuzzy-filter; ↑/↓ to move, Enter to run, Esc to close; mouse hover also selects.
- Commands: **Navigate** to any module/screen (dashboard, sales + new + receive payment, purchasing +
  new + pay supplier, inventory, the three accounting reports, the four master-data tabs) and
  **Actions** (toggle light/dark, switch language EN/ID, sign out). Labels are localised, so the
  palette is bilingual too.
- New `useCommandPalette` composable (shared open-state singleton); `CommandPalette` mounted once in
  the app shell.
- **Verified:** frontend `npm run build` green (vue-tsc).
- Next frontend: Approvals screen, Settings; (record search within the palette is a later enhancement).

---

## [2026-06-18 14:52:02 UTC]

CHG-0037 — Frontend: Bahasa Indonesia locale + language toggle

- Added the **`id` (Bahasa Indonesia)** locale — a full translation of every UI string mirroring the
  EN catalog (nav, login, dashboard, Sales, Purchasing, Inventory, Accounting, Master data, statuses).
  Satisfies the CLAUDE.md non-negotiable: English default + Indonesian supported.
- **Language toggle** (`LanguageToggle`, EN ⇄ ID) in the top bar and on the login screen; choice
  persisted in `localStorage` and restored on load (English default).
- `i18n/index.ts` registers both locales with `savedLocale()`/`persistLocale()`; English remains the
  fallback. Number/money formatting stays `id-ID` regardless of UI language.
- **Verified:** frontend `npm run build` green (vue-tsc).
- Next frontend: ⌘K command palette, Approvals screen, Settings.

---

## [2026-06-18 14:46:41 UTC]

CHG-0036 — Frontend: Master Data screens (products / customers / suppliers / warehouses)

- New **Master data** area (`/master-data`) with a tabbed layout and four list+create screens, so the
  app is self-sufficient (no more API-only setup):
  - **Products** — list + create (code, name, unit-of-measure, optional category, stock-tracked/
    sold/purchased flags).
  - **Customers** — list + create (code, name, tax id, payment terms, credit limit).
  - **Suppliers** — list + create (code, name, tax id, payment terms).
  - **Warehouses** — list + create (code, name, address).
- Added a reusable **`AppModal`** (teleported overlay, Esc/backdrop close) used by the create forms.
- API/types: `lib/masterData.ts` gains `unitsOfMeasure`/`productCategories` + `create*`;
  `types/masterdata.ts` typed DTOs. Nested routes under `/master-data`.
- **Verified:** frontend `npm run build` green; create endpoints smoke — warehouse, customer,
  supplier, and product (UoM "PCS") all created successfully.
- Next frontend: ⌘K command palette, `id` locale, Approvals screen.

---

## [2026-06-18 14:21:25 UTC]

CHG-0035 — Frontend: Inventory — stock on-hand + stock card

- **Stock on hand** (`/inventory`) — DataTable of buckets (product, warehouse, on-hand qty,
  moving-average cost, value); rows open the stock card.
- **Stock card** (`/inventory/stock-card?productId=&warehouseId=`) — the product's movement ledger
  (newest first): date, type (labelled), source, signed qty (inbound green / outbound red), unit
  cost, running qty + running average cost.
- API/types: `lib/inventory.ts` (`onHand`, `stockCard`, `isInbound`), `types/inventory.ts`.
- **Verified:** frontend `npm run build` green; endpoints smoke — on-hand bucket (qty 108, value
  492.602,98) and a 12-entry stock card with correct running qty/avg and Sales/Purchasing/Manual
  sources.
- Stock levels + moving-average cost are now visible in the UI. Next frontend: Master-data screens,
  ⌘K palette, `id` locale.

---

## [2026-06-18 14:17:01 UTC]

CHG-0034 — Frontend: Accounting reports (Trial Balance, P&L, Balance Sheet)

- New **Accounting** area (`/accounting`) with a tabbed layout and three read-only reports driven by
  the GL endpoints:
  - **Trial Balance** — accounts with debit/credit, optional from/to date filter, totals + balanced
    badge (consumes the `{ totals, isBalanced, lines }` DTO).
  - **Profit & Loss** — revenue/expense sections + net profit/loss, period filter (defaults to this
    month).
  - **Balance Sheet** — assets vs. liabilities + equity (incl. current earnings), as-of date,
    balanced badge.
- API/types: `lib/reports.ts`, `types/reports.ts`. Nested routes under `/accounting` with the sidebar
  item active across all three tabs.
- **Verified:** frontend `npm run build` green; report endpoints smoke — TB balanced
  (Dr=Cr 2.759.644,21), P&L net 444.482,99 (month), Balance Sheet balanced (assets = L+E 1.007.766,19).
- The GL is now visible in the UI. Next frontend: Inventory + Master-data screens, ⌘K, `id` locale.

---

## [2026-06-18 14:08:54 UTC]

CHG-0033 — Frontend: Supplier Payment screen — completes procure-to-pay in the UI

- New **Pay supplier** screen (`/purchasing/pay-supplier`, reachable from the Purchase orders header):
  pick a supplier → its **open bills** load (amounts prefilled to outstanding, editable) → choose a
  **cash/bank account** + date/reference → **Record payment** posts `POST /api/v1/supplier-payments`
  (Dr AP / Cr Cash-Bank + AP allocation), then reloads remaining open bills.
- Generalised the open-item type to `SubledgerOpenItem` (AR + AP share it); added
  `accountingApi.apOpenItems` and `purchasingApi.createSupplierPayment`.
- **Verified:** frontend `npm run build` green; e2e smoke — open AP bill PI/202606/00001 (1.110) paid
  via account 1010 → one fewer open bill.
- **Procure-to-pay is now fully drivable from the UI** (PO → submit → receive → bill → pay), matching
  order-to-cash. Next frontend: read/reporting screens (Inventory, Accounting, Master data).

---

## [2026-06-18 14:01:03 UTC]

CHG-0032 — Frontend: Purchasing screens (PO list + create + detail → receive + bill)

- Replicated the Sales pattern for procure-to-pay, reusing the shared DataTable/StatusBadge/form kit:
  - **Purchase Orders list** (`/purchasing`) — number, supplier, date, status, total; New action; rows
    open the detail.
  - **Create PO** (`/purchasing/new`) — supplier/warehouse/date header + dynamic line-items editor.
  - **PO detail** (`/purchasing/:id`) — header + status, line table (ordered/received/invoiced),
    totals; **Submit for approval** (Draft), **Receive outstanding** (posts a goods receipt → stock +
    Dr Inventory/Cr GR-IR), **Enter bill** (posts a purchase invoice for received-uninvoiced qty →
    Dr GR-IR+VAT/Cr AP + AP open item); **Goods receipts** and **Bills** document lists with a
    "Posted" badge.
- API/types: `lib/purchasing.ts`, `types/purchasing.ts`, suppliers lookup added to `lib/masterData.ts`.
- **Backend:** `PurchaseOrderLineDto` now exposes `InvoicedQuantity` (parity with Sales) + mapping.
- **Verified:** frontend `npm run build` green; e2e smoke — PO 8 @ 90 + PPN → submit → receive (GR
  posted) → bill (PI 799,20 posted); detail reflects received/invoiced and lists both documents.
- Supplier Payment screen is the next slice. (Cleared the stale-host-bin trap again after the DTO change.)

---

## [2026-06-18 13:47:49 UTC]

CHG-0031 — Frontend: Customer Payment (receive) screen — completes order-to-cash in the UI

- New **Receive payment** screen (`/sales/receive-payment`, reachable from the Sales orders header):
  pick a customer → its **open AR invoices** load (amounts prefilled to outstanding, editable down to
  skip/part-pay) → choose a **cash/bank account** (10xx) and date/reference → **Record payment**
  posts `POST /api/v1/customer-payments` (Dr Cash-Bank / Cr AR + AR allocation), then reloads the
  remaining open items.
- API/types: `lib/accounting.ts` (`arOpenItems(partyId)`, `accounts()` + `cashAccounts` filter),
  `salesApi.createCustomerPayment`, `types/accounting.ts`.
- **Verified:** frontend `npm run build` green; e2e smoke — open AR item SI/202606/00002 (2.664)
  settled via account 1010 → 0 open items remain.
- **Order-to-cash is now fully drivable from the UI**: Sales Order → submit → deliver → invoice →
  receive payment. Next: replicate the pattern for Purchasing screens.

---

## [2026-06-16 14:28:34 UTC]

CHG-0030 — Frontend fix: content no longer floats away from the sidebar

- The routed content was centered (`mx-auto max-w-[1440px]`), so on wide screens it drifted right,
  leaving a large gap next to the sidebar and misaligning with the left-hugging top-bar title.
- Now the content container is **left-aligned** at the same gutter as the top bar (`max-w-[1600px]`,
  no auto-centering) — extra width on very wide screens falls to the right instead of as a left gap.
  Applies to every page (fixed once in `AppShell`).

---

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
