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

Legend: ✅ implemented · ◻️ planned.

```
Accountrack.sln
├── Directory.Build.props                # ✅ shared TFM (net8.0), nullable, warnings-as-errors
├── src/
│   ├── BuildingBlocks/
│   │   ├── Accountrack.SharedKernel/             # ✅ Entity / TenantScopedEntity / TenantOwnedEntity,
│   │   │                                         #    Money, Result/Error, PagedResult, domain-event base,
│   │   │                                         #    AuditEntry, audit/soft-delete/tenancy markers
│   │   ├── Accountrack.Application.Abstractions/ # ✅ CQRS contracts, behaviors, ambient-context ports
│   │   ├── Accountrack.Infrastructure.Common/    # ✅ EF base DbContext, audit/auditing interceptors, outbox msg
│   │   └── Accountrack.Web.Common/               # ✅ response envelope, Result→HTTP mapping, error middleware
│   ├── Modules/
│   │   ├── Identity/            # ✅ Domain / Application / Infrastructure / Api
│   │   ├── CompanyManagement/   # ✅ Domain / Application / Infrastructure / Api
│   │   ├── AuditLog/            # ✅ Application / Infrastructure / Api (no Domain — read-side over AuditEntry)
│   │   ├── Approval/            # ◻️ planned
│   │   ├── Notification/        # ◻️ planned
│   │   ├── MasterData/          # ◻️ planned (Phase 2)
│   │   ├── Sales/               # ◻️ planned (Phase 2)
│   │   ├── Purchasing/          # ◻️ planned (Phase 2)
│   │   ├── Inventory/           # ◻️ planned (Phase 2)
│   │   ├── Accounting/          # ◻️ planned (Phase 2)
│   │   └── Reporting/           # ◻️ planned (Phase 2)
│   ├── Modules.Contracts/        # ◻️ planned — created when the first cross-module contract is needed
│   └── Bootstrapper/
│       └── Accountrack.Api/      # ✅ composition root: DI, pipeline, auth, module registration, host
├── tests/
│   ├── Accountrack.ArchitectureTests        # ✅ NetArchTest boundary rules (per module)
│   ├── Accountrack.Identity.UnitTests        # ✅
│   ├── Accountrack.CompanyManagement.UnitTests  # ✅
│   ├── Accountrack.AuditLog.UnitTests        # ✅
│   └── <Module>.IntegrationTests            # ◻️ planned (Testcontainers)
└── frontend/                     # ◻️ planned — Vue 3 + TS SPA (discuss design before building)
```

> Module names map 1:1 to the modules in `CLAUDE.md`. Each module is independently testable.
> Module folders use the assembly prefix `Accountrack.<Module>.*`; Company Management uses
> `Accountrack.CompanyManagement.*` to avoid a `Company` type/namespace collision.
> The AuditLog module omits a Domain project because its aggregate (`AuditEntry`) is a shared
> kernel primitive written by all modules (ADR-0026); the module is read-side only.

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
| Audit log | EF `SaveChanges` interceptor capturing before/after into one shared `audit.AuditEntries` table, atomic with the change (ADR-0006/0026) |
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

- Single PostgreSQL database, shared schema (ADR-0004; provider per ADR-0032). Each module maps to its own EF schema
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
