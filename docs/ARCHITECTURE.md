# ARCHITECTURE.md

Authoritative description of Accountrack's system architecture. Subordinate to `CLAUDE.md`;
decisions referenced here are recorded in [DECISIONS.md](DECISIONS.md).

## 1. Architectural Style

Accountrack is a **Modular Monolith** (ADR-0001): one deployable backend partitioned into
isolated modules, each following **Clean Architecture** (ADR-0002). Microservices are out of
scope for MVP, but every module is designed to be extractable later (ADR-0007, ADR-0023).

### Why
- Accounting and inventory need strong transactional consistency — cheap in-process, expensive
  across services.
- A small team ships and operates one deployable far more reliably than a fleet.
- Clean module boundaries preserve the option to extract services when scale demands it.

## 2. Solution Layout

```
Accountrack.sln
├── src/
│   ├── BuildingBlocks/
│   │   ├── Accountrack.SharedKernel/        # Money, Entity base, audit, domain-event base, Result
│   │   ├── Accountrack.Application.Abstractions/  # CQRS interfaces, behaviors, tenant context
│   │   └── Accountrack.Infrastructure.Common/     # EF base DbContext, interceptors, outbox
│   ├── Modules/
│   │   ├── Identity/
│   │   │   ├── Accountrack.Identity.Domain
│   │   │   ├── Accountrack.Identity.Application
│   │   │   ├── Accountrack.Identity.Infrastructure
│   │   │   └── Accountrack.Identity.Api           # endpoint module (carter/minimal or controllers)
│   │   ├── CompanyManagement/   (same 4 layers)
│   │   ├── MasterData/
│   │   ├── Sales/
│   │   ├── Purchasing/
│   │   ├── Inventory/
│   │   ├── Accounting/
│   │   ├── Approval/
│   │   ├── AuditLog/
│   │   ├── Notification/
│   │   └── Reporting/
│   ├── Modules.Contracts/        # public integration-event + service contracts per module
│   └── Bootstrapper/
│       └── Accountrack.Api/      # composition root: DI, pipeline, module registration, host
├── tests/
│   ├── <Module>.UnitTests
│   ├── <Module>.IntegrationTests
│   └── Accountrack.ArchitectureTests
└── frontend/                     # Vue 3 + TS SPA (see DEPLOYMENT.md)
```

> Module names map 1:1 to the modules in `CLAUDE.md`. Each module is independently testable.

## 3. Clean Architecture Layers (per module)

```
            ┌─────────────────────────────────────────┐
   inward → │                 Domain                  │  entities, value objects, domain
            │  (no dependencies on other layers)      │  events, business invariants
            ├─────────────────────────────────────────┤
            │              Application                │  use cases (CQRS handlers),
            │  (depends on Domain + Abstractions)     │  ports (interfaces), validators
            ├─────────────────────────────────────────┤
            │            Infrastructure               │  EF Core, repositories, outbox,
            │  (implements Application ports)         │  external integrations
            ├─────────────────────────────────────────┤
            │                  API                    │  endpoints, request/response DTOs,
            │  (thin; maps HTTP ↔ Application)        │  auth attributes
            └─────────────────────────────────────────┘
```

**Dependency rule:** dependencies point inward only. Domain references nothing outward.
Application defines **ports** (interfaces like `IInventoryLedger`, `IJournalPoster`,
`IClock`, `ICurrentUser`); Infrastructure implements them. Enforced by ADR-0023 tests.

## 4. CQRS & Application Pipeline

- Use cases are **commands** (state-changing) and **queries** (read) handled via a mediator
  (MediatR or equivalent).
- A consistent **pipeline of behaviors** wraps every handler:
  1. **Logging / correlation** — request id, tenant, user.
  2. **Validation** — FluentValidation; failures → 422 (see ERROR_HANDLING.md).
  3. **Authorization** — permission + company-scope check.
  4. **Tenant context** — ensures ambient `{TenantId, CompanyId}` is present.
  5. **Unit of work / transaction** — commands run in a DB transaction; outbox events flushed
     on commit.
  6. **Idempotency** — for posting handlers (ADR-0021).
- Queries bypass the write transaction and may use optimized read models / snapshots.

## 5. Module Boundaries & Communication

Hard rules (ADR-0007):

1. **No module touches another module's tables.** Each module owns its EF schema/table-prefix.
2. Cross-module collaboration happens via:
   - **Integration events** (in-process mediator dispatch, persisted through the outbox), or
   - **Public application contracts** in `Modules.Contracts` (interfaces with DTOs).
3. **Atomic vs eventual** (see INTEGRATION_EVENTS.md for the matrix):
   - *Atomic* (same DB transaction): cases where the books would be wrong if one side failed
     — e.g. posting a Sales Invoice's AR/Revenue journal, moving stock + COGS on shipment.
   - *Eventual* (outbox + handler): cross-cutting reactions — notifications, process-tracker
     updates, audit projections, report snapshot refresh.
4. Shared concepts (Money, ids, audit) live in the **SharedKernel**, not in any business module.

```
 Sales ──(SalesInvoicePosted)──► Accounting (post AR/Revenue/VAT journal)
   │                                  ▲
   └──(GoodsShipped)──► Inventory ──(StockIssued+Cost)──┘ (COGS)
 Purchasing ──(GoodsReceived)──► Inventory + Accounting (Inventory/GR-IR)
 (any) ──(DocumentStateChanged)──► ProcessTracker, Notification, AuditLog
```

## 6. Cross-Cutting Concerns

| Concern | Mechanism |
|---|---|
| Multi-tenancy | Ambient `ITenantContext` + EF global query filters (MULTI_TENANCY.md) |
| Soft delete | `IsDeleted` global filter; never physical delete (ADR-0006) |
| Audit log | EF `SaveChanges` interceptor capturing before/after (ADR-0006) |
| Auth | JWT + rotating refresh, RBAC, company scope (SECURITY.md, ADR-0019/0020) |
| Concurrency | `RowVersion` optimistic concurrency (ADR-0021) |
| Idempotency | Inbox/idempotency keys on posting handlers (ADR-0021) |
| Reliability | Transactional outbox dispatcher (ADR-0007) |
| Validation | FluentValidation behavior |
| Errors | Standard envelope + ProblemDetails (ERROR_HANDLING.md) |
| Time | `IClock` abstraction (no `DateTime.Now` in domain) |
| Config/secrets | `appsettings.{Env}.json` + env/secret store; never hardcoded |
| Observability | Structured logging now; OpenTelemetry later |
| Background work | Hosted services now; Hangfire/RabbitMQ later (tenant context propagated explicitly) |

## 7. Persistence

- Single SQL Server database, shared schema (ADR-0004). Each module maps to its own EF schema
  (e.g. `sales`, `inventory`, `accounting`) for boundary clarity and future extraction.
- One `DbContext` per module; a base context applies tenant + soft-delete filters, audit
  fields, and `RowVersion`.
- Migrations are per-module and applied in dependency order at startup (dev) / via pipeline
  (prod). See DATABASE.md.

## 8. API Surface

- ASP.NET Core Web API under `/api/v1` (versioned). Standard response envelope and error model
  (API_SPEC.md, ERROR_HANDLING.md).
- Endpoints are thin: validate → dispatch command/query → map result. No business logic in
  controllers/endpoints.

## 9. Frontend

- Vue 3 + TypeScript SPA, Pinia state, Vue Router, Tailwind. Talks to the API only.
- i18n with English (default) + Bahasa Indonesia; no hardcoded UI strings.
- Auth via access token in memory + refresh token (httpOnly cookie or secure storage; see
  SECURITY.md).

## 10. Extraction Path to Microservices (future)

Because (a) modules don't share tables, (b) communication is event/contract-based, and (c)
each module owns its schema, extracting a module means: split its schema to its own DB, replace
the in-process event dispatch with the message bus (RabbitMQ), and host its API separately. No
business-logic rewrite. The architecture tests guard the invariants that make this possible.

## 11. Quality Gates (CI)

1. Build + unit tests.
2. Integration tests (DB via container) — esp. accounting, inventory, tenancy isolation.
3. **Architecture-fitness tests** (NetArchTest, ADR-0023).
4. Lint/format (`dotnet format`, ESLint/Prettier).
5. Security review of any `IgnoreQueryFilters` / raw SQL usage.

See TESTING.md and DEPLOYMENT.md for details.
