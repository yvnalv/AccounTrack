# ADR-0037: FIFO back-dated in-period recompute (layer reconstruction + delta journals)

- **Status:** Accepted
- **Date:** 2026-07-08
- **Deciders:** Product owner, engineering
- **Tags:** inventory | accounting

## Context

ADR-0033 implemented back-dated in-period recompute for **moving-average** products: inserting a
movement dated before existing ones replays the whole cost bucket, restates the derived ledger
columns, and posts a **net delta adjusting journal** for the COGS/inventory difference of the
already-posted later issues — without editing the immutable posted journals (ADR-0009).

ADR-0034 then added **FIFO** costing as a per-product option, but explicitly scoped it **forward-only**:
back-dating a FIFO movement was rejected (`BR-INV-10`) because FIFO values issues from **cost layers**
(`StockCostLayer`), not a single running average, and the moving-average replay does not reconstruct
layers. This ADR closes that follow-up — the last remaining inventory back-dating debt other than the
cross-bucket (transfer) cascade.

The hard part is the same as ADR-0033 plus one addition: inserting an earlier movement changes **which
layers each later issue consumes**, so every layer's *remaining quantity* must be rebuilt (a layer that
was fully spent can be re-opened; a back-dated receipt introduces a new oldest layer), and the derived
GL correction must reconcile **without touching posted journals or immutable ledger facts**.

## Decision

Mirror ADR-0033's Option A for FIFO, adding layer reconstruction:

- A new pure, stateless **`FifoReplay`** domain engine replays a bucket's movements in chronological
  order: it re-opens a layer for every inbound movement, consumes the **oldest layers first** for every
  outbound (rounding-matched to `FifoCosting`/`StockCostLayer`), and returns each row's restated cost
  plus **each inbound layer's final remaining quantity**. It also tracks the derived display average so
  an unchanged replay reproduces the forward numbers exactly and a negative-stock shortfall is costed
  identically to the forward path.
- The inventory ledger service branches on `StockCostBucket.CostingMethod`. For FIFO it inserts the
  new movement chronologically, runs `FifoReplay`, **restates every ledger row in place** (a rebuildable
  projection, ADR-0014), and **rebuilds every cost layer's remaining quantity** — opening a new
  `StockCostLayer` for a back-dated receipt and calling `StockCostLayer.Restate(...)` on the rest (all
  layers, including previously fully-consumed ones, are re-derived).
- The **net delta adjusting journal** is unchanged: COGS delta for later `Sales` issues, variance delta
  for later `Adjustment` issues, balanced against Inventory, posted via the posting-rule engine
  (ADR-0024), dated at the back-dated movement so the closed-period guard applies. The whole replay +
  reconstruction + adjustment commit in one cross-module transaction, serialized on the bucket
  RowVersion (ADR-0021).
- **Still rejected** (unchanged from ADR-0033): a back-date before a later **transfer or production**
  movement (cross-bucket cost flow — the separate cascade follow-up), and a back-date that would drive
  stock negative when negative stock is disallowed. Manual receipts remain non-back-datable (they commit
  through the module unit of work, not the cross-module transaction, so they cannot post the GL
  correction).

## Options Considered

1. **FIFO replay + layer reconstruction + delta journals (chosen)** — symmetric with the moving-average
   path (same insert → replay → restate → net journal shape), reuses the delta-journal poster, keeps the
   ledger the source of truth and valuation GL-reconciled. Cost: a second pure replay engine and the
   layer-remainder rebuild.
2. **Reverse-and-repost each affected later issue** — re-issues at the corrected layers, but floods the
   ledger and GL with reversal noise for what is a derived-value correction (same reason ADR-0033
   rejected it).
3. **Keep FIFO forward-only** — contradicts ADR-0017 (in-period back-dating is permitted) and leaves a
   real gap for late FIFO purchase entries.

## Consequences

- **Positive:** FIFO now supports the same legitimate late-entry correction moving average already did;
  layers, valuation, and the GL stay reconciled; posted journals and closed-period locks preserved; the
  moving-average path and its tests are untouched. `BR-INV-10` flips from a limit to a supported case.
- **Negative / trade-offs:** a FIFO back-date replays the whole in-period bucket **and** rewrites its
  layer remainders (cost grows with in-period activity); a second replay engine to maintain alongside
  the moving-average one.
- **Follow-ups:** cross-bucket (transfer/production) back-dating cascade — still deferred, still rejected
  for both costing methods; optional company-level default costing method.

## References

- [ADR-0033](0033-inventory-backdated-recompute.md) back-dated moving-average recompute
- [ADR-0034](0034-inventory-fifo-costing.md) FIFO costing as a per-product option
- [ADR-0009](../DECISIONS.md) posted journals immutable · [ADR-0014](../DECISIONS.md) ledger is truth · [ADR-0024](../DECISIONS.md) posting-rule engine
- [INVENTORY_DESIGN.md](../INVENTORY_DESIGN.md) §10 · [BUSINESS_RULES.md](../BUSINESS_RULES.md) BR-INV-5, BR-INV-9, BR-INV-10
