# ADR-0033: Back-dated in-period inventory recompute (moving-average replay + delta adjusting journals)

- **Status:** Accepted — Option A (forward replay + delta adjusting journals)
- **Date:** 2026-07-06
- **Deciders:** Project owner (databroindonesia@gmail.com)
- **Tags:** inventory · accounting

## Context

[ADR-0017](../DECISIONS.md) already sets the **policy**: inventory/journal transactions post in
chronological order; back-dating into a **closed/locked** period is forbidden; back-dating **within
the current open period is allowed** and "triggers a forward recompute of the moving average for the
affected cost bucket." That recompute has never been implemented — it is the outstanding
**Inventory back-dating recompute** debt (STATUS.md, INVENTORY_DESIGN.md).

Today the moving average is **forward-only**:

- `InventoryLedgerService.Receive/Issue` mutate the `StockCostBucket` running average in the order
  movements arrive, and each `InventoryTransaction` records a snapshot of `RunningQtyAfter` /
  `RunningAvgCostAfter` at post time ([StockCostBucket.cs], [InventoryTransaction.cs]).
- Insert a movement dated **before** existing ones and nothing replays: the later averages, the
  on-hand valuation, and every subsequent issue's **COGS** are silently wrong.

The hard part is not the quantity/average replay — it is the **General Ledger**. When a back-dated
receipt lowers the average that a later delivery should have used, that delivery's COGS (posted
`Dr COGS / Cr Inventory` at issue cost — [DeliveryOrders.cs]) is now wrong. But
[ADR-0009](../DECISIONS.md) makes posted journals **immutable — corrections are reversal-only** (a
non-negotiable, CLAUDE.md #28). So a recompute **may not edit** the original COGS journals; it must
reconcile the GL some other way. This is a genuine accounting-policy decision, hence an ADR before
code.

Two supporting clarifications this ADR relies on:

- **The ledger's running columns are a rebuildable projection.** `InventoryTransaction` is
  append-only for its *facts* (quantity, unit cost, type, date, source), but `RunningQtyAfter` /
  `RunningAvgCostAfter` are a **derived cache** — [ADR-0014](../DECISIONS.md) already states cached
  projections "must be rebuildable from the ledger." Recompute may therefore rewrite those two
  derived columns in place without violating ledger immutability.
- **Scope is bounded by the open period** ([ADR-0010](../DECISIONS.md)/ADR-0017): a recompute can
  only ever touch the current open period, so the replay is bounded and closed periods stay frozen.

## Decision

We will implement in-period back-dated recompute as a **forward replay of the affected cost bucket**,
bounded to the current open period, that (a) rewrites the derived running columns and the bucket's
final average, and (b) posts **new delta adjusting journals** for any COGS / inventory-value
differences — **never** editing posted journals or the immutable ledger facts.

Concretely:

1. **Period guard.** Reject any movement dated in a closed/locked period (ADR-0010/0017). Recompute
   only touches the current open period.
2. **Chronological insert.** Order a bucket's movements by `(MovementDate, tie-break sequence)` and
   insert the back-dated movement at its chronological position.
3. **Replay.** From the insertion point to the latest movement in the bucket, recompute
   `RunningQtyAfter` / `RunningAvgCostAfter` and each issue's cost at the corrected running average.
   Update those derived columns in place (rebuildable projection, ADR-0014); the `StockCostBucket`
   ends at the corrected final quantity/average.
4. **GL reconciliation via delta journals.** For each already-posted movement whose recomputed cost
   differs from the cost originally posted, accumulate the difference and post **one net adjusting
   journal** (consolidated per recompute, referencing the triggering movement): the net inventory-vs-COGS
   difference as `Dr/Cr Inventory` ↔ `COGS / Inventory Revaluation`. Accounts come from the
   posting-rule engine ([ADR-0024](../DECISIONS.md)), never hardcoded. **Original journals are
   untouched** (ADR-0009).
5. **Atomicity & concurrency.** The whole replay + adjusting journal(s) commit in one cross-module
   transaction (`ICrossModuleUnitOfWork`), serialized on the `StockCostBucket` RowVersion, and are
   idempotent ([ADR-0021](../DECISIONS.md)).
6. **Auditability.** The recompute is recorded (triggering movement, count of affected movements, net
   delta) via audit log / Process Tracker.

## Options Considered

1. **Forward replay + delta adjusting journals (chosen).** Recompute the bucket forward; post one net
   correcting journal for the COGS/value delta.
   - **Pros:** respects journal immutability (ADR-0009); GL stays reconciled to inventory value;
     bounded to the open period; moderate complexity; gives effect to ADR-0017 as written.
   - **Cons:** introduces a replay engine + an adjusting-journal path; replay is O(movements after the
     insertion point in that bucket).
2. **Reverse-and-repost each affected issue.** For every later issue, reverse its journal and repost
   at corrected cost.
   - **Pros:** reuses existing reversal machinery; each correction individually traceable.
   - **Cons:** heavy audit noise (two extra journals per affected issue); larger transactions; poor
     for buckets with many subsequent issues.
3. **Recompute quantities/averages only; defer COGS to a periodic revaluation run.**
   - **Pros:** simplest hot path.
   - **Cons:** GL is temporarily inconsistent with inventory value between the back-date and the
     revaluation; conflicts with "GL in step with the ledger"; needs a separate scheduled process.
4. **Forbid in-period back-dating entirely.**
   - **Pros:** trivial.
   - **Cons:** **contradicts ADR-0017**, which explicitly allows it; blocks the legitimate motivating
     case (a late-entered earlier purchase). Would require superseding ADR-0017.

## Consequences

- **Positive:** correct moving average and valuation after legitimate late entries; GL stays
  reconciled; immutability and closed-period locks preserved; policy (ADR-0017) finally has an
  implementation.
- **Negative:** a new recompute engine in Inventory and an adjusting-journal posting path; we must
  state explicitly that the running snapshot columns are a rebuildable projection; replay cost grows
  with bucket activity in the open period.
- **Open edge — transfers.** ADR-0015 has transfers carry cost between warehouses, so a back-dated
  movement in a source bucket can change a later transfer's cost into a **destination** bucket,
  cascading across buckets. **Initial scope: single-bucket replay**; a back-date that sits before a
  stock transfer out of the bucket is rejected with a clear error (the operator reverses/re-enters the
  transfer), with cross-bucket cascade recompute as a later enhancement.
- **Follow-ups:** implement the replay in `IInventoryLedger`; add posting-rule keys for the
  revaluation/COGS-adjustment accounts (ADR-0024); update INVENTORY_DESIGN.md and POSTING_RULES.md;
  tests (unit: replay math; integration: back-dated receipt changes later COGS and posts the delta
  journal; closed-period rejection; transfer-boundary rejection); STATUS.md / MODULES.md; CHANGELOG on
  implementation.

## References

- ADR-0009 (immutable journals), ADR-0010 (fiscal periods), ADR-0014 (ledger source of truth),
  ADR-0015 (moving-average costing), ADR-0016 (negative stock), ADR-0017 (chronological posting /
  in-period back-dating), ADR-0021 (idempotency + RowVersion), ADR-0024 (configurable posting rules).
- INVENTORY_DESIGN.md, POSTING_RULES.md, ACCOUNTING_DESIGN.md.
