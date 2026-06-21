# INVENTORY_DESIGN.md

The inventory engine. Inventory traceability is non-negotiable (Core Principle 7). The
`InventoryTransaction` ledger is the source of truth (ADR-0014). Costing: moving average
(ADR-0015).

## 1. Principles

1. **Ledger is truth** (ADR-0014): on-hand quantity and value are *derived* from
   `InventoryTransactions`. `Product.CurrentStock` is never authoritative; `StockCostBuckets`
   is a rebuildable projection.
2. **Every physical movement is a ledger entry** — receipts, issues, adjustments, transfer legs,
   (future) production consume/receive.
3. **Moving-average costing** per **(Company × Warehouse × Product)** cost bucket (ADR-0015).
4. **Inventory and accounting agree**: each costed movement also drives a GL posting
   (POSTING_RULES.md) so the Inventory GL account reconciles to ledger valuation.

## 2. Ledger Model

```
InventoryTransaction  (append-only, immutable)
├── ProductId, WarehouseId           (the cost bucket, with CompanyId)
├── Type    ∈ { Receipt, Issue, AdjustIn, AdjustOut,
│               TransferOut, TransferIn,
│               ProductionConsume, ProductionReceive }   (last two: future)
├── Quantity         : decimal(19,6)   (signed by intent; Type disambiguates)
├── UnitCost         : Money
├── TotalCost        : Money           (= Quantity × UnitCost, the costed value moved)
├── MovementDate
├── SourceModule, SourceDocumentId     (traceability / drill-down)
├── RunningQtyAfter      : decimal      (snapshot for audit/reconstruction)
└── RunningAvgCostAfter  : Money        (snapshot)
```

```
StockCostBucket  (projection; one row per Company×Warehouse×Product)
├── OnHandQty   : decimal
├── AvgUnitCost : Money
└── Version     : rowversion            (serialization point, ADR-0021)
```

The bucket is the **serialization point**: cost-changing movements take a row-update on the
bucket (optimistic `Version` or `UPDLOCK`) so concurrent receipts/issues can't corrupt the
average.

## 3. Moving-Average Math

Maintained per cost bucket.

**Receipt** (qty `q` at unit cost `c`):
```
newQty       = onHand + q
newAvgCost   = (onHand*avgCost + q*c) / newQty      (if newQty > 0)
```

**Issue** (qty `q`): consumes at the current `avgCost`. `avgCost` is unchanged by an issue.
```
costOfIssue  = q * avgCost
newQty       = onHand - q                            (must be ≥ 0 unless negative stock allowed)
```
`costOfIssue` is returned to the caller so Sales can post COGS (POSTING_RULES.md).

**Adjustment**: increase behaves like a receipt at a given/last cost; decrease like an issue.

**Transfer**: `TransferOut` issues at source bucket's `avgCost`; `TransferIn` receives into the
destination bucket at that same unit cost (cost travels with the goods).

Rounding: `TotalCost` and `avgCost` use decimal money scale; residual rounding on valuation is
reconciled so ledger value = Σ TotalCost.

## 4. Negative Stock — ADR-0016

- **Default: disallowed.** An Issue that would make `newQty < 0` is rejected with a clear error.
- **Configurable per company** (`Inventory.AllowNegativeStock = true`): the issue proceeds at the
  last known `avgCost`; on the next receipt the bucket is reconciled. This is opt-in because it
  weakens valuation accuracy.
- **Implemented (CHG-0073):** the flag is a `CompanySetting` toggled in Settings (admins only) and
  read across modules via `ICompanyDirectory.GetBoolSettingAsync`. The policy is resolved once, in
  `InventoryLedgerService.IssueAsync`, so it applies uniformly to deliveries, purchase returns,
  adjustments, transfers, and opname — callers no longer pass an `allowNegative` flag.

## 5. Chronology & Back-Dating — ADR-0017

- Movements post in **chronological order** within an Open period.
- Back-dating into a **Closed/Locked** period is **forbidden** (mirrors accounting periods).
- Back-dating within the **current open period** is allowed but triggers a **forward recompute**
  of `RunningQty/RunningAvgCost` for the affected bucket from the insertion point, and reposts
  any dependent COGS deltas via reversal+repost. Because closed periods are immutable, recompute
  is bounded to the open period.

## 6. Operations → Ledger + GL

| Operation | Ledger | GL (POSTING_RULES.md) |
|---|---|---|
| Purchase Goods Receipt | Receipt(+q, c=PO/receipt cost) | Dr Inventory / Cr GR-IR |
| Sales Delivery / Shipment | Issue(−q at avg) | Dr COGS / Cr Inventory |
| Stock Adjustment (in/out) | AdjustIn/AdjustOut | Inventory ↔ Variance |
| Stock Transfer | TransferOut + TransferIn | none, or cross-account at avg |
| Stock Opname (count) | Adjust to match counted qty | via adjustment |
| Sales Return | Receipt (goods back) | Dr Inventory / Cr COGS |
| Purchase Return | Issue (goods out) | Cr Inventory / Dr AP or GR-IR |
| Production consume/receive | Issue/Receipt (future) | WIP postings (future) |

All costed movements that hit Accounting are **atomic** with the GL posting
(INTEGRATION_EVENTS.md §2).

## 7. Stock Opname (Physical Count)
- Create an opname document → freeze/snapshot book quantities → enter counted quantities →
  system computes variance per line → posting an opname generates the necessary AdjustIn/Out
  ledger entries and variance journals. Large variances can require approval (WORKFLOW_APPROVAL.md).
- **Implemented (slice 2, CHG-0057):** a per-product opname (`POST /api/v1/stock/opname`) — enter the
  counted quantity for a product/warehouse, the system computes the variance against on-hand and
  posts the reconciling AdjustIn/Out ledger entry plus its Inventory↔Variance journal atomically (an
  exact match posts nothing). A multi-line opname document (snapshot/freeze) and large-variance
  approval are a later refinement.

## 8. Units of Measure
- Products have a base UoM; transactions may be entered in alternate UoMs and converted via
  `UomConversions` to base before ledger posting. Costing is always in base UoM.

## 9. Valuation & Reports
- **Stock on hand** (qty + value) per warehouse/product, as-of date → reconstructable from the
  ledger.
- **Inventory valuation report** must equal the Inventory GL account balance (reconciliation
  control). Implemented (CHG-0062): value by product at moving-average cost vs the GL Inventory
  control-account balance (read cross-module via `IGeneralLedgerBalances`), with a difference +
  Reconciled flag; a sub-unit moving-average rounding residue is tolerated.
- **Movement / stock-card report**: full per-product transaction history with running balances
  (drill-down to source document).

## 10. Future: FIFO / Lot / Batch (ADR-0015 rationale)
The ledger reserves an optional `CostLayers` structure (ReceiptTxnId, RemainingQty, UnitCost).
Switching a product (or company) to FIFO becomes: maintain layers on receipt, consume
oldest-first on issue — **a costing-strategy plug-in, not a schema migration**. Manufacturing
(WIP) reuses ProductionConsume/ProductionReceive movement types already defined.

## 11. Correctness Risks & Controls

| Risk | Control |
|---|---|
| Average corruption under concurrency | Serialize on `StockCostBucket.Version` (ADR-0021) |
| Out-of-order / back-dated costing | Chronology rule + bounded forward recompute (§5) |
| Negative stock breaking average | Disallow by default; opt-in reconcile (§4) |
| Ledger ≠ GL Inventory account | Reconciliation report (§9), atomic posting (§6) |
| Transfer losing cost | Cost travels on TransferIn at source avg (§3) |
| Double movement on retry | Idempotency keys (ADR-0021) |

## 12. Test Coverage (high priority — TESTING.md)
Moving-average math across receipt/issue/return sequences; concurrency (parallel receipts/issues
keep a correct average); negative-stock rejection and opt-in path; transfer cost carry; opname
variance; back-dating recompute within open period and rejection in closed period; ledger↔GL
reconciliation; idempotent re-posting.
