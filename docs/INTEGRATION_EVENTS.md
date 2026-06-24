# INTEGRATION_EVENTS.md

The contract catalog for inter-module communication. Modules never read each other's tables
(ADR-0007); they collaborate through the events and contracts defined here.

## 1. Mechanics

- **In-process mediator** dispatches events within the monolith.
- **Transactional outbox**: an integration event is written to `OutboxMessages` in the *same
  DB transaction* as the source state change. A background dispatcher publishes it to handlers
  and marks it sent. Guarantees at-least-once delivery even across crashes.
- **Idempotency (inbox)**: each handler records `(EventId, Handler)` in `InboxConsumed`; a
  redelivered event is ignored. This makes posting safe under retries (ADR-0021).
- **Envelope** (every event):
  ```
  { EventId, OccurredAtUtc, TenantId, CompanyId, CorrelationId, CausationId, Payload }
  ```
  `TenantId`/`CompanyId` travel on the envelope so async handlers can reconstruct tenant context
  (MULTI_TENANCY.md §3).

**Implementation (CHG-0083).** The durable outbox is live for the **Approval** module (first
producer). `ApprovalSubmitted`/`ApprovalDecided` are enqueued via `IOutbox` into
`approval.OutboxMessages` in the *same* `SaveChanges` as the request/decision. A hosted
`OutboxDispatcherService` polls pending rows (batch 50, 2s interval, 10-attempt cap) and delivers
each in its own DI scope. The de-dup "inbox" is `platform.InboxState`, keyed by `(Handler, EventId)`
and checked before each handler runs, so at-least-once redelivery is effectively **exactly-once per
handler** (no consumer change needed for the append-only ProcessTracker/Notification consumers). A
settable `IAmbientTenant` restores the originating tenant/company per message (the request-time
`ITenantContext` falls back to it when there is no HTTP request). Both platform/module stores use
their own connection — never the cross-module transaction. Other producers still use the in-process
publisher until migrated onto the outbox.

## 2. Consistency Policy — Atomic vs Eventual

| Effect | Mode | Why |
|---|---|---|
| Sales Invoice → AR/Revenue/VAT journal | **Atomic** | Books must never show an invoice without its journal |
| Goods Shipment → stock issue + COGS journal | **Atomic** | COGS must use the cost at the moment of issue |
| Goods Receipt → stock receipt + Inventory/GR-IR journal | **Atomic** | Valuation and accrual must agree |
| Purchase Invoice → AP/VAT journal + GR-IR clearing | **Atomic** | AP and clearing must reconcile |
| Payment → cash + AR/AP allocation journal | **Atomic** | Allocation and ledger must agree |
| Any document state change → Process Tracker update | **Eventual** | Non-financial projection |
| Any change → Audit Log projection | **Eventual** | Forensic record, not a business invariant |
| Any event → Notification | **Eventual** | Side effect, retriable |
| Period close → snapshot refresh / report cache | **Eventual** | Recomputable |

**Atomic** effects are executed by a coordinating Application service inside one transaction
(the consuming module exposes a synchronous contract). **Eventual** effects go through the
outbox to subscribing handlers.

**Implementation (CHG-0019).** Atomic flows use `ICrossModuleUnitOfWork` (`Modules.Contracts`):
participating modules (currently Purchasing, Inventory, Accounting) bind their DbContext to one
request-scoped `ISharedDbConnection`, register as `ITransactionalDbContext`, and expose **save-less**
synchronous contracts (`IInventoryPosting`, `IGeneralLedgerPoster`, `IPostingAccountResolver`). The
coordinator opens a single local transaction (no MSDTC), runs the work, persists every enlisted
context, and commits — or rolls everything back on failure. First consumer: Goods Receipt (stock
ledger + Dr Inventory/Cr GR-IR journal). Note: idempotency keys (inbox) for these flows are not yet
implemented — retries are not yet safe against double-posting.

> Rule of thumb: if a failure would make the **GL or inventory ledger wrong or inconsistent**,
> it is atomic. If a failure only delays a notification/projection that can be replayed, it is
> eventual.

## 3. Public Service Contracts (synchronous, for atomic flows)

Defined in `Modules.Contracts`. Examples (signatures illustrative):

```csharp
// Accounting
public interface IJournalPoster
{
    // Idempotent by (source, sourceDocumentId, eventType).
    Task<JournalEntryId> PostAsync(PostJournalRequest request, CancellationToken ct);
    Task<JournalEntryId> ReverseAsync(JournalEntryId entryId, string reason, CancellationToken ct);
}

public interface IFiscalPeriodQuery
{
    Task<bool> IsOpenAsync(Guid companyId, DateOnly date, CancellationToken ct);
}

// Inventory
public interface IInventoryLedger
{
    // Returns the cost applied (current moving average) so the caller can post COGS.
    Task<StockMovementResult> IssueAsync(IssueStockRequest req, CancellationToken ct);
    Task<StockMovementResult> ReceiveAsync(ReceiveStockRequest req, CancellationToken ct);
    Task<decimal> GetOnHandAsync(Guid productId, Guid warehouseId, CancellationToken ct);
}

// Master data
public interface IPostingRuleResolver
{
    Task<Guid> ResolveAccountAsync(Guid companyId, string eventType, PostingSelector selector,
                                   CancellationToken ct);
}
```

`PostJournalRequest` carries pre-computed balanced lines OR an event type + amounts that the
Accounting module expands using posting rules (POSTING_RULES.md). The poster validates balance
and period-open before committing.

## 4. Event Catalog

Naming: past tense, `<Aggregate><Fact>`. All carry the envelope above.

### Sales
| Event | Emitted when | Primary consumers (mode) |
|---|---|---|
| `SalesOrderConfirmed` | SO approved | ProcessTracker (E) |
| `GoodsShipped` | Delivery Order posted | Inventory issue + COGS (A), ProcessTracker (E) |
| `SalesInvoicePosted` | Sales Invoice posted | Accounting AR/Revenue/VAT (A), AR subledger (A) |
| `CustomerPaymentReceived` | Payment posted | Accounting cash + AR allocation (A) |
| `SalesReturnPosted` | Sales Return posted | Inventory receipt (A), Accounting reversal-style (A) |

### Purchasing
| Event | Emitted when | Primary consumers (mode) |
|---|---|---|
| `PurchaseOrderApproved` | PO approved | ProcessTracker (E) |
| `GoodsReceived` | Goods Receipt posted | Inventory receipt (A), Accounting Inventory/GR-IR (A) |
| `PurchaseInvoicePosted` | Purchase Invoice posted | Accounting AP/VAT + clear GR-IR (A), AP subledger (A) |
| `SupplierPaymentPosted` | Payment posted | Accounting cash + AP allocation (A) |
| `PurchaseReturnPosted` | Purchase Return posted | Inventory issue (A), Accounting (A) |

### Inventory
| Event | Emitted when | Primary consumers (mode) |
|---|---|---|
| `StockAdjusted` | Adjustment posted | Accounting variance journal (A), ProcessTracker (E) |
| `StockTransferred` | Transfer posted | Accounting (only if cross-valuation) (A/E) |
| `StockMovementRecorded` | any ledger write | Reporting projection (E) |

### Accounting
| Event | Emitted when | Primary consumers (mode) |
|---|---|---|
| `JournalPosted` | Journal committed | Reporting snapshot refresh (E), ProcessTracker (E) |
| `JournalReversed` | Reversal posted | Reporting (E), AuditLog (E) |
| `FiscalPeriodClosed` | Period close | Reporting snapshot build (E), Notification (E) |

### Cross-cutting (any module)
| Event | Emitted when | Consumers (mode) |
|---|---|---|
| `DocumentStateChanged` | any status transition | ProcessTracker (E), Notification (E) |
| `EntityChanged` (interceptor) | any SaveChanges | AuditLog (E) |
| `ApprovalRequested` / `ApprovalDecided` | workflow | Notification (E), source module to advance state (A/E) |

(A) = atomic / synchronous contract; (E) = eventual / outbox.

## 5. Failure Handling

- Outbox dispatch retries with backoff; after N failures an event is parked in a dead-letter
  state and alerts.
- Atomic flows: if the consuming contract throws, the whole source transaction rolls back — the
  document is not posted. Surfaced to the user as a posting error (ERROR_HANDLING.md).
- Idempotency keys guarantee replays never double-post.

## 6. Adding a New Event
1. Define the event + payload in `Modules.Contracts` (immutable, versioned if shape changes).
2. Decide and document its consistency mode in the table above.
3. Implement producer (write to outbox in source txn, or call contract for atomic).
4. Implement idempotent consumer(s).
5. Add tests: producer emits, consumer is idempotent, atomic flow rolls back on failure.
