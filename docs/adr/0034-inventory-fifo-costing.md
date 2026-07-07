# ADR-0034: FIFO costing as a per-product option alongside moving average

- **Status:** Accepted
- **Date:** 2026-07-07
- **Deciders:** Product owner, engineering
- **Tags:** inventory | accounting

## Context

MVP costing is moving weighted average per (Company × Warehouse × Product) — ADR-0015. Real
trading and distribution businesses frequently need **FIFO** (first-in, first-out) for some
goods (e.g. dated stock, imported lots) while keeping average for others. INVENTORY_DESIGN.md §10
always anticipated this and reserved an optional cost-layer structure so FIFO would be "a
costing-strategy plug-in, not a schema migration".

The ledger (`InventoryTransaction`) stays the source of truth (ADR-0014); costing only changes how
an *issue* is valued. We must not disturb the existing moving-average behavior, must keep inventory
valuation reconciling to the GL Inventory control account (BR-INV-7), and must respect module
boundaries (Inventory must not read Master Data tables directly — ADR-0007).

## Decision

We will make the **costing method a per-product choice** (`MovingAverage` | `Fifo`), set at product
creation and **immutable thereafter** (like the base UoM — changing it would corrupt historical
valuation). A company default may seed the choice, but the product owns it.

- A product's **stock buckets inherit its method** at bucket creation (stamped on
  `StockCostBucket.CostingMethod`), read across the module boundary via
  `IMasterDataLookup.GetCostingMethodAsync`.
- **FIFO uses cost layers.** Each inbound movement opens a `StockCostLayer` (source txn, unit cost,
  remaining qty, movement date). An issue consumes the **oldest open layers first**; the cost of
  goods issued is the sum of the consumed layer costs (pure, unit-tested `FifoCosting`). The bucket
  still tracks on-hand and a *derived* average for display; FIFO on-hand **value is the sum of open
  layers**, which the inventory-valuation report uses so it reconciles to the GL exactly.
- **Moving average is unchanged** — the same code path, no layers.
- **v1 scope:** FIFO is **forward-only**. Back-dating a movement for a FIFO product is **rejected**
  (`BR-INV-10`); FIFO layer reconstruction across an inserted movement is a later enhancement (the
  moving-average back-dated replay of ADR-0033 does not apply to layers). Negative stock (opt-in,
  ADR-0016) costs any layer shortfall at the bucket's last average, reconciled on the next receipt.

## Options Considered

1. **Per-product method + cost layers (chosen)** — matches real mixed-method businesses; isolates
   FIFO to a new layer table + a branch in the ledger service; moving average untouched. More
   moving parts (a new entity, valuation branch).
2. **Per-company method** — simpler config, one method for everything; rejected because businesses
   legitimately mix methods per item.
3. **Global FIFO for all buckets (average derived)** — one code path, but rewrites all existing
   moving-average behavior and migration risk for zero business gain where average is wanted.

## Consequences

- **Positive:** FIFO available without a schema rewrite; average path and its tests unchanged;
  valuation stays GL-reconciled; the method is locked to protect historical valuation.
- **Negative / trade-offs:** FIFO back-dating is unsupported in v1 (clear reject, not silent);
  a FIFO bucket keeps a layer table that must be scanned for valuation; the bucket's average is
  only a display figure for FIFO products.
- **Follow-ups:** FIFO back-dated layer reconstruction; cross-bucket (transfer) back-dating
  (deferred to a future ADR-0035); optional CSV/Excel import column for costing method; a
  company-level default costing-method setting.

## References

- [ADR-0015](../DECISIONS.md) moving-average costing · [ADR-0014](../DECISIONS.md) ledger is truth
- [ADR-0033](0033-inventory-backdated-recompute.md) back-dated moving-average recompute
- [INVENTORY_DESIGN.md](../INVENTORY_DESIGN.md) §3, §9, §10 · [BUSINESS_RULES.md](../BUSINESS_RULES.md) BR-INV-2, BR-INV-10
