# STATUS.md — Project Milestones & "You Are Here"

The single place to see **where the build is and what to do next**, so any session can resume in
context. Complements: [ROADMAP.md](ROADMAP.md) (the plan), [`../CHANGELOG.md`](../CHANGELOG.md)
(the history), [MODULES.md](MODULES.md) (per-module status table).

> Update this file whenever a module/slice lands (alongside the CHANGELOG entry).

## Snapshot

- **As of:** 2026-07-09 (last change **CHG-0128**)
- **Build:** green — backend `net8.0` (374 tests); **frontend** `frontend/` builds (vue-tsc + vite).
  Latest: **hardening — auth rate limiting + SPA security headers (CHG-0128, SECURITY.md §5)** — the
  anonymous auth endpoints (`/auth/login`, `/register`, `/refresh`) now enforce a per-client fixed-window
  rate limit (.NET 8 `AddRateLimiter`; 429 `RATE_LIMITED` + `Retry-After`; configurable `RateLimiting:Auth`,
  default 20/60 s); the SPA's Nginx now sends CSP + `nosniff`/`X-Frame-Options: DENY`/`Referrer-Policy`,
  and the Vite build ships no inline bootstrap script so it runs under a strict `script-src 'self'`.
  Verified in Docker (20 pass → 429; valid login 200; headers + assets/font 200).
  Latest: **web RBAC + role-based dashboards + list filters/mobile (CHG-0127)** — the SPA now enforces
  permissions (route guard → 403 page; sidebar/⌘K/buttons hidden by permission; backend
  `RequireAuthorization` remains the hard wall, SECURITY.md §2); a second **role-based operational
  dashboard** (Sales/Purchasing/Inventory KPIs) for non-finance users; per-list status/warehouse filters
  + `hideOnMobile` columns + responsive toolbars. Sign-out redirect fixed.
  Latest: **manual Adjust + Opname exactly-once (CHG-0126, ADR-0021)** — the last two GL-posting manual
  stock commands are now idempotent (they already commit through the coordinator); `StockOpnameResult`
  is addressable, with an exact-match no-op having no id. Every GL-posting manual stock command
  (Receive/Adjust/Opname) is now replay-safe.
  Latest: **cross-bucket back-dating — moving average (CHG-0125, ADR-0038 Phase 1)** — a back-dated
  moving-average movement that cascades through a warehouse transfer now recomputes across buckets
  (new pure `CrossBucketMovingAverageReplay`; `TransferGroupId` links the legs; one net delta journal).
  Implemented as a single global-chronological pass (cost cycles impossible by construction — no topo
  sort). Deferred (Phase 2): directly back-dating a transfer document, and FIFO cross-bucket.
  Latest: **cross-bucket back-dating design (CHG-0124, ADR-0038)** — the design doc behind the above.
  Latest: **ReceiveStock exactly-once (CHG-0123)** — manual goods-in is now idempotent: the idempotency
  machinery was generalized beyond `Result<Guid>` (new `IIdempotentResult` marker; `StockMovementResult`
  implements it) and `ReceiveStock` routed through the cross-module coordinator, so a retried receive
  with the same `Idempotency-Key` no longer double-posts stock. Replay is id-only (Option A). **This
  closes the idempotency backlog** — every posting/create flow is now exactly-once.
  Latest: **dashboard chart code-split (CHG-0122)** — ECharts moved into an async `AppChart` chunk, so
  the `DashboardView` route chunk drops 534 KB → ~10 KB and the chart library loads on demand (no visual
  change). Clears the Vite >500 KB warning on the route chunk.
  Latest: **brand typeface, self-hosted (CHG-0121)** — Plus Jakarta Sans (SIL OFL 1.1) embedded in the
  QuestPDF renderer (documents + report PDFs now match the SPA) and self-hosted in the frontend, dropping
  the Google Fonts CDN so the app is offline-clean on the VPS. Verified live: report PDF embeds the font;
  SPA serves the woff2 with no CDN reference.
  Latest: **draft creates exactly-once (CHG-0120)** — Create Sales Order / Purchase Order / Expense
  Draft now commit through the cross-module coordinator, so the idempotency key is written atomically
  with the draft; a retried create with the same `Idempotency-Key` returns the original id instead of
  creating a duplicate (which would burn a sequence number). (The last gap, `ReceiveStock`, was closed
  in CHG-0123.)
  Latest: **FIFO back-dated in-period recompute (CHG-0119, ADR-0037)** — FIFO products now support
  back-dating within the open period (lifting the ADR-0034 forward-only limit): a new pure `FifoReplay`
  engine replays the bucket consuming oldest layers first, rebuilds every cost layer's remaining
  quantity, restates later issues' COGS, and posts one net adjusting journal. Cross-bucket (transfer)
  back-dating stays rejected for both costing methods (the last remaining inventory back-dating item).
  Latest: **document browse lists (CHG-0118)** — four company-wide list pages (Sales Invoices +
  Payments; Purchasing Bills + Payments) completing the list→detail web; new global list endpoints
  (`GET /sales-invoices`, `GET /purchase-invoices`) and optional party filter on the payment lists;
  reachable from the order-list toolbars and ⌘K palette.
  Latest: **transactional document detail pages (CHG-0117)** — six read-only detail pages (Delivery
  Order, Sales Invoice, Customer Payment; Goods Receipt, Purchase Invoice/Bill, Supplier Payment),
  turning previously dead-end rows into a navigable web: the Deliveries list opens the delivery,
  SO/PO detail delivery/receipt/invoice numbers are clickable, and customer/supplier *Payments* rows
  open the payment. Frontend-only (all six backend detail endpoints already existed).
  Latest: **master data as dedicated sidebar pages** with 4 insight cards + informative columns —
  in-stock (products), receivable (customers), payable (suppliers), stock value/SKUs (warehouses)
  (CHG-0114); **customer / supplier / warehouse detail pages** — clickable rows open a detail with
  KPIs + transaction history (open invoices/bills, orders, payments) and per-warehouse stock contents
  (CHG-0115); **actionable balances** (Receive payment / Pay supplier buttons preselect the payment
  screen) + a **product detail page** tracing stock-by-warehouse and movement history (CHG-0116).
  Latest: **richer product list** (category/UoM/prices/costing columns; CHG-0112); **Deliveries list**
  (Sales → Deliveries) + the delivery figure clarified as **cost of goods (COGS)**, not the sale price
  (CHG-0113).
  Latest: **pricing reworked (CHG-0111, ADR-0036, supersedes ADR-0035)** — the base price now lives on
  the product (`SalePrice`/`PurchasePrice`, auto-fills order lines); price lists became **shared
  discount rules** (a % off base + optional per-product overrides), fixing the per-customer
  maintenance burden. Resolution: override → % off base → product base → manual. (Also fixed committed
  merge-conflict markers left in DECISIONS.md by the ADR-0034/0035 concurrent merge.)
  **Deployed:** live on a VPS behind the owner's Nginx + Let's Encrypt (SAN cert), reusing an existing
  dockerized PostgreSQL; **auto-deploy CI/CD** (GitHub Actions → build/test → GHCR images → SSH
  `compose pull` on push to `main`; CHG-0098/0100) and **nightly PostgreSQL backups** (CHG-0100). See
  [DEPLOYMENT.md](DEPLOYMENT.md) §5/§8 and [VPS_DEPLOYMENT_GUIDE.md](VPS_DEPLOYMENT_GUIDE.md).
  **Database provider migrated SQL Server → PostgreSQL (Npgsql), CHG-0094 / ADR-0032.**
  Latest: **Expenses draft workflow + reversal** — create/edit/submit/cancel drafts and reverse posted
  vouchers, with matching UI (full parity with Sales/Purchasing; CHG-0095/0096); **General Ledger
  account filter applies on selection + control accounts selectable** (CHG-0099); **silent
  refresh-token rotation** — the SPA refreshes the access token in the background on
  401 and retries, instead of bouncing to login; sign-out revokes server-side (CHG-0090); **list export
  honors active filters** — Export downloads only the searched/filtered rows
  via a generic `POST /api/v1/export` (CHG-0089); **Settings tabs + modal overflow fix** — modals now cap to the viewport with a scrollable
  body (fixes the Add-role modal pushing its footer off-screen); Settings is tabbed per category
  (CHG-0088); **optimistic concurrency extended to all master-data + Chart-of-Accounts edits** — stale
  cross-request edits return 409 `CONCURRENCY_CONFLICT` (CHG-0087); **optimistic concurrency on SO/PO
  edits** — drafts carry a `rowVersion`; a stale cross-request
  edit is rejected with 409 `CONCURRENCY_CONFLICT` instead of clobbering (CHG-0086); **outbox
  dead-letter UI** — an "Event delivery" card in Settings (Approval.Manage) that lists
  failed events with a Retry action (CHG-0085); **outbox dead-letter visibility** —
  `Approval.Manage`-gated list + retry endpoints for events
  the dispatcher gave up on; retired the now-dead in-process publisher (CHG-0084); **durable
  transactional outbox** for approval events — staged in the approval txn, delivered async by a
  background dispatcher, exactly-once per handler via a `platform.InboxState` de-dup
  (CHG-0083); **expense voucher approvals** — threshold-gated, posts on approval (CHG-0082); **granular
  master-data + CoA permissions** (Create/Edit/Delete, replacing MasterData.Manage; CHG-0081);
  **Excel (.xlsx) master-data import** (CHG-0080); **period-close balance snapshots**
  (rebuildable, ADR-0022) with a Balances modal on the Periods screen (CHG-0079); **public
  organization sign-up** — self-serve tenant+company+admin
  provisioning with a /register page (CHG-0078); **user management** — list/create/edit users, assign
  roles + company access (CHG-0077);
  **dynamic roles & access management** — 6 seeded standard roles, editable permission matrix in
  Settings (CHG-0076); **distinct Edit/Cancel permissions for Sales & Purchasing documents** (CHG-0075);
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
  (CHG-0040); the **cross-tenant isolation suite** landed (CHG-0042). Backend debts cleared:
  **period-close snapshots** (CHG-0079) and **durable outbox** (CHG-0083) done. (VAT report — done, CHG-0043.)
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
  **6 seeded standard roles + dynamic role/permission management (CHG-0076); user-management UI —
  list/create/edit, assign roles + company access (CHG-0077); public organization sign-up — self-serve
  tenant/company/admin provisioning + /register page (CHG-0078)**.
- ✅ **Company Management** — tenants, companies, settings; tenant context (CHG-0005)
- ✅ **Audit Log** — automatic atomic before/after capture (CHG-0006)
- ✅ **Approval Workflow** — generic engine: conditional/multi-level, SoD, auto-approve (CHG-0012)
- ✅ **Process Tracker** — document lifecycle timeline; consumes approval events (CHG-0013)
- ✅ **In-process integration events** — publisher + handlers building block (ADR-0007) (CHG-0013)
- ✅ **Cross-module atomic transactions** — shared connection + unit of work (INTEGRATION_EVENTS.md §2) (CHG-0019)
- ✅ **Notification** — in-app notifications consuming approval events; list + mark-read (CHG-0014)
- ✅ **Cross-tenant isolation integration suite** — behavioral (real PostgreSQL) + model-convention
  reflection over all 11 contexts (CHG-0042). MULTI_TENANCY.md §9. (Uses local/CI PostgreSQL via env
  `ACCOUNTRACK_TEST_PG` with skip, not Testcontainers — Docker unavailable here.)

### Phase 2 — Core ERP
- 🟡 **Accounting** (slice 1 + reports + posting engine + subledgers) — chart of accounts, fiscal
  periods, double-entry journals + reversal, trial balance (CHG-0008); **Profit & Loss + Balance
  Sheet** (CHG-0016); **posting-rule / account-determination engine** + health check (CHG-0017);
  **AR/AP subledgers** — open items, allocation, aging (CHG-0018)
- ✅ **Master Data** — products, categories, units, customers, suppliers, warehouses, tax codes (CHG-0009);
  **pricing (CHG-0111, ADR-0036)** — product base price (SalePrice/PurchasePrice) + shared discount
  price lists (% off base + per-product overrides) + SO/PO line auto-fill
- 🟡 **Inventory** (slice 1 + 2) — transaction ledger, moving-average buckets, receive/adjust/transfer,
  on-hand + stock card, `IInventoryLedger` (CHG-0010); **slice 2 (CHG-0057)** — adjustments + stock
  opname post Dr/Cr Inventory↔Variance to the GL atomically (Adjust/Count UI); **per-company
  negative-stock policy (CHG-0073)**; ✅ **back-dated in-period moving-average recompute (CHG-0104, ADR-0033)** — with UI guidance + reject reasons surfaced on the Adjust/Opname forms (CHG-0107); ✅ **per-product FIFO costing (CHG-0109, ADR-0034)** — cost layers, consumed oldest-first, valuation reconciled to the GL; ✅ **FIFO back-dated in-period recompute (CHG-0119, ADR-0037)** — `FifoReplay` + layer reconstruction + delta journal; ✅ **cross-bucket (transfer) cascade — moving average (CHG-0125, ADR-0038 Phase 1)**. Remaining: cross-bucket for FIFO + directly back-dating a transfer (ADR-0038 Phase 2)
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

- **🔴 Public-launch readiness (security priority) — see [SECURITY.md §12](SECURITY.md) (CHG-0129):**
  the core ERP engine is production-grade, but **open public self-serve signup is NOT ready.**
  **Controlled / invite-only pilot is close to ready.** Blockers before opening `/register` to the
  public (all currently unimplemented): 🔴 (1) no email delivery infrastructure — root dependency;
  (2) no password reset/forgot-password; (3) no email verification on signup; (4) weak password policy
  (`MinimumLength(8)` only, no complexity/breach check); (5) no account lockout (per-IP rate limit
  CHG-0128 doesn't stop targeted stuffing). 🟠 (6) auth tokens (access + refresh) in `localStorage`
  (XSS token-theft; move refresh to httpOnly cookie); (7) no error/uptime monitoring; (8) rate limiting
  is auth-only; (9) untested DR restore. 🟡 (10) no ToS/Privacy/consent; (11) no billing/subscription
  enforcement; (12) data-subject export/delete not surfaced. Sequence: close 🔴 as one coupled epic
  (email unlocks 2–3), then 🟠, then 🟡. Full detail, evidence, and strengths in SECURITY.md §12.
- **Returns:** ✅ sales returns (CHG-0046, BR-SAL-8) + purchase returns (CHG-0047, BR-PUR-7) — credit/
  debit notes that restock/de-stock at cost, reverse Revenue/VAT/AR resp. AP/VAT-Input, and move the
  subledger; ✅ **settled-invoice refund + returns list screens (CHG-0074)** — returning a paid
  invoice refunds the excess to a chosen cash/bank account; dedicated sales/purchase returns lists.
  ✅ **standalone return-detail pages (CHG-0105)** — clickable list rows open a read-only credit/debit-note
  detail (lines, cost, totals, invoice PDF + source-document links). Returns UI complete.
- **CRUD completion (ADR-0029):** ✅ master data Edit + activate/deactivate for customers/
  suppliers/warehouses/products (CHG-0045) **and Units/Categories/Tax-codes (CHG-0060)**; **draft
  SO/PO cancel (CHG-0061) + edit (CHG-0070); CoA edit + deactivate (CHG-0071)**; **distinct
  `Sales/Purchasing.Edit` + `.Cancel` permissions (CHG-0075)**; **master-data + CoA `Create`/`Edit`/
  `Delete` permissions, replacing `MasterData.Manage` (CHG-0081)**. (BR-X-7/8.)
- **Expenses module (ADR-0030):** ✅ complete — vouchers paid from cash/bank **or on account (Cr AP,
  opens an AP subledger item)**; category→GL via posting rules; atomic Dr Expense [+ VAT Input] /
  Cr Cash-Bank|AP; category edit + activate/deactivate (CHG-0072); **threshold-gated approvals — posts
  on approval (CHG-0082)**; **draft workflow (create/edit/submit/cancel) + reversal of posted vouchers,
  with matching UI — full parity with Sales/Purchasing (CHG-0095/0096; BR-EXP-4/7)**. (BR-EXP-*.)
- **Data Import/Export (ADR-0031):** 🟡 **CSV + Excel (.xlsx) import** for all four master-data
  entities (CHG-0049/0050; **Excel import — CHG-0080**); **CSV + Excel export** across all list menus —
  master data, sales orders, purchase orders, inventory on-hand, expenses (CHG-0051, ClosedXML/MIT);
  **PDF** documents — Invoice + Quotation (CHG-0052), **purchase-document PDFs** PO + bill (CHG-0054) —
  and **report PDFs** TB/P&L/BS/VAT (CHG-0053), all QuestPDF, with the **brand logo** embedded as
  vector SVG (CHG-0054); ✅ **list export honors active filters (CHG-0089)** — Export downloads only
  the searched/filtered rows via a generic `POST /api/v1/export`; ✅ **brand typeface (CHG-0121)** —
  Plus Jakarta Sans (SIL OFL 1.1) embedded in the QuestPDF renderer so documents/reports match the SPA,
  and self-hosted in the frontend (Google Fonts CDN dropped → offline-clean on the VPS). Remaining:
  async large-file import (needs background-job infra — Hangfire, currently future). (BR-IMP-*.)
- **Accounting slice 2:** ✅ complete — **period-close balance snapshots (rebuildable, ADR-0022 —
  CHG-0079)**. (P&L + Balance Sheet — CHG-0016; posting-rule engine — CHG-0017; AR/AP subledgers —
  CHG-0018; **Cash Flow — CHG-0056**; **year-end close to retained earnings — CHG-0059**.)
- **Inventory slice 2:** ✅ GL posting on stock adjustments (Dr/Cr Inventory↔Variance) + stock opname
  done (CHG-0057); ✅ **per-company negative-stock policy (CHG-0073)**; ✅ **back-dated in-period
  moving-average recompute (CHG-0104, ADR-0033)** — replay + net Inventory↔COGS/Variance adjusting
  journal, on the cross-module paths. ✅ **per-product FIFO costing (CHG-0109, ADR-0034)**;
  ✅ **FIFO back-dated in-period recompute (CHG-0119, ADR-0037)** — `FifoReplay` + layer reconstruction
  + delta journal. ✅ **cross-bucket (transfer) cascade — moving average (CHG-0125, ADR-0038 Phase 1)** —
  new `CrossBucketMovingAverageReplay` (global-chronological pass, `TransferGroupId` links the legs, one
  net journal). Remaining (ADR-0038 Phase 2): FIFO cross-bucket + directly back-dating a transfer document.
  (Transfers are GL-neutral under a single Inventory control account.)
- **Cross-module atomic posting:** ✅ foundation done (CHG-0019) — shared connection +
  `ICrossModuleUnitOfWork`, used by Goods Receipt. Sales shipment (stock issue + COGS) and invoice
  flows will reuse it.
- **Durable transactional outbox (ADR-0007):** ✅ live for the **Approval** module (CHG-0083) —
  `ApprovalSubmitted`/`ApprovalDecided` staged in `approval.OutboxMessages` in the same txn, delivered
  by a background `OutboxDispatcherService`, exactly-once per handler via `platform.InboxState`,
  tenant restored per message via `IAmbientTenant`. ✅ **dead-letter list + retry endpoints
  (`Approval.Manage`) and the in-process publisher retired (CHG-0084)** — every event now flows
  through the outbox. (Approval is currently the only producer; other modules only consume, so there
  are no remaining in-process producers to migrate.)
- **Idempotency for atomic flows:** ✅ command-level idempotency done (CHG-0040) —
  `IdempotencyBehavior` keyed off the `Idempotency-Key` header dedupes replays of the posting/create
  commands (ADR-0021). ✅ **RowVersion optimistic concurrency on SO/PO edits (CHG-0086)** + **all
  master-data & CoA edits (CHG-0087)** — stale cross-request edits return 409. ✅ **exactly-once for
  atomic flows (CHG-0102)** — the key is written inside the same cross-module transaction as the
  effects (via `IIdempotencyScope` + `WriteInTransactionAsync`), closing the commit/`SaveAsync`
  crash-window. ✅ **draft creates exactly-once (CHG-0120)** — Create Sales Order, Create Purchase
  Order, and Create Expense Draft now commit through the cross-module coordinator, so the
  idempotency key is written atomically with the draft (a retried create with the same
  `Idempotency-Key` never burns a second sequence number). ✅ **`ReceiveStock` exactly-once
  (CHG-0123)** — the behavior/coordinator were generalized beyond `Result<Guid>` (new
  `IIdempotentResult` marker; `StockMovementResult` implements it) and `ReceiveStock` routed through the
  coordinator; replay is id-only (Option A). **Idempotency backlog complete.**
- **Frontend:** Vue 3 SPA — **pause for a UI/UX design discussion before building** (user
  preference: not template/AI-ish).
- **Surfacing built-but-hidden backend in the UI:** ✅ **in-app notifications bell (CHG-0107)** —
  the top-bar bell now lists notifications (popover, unread badge, mark-(all-)read, 60s poll);
  ✅ **Audit trail screen (CHG-0108)** — `Audit.View`-gated Settings tab over `GET /audit-entries`
  (filter by record type + date, paged, expandable before/after diff); ✅ **Process Tracker timeline
  (CHG-0108)** — lifecycle milestones on the Sales Order / Purchase Order / Expense Voucher detail
  pages via `GET /documents/{type}/{id}/timeline`. Surfacing complete.

## ▶️ Next up (recommended)

**The MVP transactional backend is complete** (procure-to-pay CHG-0019–0021, order-to-cash
CHG-0022–0025; both post atomically across modules and reconcile to the AR/AP subledgers). The
**dashboard summary endpoint** landed CHG-0026.

**Frontend** (UI/UX discussed & design language locked — see [frontend/](frontend/)): scaffolded in
CHG-0027 (`frontend/`) — app shell, light/dark toggle, login, finance dashboard. Next frontend slices:
⌘K command palette; a shared **DataTable** + **DocumentForm** in the dense register; the first real
CRUD screen (**Sales** list + Sales-Order create/detail); then the other modules' screens; i18n `id`
locale; **✅ refresh-token rotation (silent refresh + retry — CHG-0090)**; **✅ self-hosted font (CHG-0121)**.

Backend threads that can be picked up independently if desired (none block the frontend):
- **Idempotency keys** for the atomic posting flows — ✅ done (CHG-0040, ADR-0021); ✅ **same-transaction
  key write / exactly-once (CHG-0102)**; ✅ **draft creates exactly-once (CHG-0120)**; ✅ **`ReceiveStock`
  exactly-once (CHG-0123)** — machinery generalized beyond `Result<Guid>`. Idempotency backlog complete.
- **Reporting:** Cash Flow, AR/AP aging, **VAT (CHG-0043)**, **General Ledger / account detail
  (CHG-0058)**, **inventory valuation (CHG-0062)** done. Reporting suite complete.
- **Accounting:** ✅ period-close balance snapshots (CHG-0079); year-end close to retained earnings (CHG-0059).
- **Inventory slice 2:** ✅ GL posting on adjustments + stock opname done (CHG-0057); ✅ per-company
  negative-stock policy (CHG-0073); ✅ back-dated in-period recompute (CHG-0104, ADR-0033); ✅ per-product FIFO costing (CHG-0109, ADR-0034); ✅ FIFO back-dated recompute (CHG-0119, ADR-0037); ✅ cross-bucket (transfer) cascade — moving average (CHG-0125, ADR-0038 Phase 1); remaining (ADR-0038 Phase 2): FIFO cross-bucket + directly back-dating a transfer document.
- **Returns:** purchase & sales returns/credit notes.
- A dev **customer seed** (none seeded today; created via API in e2e).

After order-to-cash, the backend is MVP-functional end to end — the natural next phase is the **Vue 3
frontend**, which requires the **UI/UX design discussion** before any build (user preference: not
template/AI-ish). Pause and raise it then.

Other open threads (not blocking): **Inventory** cross-bucket (transfer) back-dating — ✅ **moving
average (CHG-0125, ADR-0038 Phase 1)**; remaining **Phase 2**: FIFO cross-bucket + directly back-dating a
transfer document (FIFO forward costing ✅ CHG-0109; FIFO back-dating ✅ CHG-0119).
(Idempotency exactly-once ✅ CHG-0102 / ReceiveStock ✅ CHG-0123; inventory back-dating ✅ CHG-0104; returns detail ✅ CHG-0105.)

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
