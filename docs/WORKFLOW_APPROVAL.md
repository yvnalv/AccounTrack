# WORKFLOW_APPROVAL.md

The approval engine and document lifecycle / process tracking. The engine is **generic and
document-agnostic** (ARCHITECTURE.md §5): business modules expose state and raise events; the
Approval module drives transitions. This keeps Sales/Purchasing free of hardcoded approval logic.

## 1. Document Lifecycle (status workflow)

Standard states (a document uses the subset it needs):

```
Draft → Submitted → [Approval] → Approved → Posted → Completed
                         │
                         └→ Rejected → (back to Draft)
Any non-final state → Cancelled
```

- **Draft** — editable, no ledger/inventory effect.
- **Submitted** — locked for editing, awaiting approval (if required).
- **Approved** — passed approval; ready to post.
- **Posted** — financial/inventory effects committed (journals, stock). From here, changes are
  **reversal-only** (ADR-0009).
- **Completed** — fully settled (e.g. invoice fully paid, order fully delivered+invoiced).
- **Rejected / Cancelled** — terminal/return states; Cancelled requires that no irreversible
  posting has occurred (otherwise reverse first).

State transitions are validated by the owning module; illegal transitions are rejected.

## 2. Approval Engine

### Configuration
```
ApprovalDefinition
├── DocumentType   (PurchaseOrder, Expense, StockAdjustment, SalesOrder, …)
├── IsActive
└── Conditions[]   (when this definition applies — see §3)

ApprovalStep (ordered, belongs to a definition)
├── Level          (1..n)
├── ApproverType   (Role | User | Manager)
├── ApproverRef    (RoleId / UserId)
├── Quorum         (e.g. any-1-of-role, all)
└── Optional escalation (timeout → escalate to …)
```

### Runtime
```
ApprovalRequest (DocumentType, DocumentId, CurrentLevel, Status {Pending,Approved,Rejected})
ApprovalAction  (StepId, ApproverId, Decision {Approve,Reject}, Comment, ActedAt)
```

Flow:
1. On **Submit**, the owning module raises `ApprovalRequested`. The engine evaluates conditions
   to select the applicable definition.
2. If no definition matches → auto-approved (document goes straight to Approved).
3. Otherwise the engine creates a request at Level 1 and notifies approvers.
4. Each `Approve` advances to the next level (respecting quorum); the final level → emits
   `ApprovalDecided(Approved)` and the document transitions to Approved.
5. Any `Reject` → `ApprovalDecided(Rejected)`; document returns to Draft/Rejected with the
   comment.

## 3. Approval Types (all supported)

- **Single-level** — one step, one role/user.
- **Multi-level** — ordered steps (e.g. Supervisor → Finance Manager → Director).
- **Conditional** — a definition applies only when its conditions hold. Conditions evaluate
  document attributes, e.g.:
  - `PurchaseOrder.Total > 50,000,000 IDR` → require Director level.
  - `StockAdjustment.AbsValue > threshold` → require Finance approval.
  - `SalesOrder.Customer.OverCreditLimit = true` → require approval.

Thresholds and routes are **configuration per company**, not code.

## 4. Segregation of Duties (with SECURITY.md)
- The **submitter cannot be the sole approver** of the same document. If the only eligible
  approver is the submitter, the request escalates rather than auto-passing.
- Approve, Post, and Pay are distinct permissions; SoD violations are reportable.

## 5. Escalation (future-friendly, designed now)
- A step may define a timeout; on expiry the request escalates to a fallback approver or the next
  level, with notification. MVP may ship without timers but the model reserves the fields.

## 6. Process Tracker

Separate concern from approval and audit (see GLOSSARY.md distinction). Every document exposes a
**timeline of business milestones**:

```
ProcessTimeline (DocumentType, DocumentId)
ProcessEvent    (Milestone, At, By, Note)   e.g. Created, Submitted, Approved (L1/L2),
                                                 Posted, Partially Paid, Completed, Cancelled
```

- Milestones are appended by subscribing to `DocumentStateChanged` and `ApprovalDecided`
  (eventual/outbox — INTEGRATION_EVENTS.md).
- Read-only, user-facing; powers the "where is this document" view across the whole journey.

## 7. Which documents use approval (MVP defaults; configurable)
| Document | Default approval |
|---|---|
| Purchase Order | conditional by amount |
| Purchase Invoice | optional |
| Expense | conditional by amount |
| Stock Adjustment / Opname | conditional by variance value |
| Sales Order | optional / credit-limit conditional |
| Journal (manual) | required (post permission + optional approval) |
| Period Reopen | required (permissioned + audited) |

## 8. Test Coverage
Single/multi/conditional routing; quorum; reject returns document correctly; SoD self-approval
blocked; no-definition auto-approve; process timeline reflects every transition; approval state
and document state stay consistent.
