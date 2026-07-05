# API_SPEC.md

API conventions for Accountrack. Error model detailed in [ERROR_HANDLING.md](ERROR_HANDLING.md).

## 1. General

- **Base route:** `/api/v1`. Version in the path; breaking changes bump the version.
- **Transport:** HTTPS only. JSON request/response (`application/json`, UTF-8).
- **Style:** resource-oriented REST; resource paths are kebab-case plural
  (`/api/v1/sales-orders`, `/api/v1/purchase-invoices`, `/api/v1/inventory-transactions`).
- **Auth:** `Authorization: Bearer <access-token>` on every protected endpoint.
- **Company scope:** `X-Company-Id: <guid>` header selects the active company; validated against
  the user's granted companies (MULTI_TENANCY.md). Tenant is never passed by the client.
- **Correlation:** clients may send `X-Correlation-Id`; the server generates one if absent and
  echoes it.
- **Idempotency:** unsafe POSTs that create financial effects accept an `Idempotency-Key` header.

## 2. Response Envelope

The `CLAUDE.md` standard envelope, extended for lists and errors.

**Success (single):**
```json
{ "success": true, "data": { } }
```

**Success (collection, paginated):**
```json
{
  "success": true,
  "data": [ ],
  "meta": { "page": 1, "pageSize": 20, "totalItems": 137, "totalPages": 7 }
}
```

**Failure:**
```json
{
  "success": false,
  "message": "Validation failed",
  "error": {
    "code": "VALIDATION_ERROR",
    "details": [ { "field": "lines", "message": "At least one line is required." } ],
    "traceId": "00-abc...-01"
  }
}
```

See ERROR_HANDLING.md for codes and HTTP status mapping.

## 3. HTTP Methods & Status

| Action | Method | Success status |
|---|---|---|
| List | GET (collection) | 200 |
| Read | GET (item) | 200 |
| Create | POST | 201 (+ `Location`) |
| Update | PUT/PATCH | 200 |
| Soft delete | DELETE | 200/204 |
| Domain action (post/approve/pay) | POST `/{id}/{action}` | 200 |

Document actions use sub-resources, e.g. `POST /api/v1/sales-invoices/{id}/post`,
`POST /api/v1/purchase-orders/{id}/approve`, `POST /api/v1/fiscal-periods/{id}/close`.

## 4. Querying Collections

- **Pagination:** `?page=1&pageSize=20` (default 20, max 100).
- **Sorting:** `?sort=-createdAt,name` (prefix `-` = descending).
- **Filtering:** explicit query params per resource, e.g.
  `?status=Posted&customerId=...&dateFrom=2026-06-01&dateTo=2026-06-30`.
- **Search:** `?q=...` where supported.
- All list endpoints are tenant + company scoped automatically.

## 5. Conventions

- **Dates/times:** ISO-8601 UTC (`2026-06-13T10:00:00Z`). Business dates without time use
  `YYYY-MM-DD`.
- **Money:** object `{ "amount": 1500000.00, "currency": "IDR" }`.
- **Ids:** GUID strings.
- **Enums:** PascalCase strings in JSON (`"Posted"`), mapped to the stored value server-side.
- **Naming in JSON:** camelCase fields.
- **Null vs absent:** omit unknown optional fields; explicit `null` means "cleared".

## 6. Resource Catalog (MVP, illustrative)

```
Identity        /auth/login  /auth/refresh  /auth/logout  /users  /roles  /permissions
Company         /tenants  /companies  /companies/{id}/settings
Master Data     /products  /product-categories  /units-of-measure  /customers  /suppliers
                /warehouses  /accounts  /tax-codes
Sales           /quotations  /sales-orders  /delivery-orders  /sales-invoices
                /customer-payments  /sales-returns
Purchasing      /purchase-requests  /purchase-orders  /goods-receipts  /purchase-invoices
                /supplier-payments  /purchase-returns
Expenses        /expense-categories  /expense-vouchers  (+ /{id}/submit|cancel|reverse; /draft)
Inventory       /inventory-transactions  /stock-adjustments  /stock-transfers  /stock-opnames
                /stock-on-hand
Accounting      /journal-entries  /fiscal-years  /fiscal-periods  /posting-rules
                /reports/trial-balance  /reports/profit-loss  /reports/balance-sheet
                /reports/cash-flow  /reports/general-ledger  /reports/ar-aging  /reports/ap-aging  /reports/vat
Approval        /approval-definitions  /approval-requests  /approval-requests/{id}/approve
Process         /documents/{type}/{id}/timeline
Audit           /audit-entries
Notification    /notifications
```

## 7. Security & Versioning Rules
- Every endpoint declares its required permission(s) (SECURITY.md).
- Authorization (permission + company scope) runs in the pipeline before the handler.
- Backward-incompatible changes require a new `/api/vN`; additive changes don't.
- OpenAPI/Swagger is generated and published per version.
