# ADR-0038: Cross-bucket (transfer) back-dated recompute — coordinated multi-bucket replay

- **Status:** Accepted — **fully implemented**: Phase 1 (moving average, CHG-0125), Phase 2a (FIFO
  cross-bucket, CHG-0129), Phase 2b (directly back-dating a transfer document, CHG-0130). All inventory
  back-dating debt is closed.
- **Date:** 2026-07-09
- **Deciders:** Product owner, engineering
- **Tags:** inventory | accounting

> **Implementation refinement.** The design below was drafted with a topological sort of affected
> buckets plus cycle rejection. The implementation uses a strictly simpler **single global-chronological
> pass** across all of a product's buckets (§2): because a transfer's `TransferOut` is always recorded
> before its paired `TransferIn`, one forward pass threads every transfer's cost correctly and a cost
> cycle is **impossible by construction** — so no topological sort and no cycle handling are needed. The
> `TransferGroupId` linkage and the rest of the decision stand as written.

## Context

Back-dated in-period recompute is implemented for a **single cost bucket** — both moving-average
(ADR-0033) and FIFO (ADR-0037). Inserting a movement dated before existing ones replays that one
`(product, warehouse)` bucket, restates its derived ledger columns, and posts one **net delta
adjusting journal** for the COGS/variance difference of the already-posted later issues, without
editing immutable posted journals (ADR-0009) or ledger facts (ADR-0014).

Both engines deliberately **reject** one case (`BR-INV-5` / `BR-INV-10`, `InventoryErrors.BackDatingCrossesTransfer`):
a back-date whose later movements include a **transfer** (or production consume/receive). This is the
**last remaining inventory back-dating debt**. Two flavours of it are blocked today:

1. **Cascade through a later transfer.** A back-dated movement in bucket A changes A's running cost →
   changes the cost a later `TransferOut` from A carried → that cost is what the paired `TransferIn`
   in bucket B received → B's average/layers change → later **sales** out of B have different COGS
   (a real GL effect). Rejected by both recompute paths.
2. **Directly back-dating a transfer.** `TransferStockHandler` rejects a transfer whose date precedes
   the latest movement of **either** warehouse, because it commits through the module unit of work
   (not the cross-module transaction) and cannot post the recompute correction.

Why it is hard, and why it was deferred past ADR-0033/0037:

- **A transfer is GL-neutral by itself** (cost travels between warehouses under one Inventory control
  account, `BR-INV-6`), but restating it is **not** bucket-local: the value moves into another bucket
  whose later issues *are* GL events. Correctness requires replaying **every** bucket the change
  reaches, in dependency order.
- **The transfer legs are not linked.** `TransferOut` and `TransferIn` are two independent
  `InventoryTransaction` rows with `SourceDocumentId == null` (see `TransferStockHandler`). Nothing
  today says "this out corresponds to that in," so a restated out-cost cannot be routed to its in.
- Chains are possible (A→B→C) and, pathologically, cycles (A→B→A). The engine must order buckets and
  refuse what it cannot prove correct rather than silently miscompute financial data
  (accounting integrity is non-negotiable, CLAUDE.md).

This ADR records the **design** so it can be implemented as a dedicated, well-tested follow-up. No
engine ships with this ADR.

## Decision

Introduce a **coordinated multi-bucket recompute** that generalizes the single-bucket replay, plus
the minimum schema needed to make transfer cost flow traceable.

### 1. Link the transfer legs (schema)

Add a nullable `TransferGroupId : Guid?` to `InventoryTransaction`. `TransferStockHandler` generates
one id per transfer and stamps it on **both** the `TransferOut` and `TransferIn` rows (EF migration in
the `inventory` schema). The pairing is then "same `TransferGroupId`, opposite direction." Production
consume/receive, when it lands, reuses the same correlation.

**Legacy data:** transfers recorded before the migration have `TransferGroupId == null` and cannot be
paired. A back-date that reaches an unlinked transfer keeps returning the existing
`BackDatingCrossesTransfer` error (safe degradation) — no fragile heuristic backfill. Only transfers
created after the migration participate in cross-bucket back-dating.

### 2. Coordinated replay (new orchestration over the existing pure engines)

Keep `MovingAverageReplay` and `FifoReplay` as the pure single-bucket engines; add a pure **multi-bucket**
engine (`CrossBucketMovingAverageReplay`) and an orchestration in the inventory ledger service. As
implemented (Phase 1, moving average):

1. **Global chronological replay.** Load **all** of the product's movements across every warehouse and
   replay them in one global `(MovementDate, insertion-order)` pass, maintaining per-warehouse
   running qty/average. A `TransferOut` records its issued **total** cost in a `TransferGroupId → cost`
   map; the paired `TransferIn` (always later in the pass) reads that map and receives the value
   (value-preserving, matching the forward transfer path's rounding). One pass therefore threads every
   transfer's cost from source bucket to destination bucket; because a leg can never depend on a later
   one, **cost cycles cannot occur** and no topological sort / cycle handling is required.
2. **One net journal.** Accumulate COGS deltas (later `Sales` issues) and variance deltas (later
   `Adjustment` issues) across **all** buckets — each row's restated `TotalCost` minus its stored one —
   and post a **single** net delta adjusting journal balanced against Inventory, via the posting-rule
   engine (ADR-0024), dated at the back-dated movement so the closed-period guard applies. Transfer legs
   themselves stay GL-neutral (value threads to the destination).
3. **Restate in place.** Every ledger row is restated (rebuildable projection, ADR-0014) and each
   affected bucket is set to its final running state; posted journals stay immutable (ADR-0009).
4. **Atomicity.** The whole multi-bucket replay + restatements + the net journal commit in **one**
   cross-module transaction (ADR-0021) — the ledger service already runs inside the caller's
   coordinator (e.g. `AdjustStockHandler`, goods receipt, delivery).
5. **Routing.** A back-dated moving-average movement uses the cross-bucket path only when the product
   has a transfer on or after the movement's date (`HasTransferOnOrAfterAsync`); otherwise the proven
   single-bucket path (ADR-0033) is unchanged. A legacy **unlinked** transfer (null `TransferGroupId`)
   or any production movement in the set is rejected (`BackDatingCrossesTransfer`) — the recompute
   cannot thread a cost it cannot pair.

### 3. FIFO across buckets (Phase 2 — deferred)

The FIFO case additionally rebuilds **layers across buckets**: a destination `TransferIn` opens a
layer at the transfer-out's FIFO-derived unit cost, which is itself the cost consumed from the
source's oldest layers during the source replay. Because this is the highest-risk part, it is
**deferred**: a FIFO back-date that crosses a transfer is still rejected with a clear error, and the
moving-average phase shipped first.

### Phasing

- **Phase 1 (shipped, CHG-0125):** moving-average — a back-dated movement that **cascades through** an
  existing forward transfer recomputes across buckets. Forward transfers stamp `TransferGroupId`.
- **Phase 2 (deferred):** (a) **directly back-dating a transfer document** (inserting both legs before
  existing movements in two buckets) — `TransferStockHandler` still rejects this; and (b) **FIFO**
  cross-bucket. Both remain rejected with a clear error until implemented.

## Options Considered

1. **Coordinated multi-bucket replay + `TransferGroupId` (chosen).** Reuses the two pure engines,
   keeps the ledger the source of truth and the GL reconciled, and closes the last back-dating gap.
   Cost: a schema column + migration, new orchestration (topological order, cycle rejection,
   cross-bucket propagation), and the FIFO cross-bucket layer rebuild.
2. **Reverse-and-repost every affected transfer + later issue.** Re-issues at corrected costs by
   posting reversals. Rejected for the same reason ADR-0033/0037 rejected it: it floods the ledger and
   GL with reversal noise for what is a derived-value correction, and multiplies across buckets.
3. **Keep cross-bucket rejected (status quo).** Zero risk, but leaves a real gap: a legitimate late
   entry (a back-dated purchase) for a product that was later transferred and sold cannot be corrected
   in place — the user must reverse and re-enter downstream documents by hand.
4. **Reuse `SourceDocumentId` instead of a new column.** Avoids a migration but overloads a field with
   distinct semantics and still leaves legacy rows unlinked; an explicit `TransferGroupId` documents
   intent and is worth the migration.

## Consequences

- **Positive:** closes the last inventory back-dating debt; in-place correction of late entries even
  when stock later moved warehouses; valuation and the GL stay reconciled; posted journals and
  closed-period locks preserved; single net journal keeps the correction auditable; the single-bucket
  paths and their tests are untouched.
- **Negative / trade-offs:** a schema migration; a back-date can now fan out into a replay of several
  buckets (cost grows with the transfer graph's in-period breadth); a genuinely new class of
  orchestration bug to guard against (ordering, propagation, cycles) in the accounting-integrity core —
  hence heavy testing and, optionally, a phased MA-then-FIFO rollout; cycles and legacy-unlinked
  transfers remain rejected by design.
- **Follow-ups:** Phase 2 — directly back-dating a transfer document, and FIFO cross-bucket layer
  reconstruction; production consume/receive inherits the same `TransferGroupId` correlation when
  Manufacturing lands. (`BR-INV-11` added for the cross-bucket cascade + legacy/unlinked rejection; the
  cycle-rejection error the draft anticipated proved unnecessary — cost cycles cannot occur under the
  global-chronological pass.)

## References

- [ADR-0033](0033-inventory-backdated-recompute.md) moving-average back-dated recompute
- [ADR-0037](0037-inventory-fifo-backdated-recompute.md) FIFO back-dated recompute (layer reconstruction)
- [ADR-0034](0034-inventory-fifo-costing.md) FIFO costing as a per-product option
- [ADR-0009](../DECISIONS.md) posted journals immutable · [ADR-0014](../DECISIONS.md) ledger is truth · [ADR-0021](../DECISIONS.md) cross-module atomicity + RowVersion · [ADR-0024](../DECISIONS.md) posting-rule engine
- [INVENTORY_DESIGN.md](../INVENTORY_DESIGN.md) §10 · [BUSINESS_RULES.md](../BUSINESS_RULES.md) BR-INV-5, BR-INV-6, BR-INV-9, BR-INV-10
