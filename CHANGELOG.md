# Accountrack Changelog

## [2026-06-13 11:52:14 UTC]

CHG-0004 — Introduce changelog and changelog policy

- Added this `CHANGELOG.md` as the project's immutable historical record, in reverse-chronological
  order (newest on top) using sequential `CHG-NNNN` ids and UTC timestamps.
- Backfilled entries CHG-0001..CHG-0003 for the work delivered so far.
- Added a **Changelog** policy section to `CLAUDE.md` (ordering, id, timestamp, and AI-agent rules)
  and listed `CHANGELOG.md` in the documentation structure.
- Referenced the changelog from `README.md` and `docs/README.md`; recorded the decision as
  [ADR-0025](docs/DECISIONS.md).
- Docs only — no code or schema changes.

---

## [2026-06-13 11:42:07 UTC]

CHG-0003 — Identity module (authentication, RBAC, JWT, refresh rotation)

- Implemented the Phase 1 **Identity** module end-to-end across clean-architecture layers
  (Domain / Application / Infrastructure / Api). Commit `204f82b`.
- **Domain:** `User`, `Role`, `Permission`, `RefreshToken` (+ join entities), `Email` value
  object, permission catalog, domain errors.
- **Application:** Login, RefreshToken (rotation + reuse-detection family revocation), Logout,
  CreateUser — MediatR handlers with FluentValidation, returning `Result`.
- **Infrastructure:** `IdentityDbContext` (own `identity` schema) + EF configurations, PBKDF2
  password hasher, HS256 token service, repositories (auth lookups use the reviewed tenant-filter
  bypass), permission/dev-admin seeder, DI, and the `InitialIdentity` migration (verified to apply
  to a real SQL Server).
- **Api:** `POST /api/v1/auth/login·refresh·logout`, `GET /api/v1/auth/me`, `POST /api/v1/users`.
- **Shared kernel:** added `TenantScopedEntity` / `ITenantScoped` (tenant-only, for Users/Roles);
  extended `BaseDbContext` query filters and the auditing interceptor accordingly.
- **Web.Common (new building block):** standard response envelope, `Result`→HTTP mapping, and the
  exception middleware (moved out of the host for reuse by module Api layers).
- **Host:** JWT bearer authentication (claims unmapped), HttpContext-backed `ICurrentUser` /
  `ITenantContext` replacing the placeholders, a permission-as-policy provider, and Identity module
  registration with optional startup migrate + seed (off by default).
- **Tests:** 20 Identity unit tests + Identity architecture-boundary tests (total suite 26, green).
- Decisions: [ADR-0019](docs/DECISIONS.md) (RBAC + SoD), [ADR-0020](docs/DECISIONS.md) (JWT +
  rotating refresh). See [docs/SECURITY.md](docs/SECURITY.md), [docs/MODULES.md](docs/MODULES.md).

---

## [2026-06-13 08:38:00 UTC]

CHG-0002 — Modular-monolith solution skeleton and shared kernel

- Scaffolded the .NET 8 solution and Phase 1 building blocks. Commit `907f2f0`.
- `Accountrack.sln` + `Directory.Build.props` (net8.0, nullable, warnings-as-errors).
- **SharedKernel:** `Entity`, `TenantOwnedEntity`, `ValueObject`, `Money`, `Result`/`Error`,
  domain-event base, audit/soft-delete/tenancy marker interfaces (no dependencies).
- **Application.Abstractions:** CQRS contracts, ambient-context ports
  (`ITenantContext`/`ICurrentUser`/`IClock`), logging + validation pipeline behaviors.
- **Infrastructure.Common:** `BaseDbContext` with global query filters (soft-delete + tenant),
  auditing/tenancy `SaveChanges` interceptor, outbox message.
- **Accountrack.Api host:** error-envelope middleware, response envelope, MediatR pipeline,
  Swagger, `/health` and `/api/v1/ping` (verified returning the standard envelope).
- **ArchitectureTests:** NetArchTest boundary rules ([ADR-0023](docs/DECISIONS.md)).
- See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

---

## [2026-06-13 07:31:13 UTC]

CHG-0001 — Architecture and design documentation foundation

- Established the full `docs/` set defining the system before implementation, plus a
  stack-tailored `.gitignore`. Commit `2fef2a1`.
- Authored 22 documents + index and an ADR template: PRD, ROADMAP, ARCHITECTURE, MODULES,
  INTEGRATION_EVENTS, DATABASE, API_SPEC, ERROR_HANDLING, ACCOUNTING_DESIGN, POSTING_RULES,
  INVENTORY_DESIGN, WORKFLOW_APPROVAL, SECURITY, MULTI_TENANCY, BUSINESS_RULES, DECISIONS,
  GLOSSARY, CODING_STANDARDS, TESTING, DEPLOYMENT, CONTRIBUTING.
- Recorded 24 architectural decisions (ADR-0001..ADR-0024), including modular monolith,
  shared-DB multi-tenancy, double-entry GL as source of truth, AR/AP subledgers, GR/IR clearing,
  moving-average inventory, Indonesia-first PPN, single functional currency, and RBAC + SoD.
- Updated `CLAUDE.md` with the confirmed decisions and new non-negotiable rules; refreshed
  `README.md`.
