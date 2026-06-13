# ERROR_HANDLING.md

How Accountrack reports and handles errors, end to end. Complements API_SPEC.md.

## 1. Failure Envelope

Every non-success response uses:
```json
{
  "success": false,
  "message": "<human-readable summary>",
  "error": {
    "code": "<STABLE_ERROR_CODE>",
    "details": [ { "field": "<name>", "message": "<why>" } ],
    "traceId": "<correlation/trace id>"
  }
}
```
- `message` is safe to show to users (no stack traces, no internal detail).
- `code` is a stable machine-readable string the frontend can switch on / translate.
- `details` is present for validation and multi-error cases.
- `traceId` ties the response to server logs.

## 2. Error Categories → HTTP Status → Code

| Category | HTTP | `code` | Notes |
|---|---|---|---|
| Validation failed | 422 | `VALIDATION_ERROR` | field-level `details` |
| Malformed request | 400 | `BAD_REQUEST` | unparseable / missing required |
| Unauthenticated | 401 | `UNAUTHENTICATED` | missing/expired/invalid token |
| Forbidden (permission/company scope) | 403 | `FORBIDDEN` | authenticated but not allowed |
| Not found / not in scope | 404 | `NOT_FOUND` | also for cross-tenant ids (never reveal existence) |
| Concurrency conflict | 409 | `CONCURRENCY_CONFLICT` | RowVersion mismatch |
| Business rule violation | 409 | `BUSINESS_RULE_VIOLATED` | includes `ruleId` (e.g. `BR-ACC-4`) |
| Duplicate / idempotency replay | 409 | `DUPLICATE` | safe replays return the prior result |
| Rate limited | 429 | `RATE_LIMITED` | `Retry-After` header |
| Unexpected server error | 500 | `INTERNAL_ERROR` | generic message; details only in logs |
| Dependency unavailable | 503 | `SERVICE_UNAVAILABLE` | DB/queue down |

## 3. Domain Error Modeling

- Application use cases return a **Result** type (`Result<T>` with success/failure + error code)
  rather than throwing for *expected* business failures (validation, rule violations). This keeps
  control flow explicit and testable.
- **Exceptions** are reserved for *unexpected* faults (bugs, infrastructure). A global exception
  middleware maps them to `INTERNAL_ERROR` (500) and logs with the traceId.
- Business-rule failures carry the `BR-` id from BUSINESS_RULES.md so the message can be precise
  and localized.

## 4. Validation

- FluentValidation in the Application pipeline (ARCHITECTURE.md §4). All failures aggregate into
  one `VALIDATION_ERROR` (422) with per-field `details` — never fail on the first field only.
- Domain invariants (e.g. unbalanced journal, negative stock) are enforced in the Domain and
  surface as `BUSINESS_RULE_VIOLATED` (409) if they slip past input validation.

## 5. Accounting/Inventory-Specific Errors (examples)

| Situation | code | ruleId |
|---|---|---|
| Posting into closed period | `BUSINESS_RULE_VIOLATED` | BR-ACC-4 |
| Unbalanced journal | `BUSINESS_RULE_VIOLATED` | BR-ACC-1 |
| Issue exceeds on-hand (negative disallowed) | `BUSINESS_RULE_VIOLATED` | BR-INV-3 |
| Posting rule unresolved | `BUSINESS_RULE_VIOLATED` | (config error, see POSTING_RULES.md) |
| Invoice qty exceeds order | `BUSINESS_RULE_VIOLATED` | BR-SAL-2 |
| Submitter self-approval | `FORBIDDEN` | BR-APR-2 |
| Replay of a posting (idempotent) | `DUPLICATE` (or 200 with prior result) | — |

## 6. Atomic Flow Failures

If an atomic cross-module effect fails (e.g. journal posting throws while posting an invoice),
the **entire source transaction rolls back**; the document stays in its pre-post state and the
caller receives the underlying error code. No partial financial state is ever persisted
(INTEGRATION_EVENTS.md §5).

## 7. Eventual Flow Failures

Outbox handler failures retry with backoff; persistent failures dead-letter and alert. Because
these are non-financial projections (notifications, process tracker, audit projections,
snapshots), the user-facing operation already succeeded; recovery is by replay.

## 8. Logging

- Structured logs include `traceId`, `tenantId`, `userId`, `companyId`, route, and outcome.
- Never log secrets, tokens, passwords, or full PII payloads.
- 4xx are logged at information/warning; 5xx at error with exception detail.

## 9. Localization
- `code` + `ruleId` allow the frontend to render localized (English / Bahasa Indonesia) messages.
  The server `message` is an English fallback.
