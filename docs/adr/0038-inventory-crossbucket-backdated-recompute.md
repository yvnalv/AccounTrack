# ADR-0038: Cross-bucket (transfer) back-dated recompute — coordinated multi-bucket replay

- **Status:** Proposed (design only — not yet implemented)
- **Date:** 2026-07-09
- **Deciders:** Product owner, engineering
- **Tags:** inventory | accounting

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

Keep `MovingAverageReplay` and `FifoReplay` as the pure per-bucket engines; add an orchestration in the
inventory ledger service:

1. **Discover the affected set.** Starting from the edited bucket, replay it; whenever a later
   `TransferOut` whose cost changed is found, follow its `TransferGroupId` to the destination bucket and
   enqueue it. Repeat transitively.
2. **Order by dependency.** Topologically sort affected buckets so a source is always replayed before
   its destination (a transfer edge = "source → destination"). **Reject cycles** with a new specific
   error (`BackDatingCrossesTransferCycle`) — a cost cycle has no stable fixed point in one pass.
3. **Propagate.** When replaying a destination bucket, the paired `TransferIn`'s inbound unit cost is
   taken from the **already-replayed** source `TransferOut`'s restated cost (not the stale recorded
   value). This is the only new coupling; each bucket still runs through the unchanged pure engine.
4. **One net journal.** Accumulate COGS deltas (later `Sales` issues) and variance deltas (later
   `Adjustment` issues) across **all** replayed buckets, and post a **single** net delta adjusting
   journal balanced against Inventory, via the posting-rule engine (ADR-0024), dated at the back-dated
   movement so the closed-period guard applies. Transfer legs themselves stay GL-neutral.
5. **Atomicity.** The whole multi-bucket replay + all layer/ledger restatements + the net journal
   commit in **one** cross-module transaction (ADR-0021), serialized on each affected bucket's
   RowVersion. `TransferStockHandler` moves onto `ICrossModuleUnitOfWork` so a directly back-dated
   transfer runs through the same path.

### 3. FIFO across buckets

The FIFO case additionally rebuilds **layers across buckets**: a destination `TransferIn` opens a
layer at the transfer-out's FIFO-derived unit cost, which is itself the cost consumed from the
source's oldest layers during the source replay. Layer reconstruction (ADR-0037) runs per bucket in
dependency order. Because this is the highest-risk part, implementation **may** land moving-average
first (FIFO cross-bucket still rejected with a clear error) and FIFO second — see Options.

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
- **Follow-ups:** implement in a dedicated PR after this design is accepted; new error(s)
  `BackDatingCrossesTransferCycle` (and, if phased, a FIFO-cross-bucket-not-yet error); flip
  `BR-INV-5` / `BR-INV-10`'s "cross-bucket rejected" clause and add a `BR-INV-11` for the cross-bucket
  cascade + cycle/legacy rejection; update INVENTORY_DESIGN.md §10; production consume/receive inherits
  the same `TransferGroupId` correlation when Manufacturing lands.

## References

- [ADR-0033](0033-inventory-backdated-recompute.md) moving-average back-dated recompute
- [ADR-0037](0037-inventory-fifo-backdated-recompute.md) FIFO back-dated recompute (layer reconstruction)
- [ADR-0034](0034-inventory-fifo-costing.md) FIFO costing as a per-product option
- [ADR-0009](../DECISIONS.md) posted journals immutable · [ADR-0014](../DECISIONS.md) ledger is truth · [ADR-0021](../DECISIONS.md) cross-module atomicity + RowVersion · [ADR-0024](../DECISIONS.md) posting-rule engine
- [INVENTORY_DESIGN.md](../INVENTORY_DESIGN.md) §10 · [BUSINESS_RULES.md](../BUSINESS_RULES.md) BR-INV-5, BR-INV-6, BR-INV-9, BR-INV-10
