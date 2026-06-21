# MULTI_TENANCY.md

How Accountrack isolates data between tenants and scopes access between companies.
Tenancy isolation is the single highest-severity risk in the system; the controls here are
**mandatory**, not advisory. Decision basis: ADR-0004.

## 1. Model

- **Tenant** — the subscription/organization. The **hard isolation boundary**. A user belongs
  to exactly one tenant.
- **Company** — a legal entity within a tenant. Books (GL, inventory) are kept per company.
  A user is **granted access to a subset of companies** within their tenant.

```
Tenant (Yovan Account)
├── Company: Shirt Lab        ← user A: full access
├── Company: Future Co A      ← user A: no access, user B: access
└── Company: Future Co B
```

Every business row carries both `TenantId` and `CompanyId`.

### Tenant provisioning

- **Seeding** creates the well-known development tenant/company at startup.
- **Public sign-up** (CHG-0078, `POST /api/v1/auth/register`) provisions a **new tenant + first
  company** for a self-onboarding business: Company Management creates the tenant/company
  (`ICompanyProvisioning`), then Identity seeds the 6 standard roles and the registrant as that
  tenant's Administrator. This is the only anonymous write path that creates a tenant; every other
  write is tenant-scoped by the ambient context.

## 2. Strategy

Single database, single shared schema (ADR-0004). Isolation is enforced by **four
independent layers** — defense in depth, so no single mistake leaks data:

1. **Ambient tenant context** resolved from the authenticated principal.
2. **EF Core global query filters** on every tenant-owned entity.
3. **Authorization guards** (company-scope checks in the pipeline).
4. **Repository / write-path validation** (assigning and re-checking `TenantId`/`CompanyId`).

## 3. Tenant Context

```csharp
public interface ITenantContext
{
    Guid TenantId { get; }
    Guid CompanyId { get; }                 // the active company for this request
    IReadOnlyCollection<Guid> GrantedCompanyIds { get; }
    bool IsSet { get; }
}
```

Rules:
- `TenantId` and `GrantedCompanyIds` come **only** from validated JWT claims — **never** from a
  request body, query string, route, or header that the client controls.
- The **active `CompanyId`** is selected by the client (header `X-Company-Id` or route) but is
  **validated against `GrantedCompanyIds`** in the authorization behavior. An un-granted company
  → 403.
- Context is established once per request (middleware) and flows via DI scope.
- **Background jobs** have no request; they must construct the tenant context explicitly from
  the job payload (each enqueued job carries its tenant/company). A job with no tenant context
  must not touch tenant-owned data.

## 4. EF Global Query Filters

Every tenant-owned entity implements `ITenantOwned { Guid TenantId; Guid CompanyId; }` and the
base `DbContext` applies:

```csharp
modelBuilder.Entity<T>().HasQueryFilter(e =>
    e.TenantId == _tenant.TenantId
    && e.CompanyId == _tenant.CompanyId
    && !e.IsDeleted);
```

- Cross-company reporting within a tenant uses a dedicated, explicitly-scoped query path that
  filters on `TenantId` + `CompanyId IN GrantedCompanyIds` — **not** by disabling the filter.
- The filter predicate reads from the ambient context captured at context construction.

## 5. The `IgnoreQueryFilters` Ban

`IgnoreQueryFilters()` removes tenant isolation. It is **banned** except in:
- explicitly reviewed **platform-admin** maintenance paths, and
- the migration/seed bootstrap before any tenant exists.

Enforcement:
- An **architecture/lint test** fails the build on any `IgnoreQueryFilters` usage outside an
  allow-listed namespace.
- Any allow-listed usage requires a code comment with an ADR/issue reference and a reviewer.

## 6. Write-Path Validation

On insert: `TenantId`/`CompanyId` are stamped from the ambient context by a SaveChanges
interceptor — application code does not set them manually, and may not override them.
On update/delete: the entity's `TenantId`/`CompanyId` is re-verified against context; a
mismatch throws (defense against detached-entity tampering).

## 7. Indexing & Uniqueness

- Every index on a tenant-owned table **leads with `TenantId` (then `CompanyId`)**.
- "Unique" business keys are unique **per company**, not globally — e.g. invoice number is
  `UNIQUE (CompanyId, DocumentNumber)`. Never globally unique.
- This also limits the "noisy neighbor" effect of shared schema by keeping each tenant's rows
  co-located in the index.

## 8. JWT Claims

| Claim | Meaning |
|---|---|
| `sub` | user id |
| `tenant_id` | the user's tenant (hard boundary) |
| `companies` | granted company ids |
| `roles` / `perms` | RBAC (may be fetched server-side instead, to keep tokens small) |

Claims are validated server-side every request; the token is signed and short-lived (ADR-0020).

## 9. Mandatory Tests (a permanent suite)

These run in CI and must never regress:

1. A query executed under tenant A returns **zero** rows owned by tenant B.
2. Requesting an un-granted `CompanyId` returns **403**.
3. Inserting with a forged `TenantId`/`CompanyId` is overridden/rejected by the interceptor.
4. A background job without tenant context cannot read tenant data.
5. Every tenant-owned entity has a global query filter (reflection test).
6. No `IgnoreQueryFilters` outside the allow-list (architecture test).

See SECURITY.md for the broader security posture and TESTING.md for the isolation test suite.
