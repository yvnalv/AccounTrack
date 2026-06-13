# Accountrack Changelog

## [2026-06-13 13:02:09 UTC]

CHG-0008 — Accounting engine (slice 1): chart of accounts, fiscal periods, double-entry journals, trial balance

- Implemented the Phase 2 **Accounting** backbone across clean-architecture layers. Verified
  end-to-end against a real database: posted a balanced journal, rejected an unbalanced one
  (BR-ACC-1), produced a balancing Trial Balance, reversed the entry (net-zero), and saw all of it
  captured automatically by the audit log.
- **Domain:** `Account` (+ `AccountType`/`NormalBalance`/`ControlType`), `FiscalYear`/`FiscalPeriod`
  (open/close/lock), `JournalEntry`/`JournalLine` (balanced double-entry, immutable, reversal-only),
  per-company `JournalNumberSequence`, errors.
- **Application:** ports + `IJournalPoster`; CreateAccount/GetAccounts; CreateFiscalYear/Close/Reopen;
  PostJournal, ReverseJournal, GetJournalEntry; GetTrialBalance (derived from the GL, ADR-0008).
- **Infrastructure:** `AccountingDbContext` (own `accounting` schema) + configs (owned `Money`
  columns, `DateOnly` periods), repositories, GL trial-balance read store, default Indonesian-SMB
  chart + current fiscal-year seeder, DI, design-time factory, `InitialAccounting` migration.
- **Api:** `/api/v1/accounts`, `/api/v1/fiscal-years`, `/api/v1/fiscal-periods/{id}/close|reopen`,
  `/api/v1/journal-entries` (post/get/reverse), `/api/v1/reports/trial-balance`. Permission-gated
  (Accounting.View/Post/PeriodClose/PeriodReopen, MasterData.Manage). JSON enums as strings.
- **Cross-module:** added the `Modules.Contracts` project with `ICompanyDirectory` (implemented by
  Company Management) so Accounting reads a company's functional currency without coupling.
- **Tests:** 16 Accounting unit tests (journal invariants, reversal, posting service:
  balance/period/account rules) + Accounting architecture-boundary tests. Full suite now 62, green.
- **Scope:** slice 1 only. Slice 2 (next): posting rules / account determination (POSTING_RULES.md),
  AR/AP subledgers, period-close snapshots, and P&L / Balance Sheet / Cash Flow reports.
- See [docs/ACCOUNTING_DESIGN.md](docs/ACCOUNTING_DESIGN.md), [docs/MODULES.md](docs/MODULES.md).

---

## [2026-06-13 12:35:38 UTC]

CHG-0007 — Sync design docs with implemented foundation

- Documentation-only pass to bring the design docs in line with the implemented modules
  (CLAUDE.md "documentation must remain synchronized").
- **ARCHITECTURE.md:** updated the solution layout with implemented/planned (✅/◻️) markers, the
  new `Web.Common` building block, the `CompanyManagement` naming note, and why AuditLog has no
  Domain project; noted atomic shared-table audit in the cross-cutting table.
- **DATABASE.md:** documented the three entity bases (`Entity` / `TenantScopedEntity` /
  `TenantOwnedEntity`), `PagedResult<T>`, and the shared append-only `AuditEntry` / `audit.AuditEntries`
  table (ADR-0026).
- **MODULES.md:** added an implementation-status table.
- **ROADMAP.md:** checked off Phase 1 items 1–4; flagged the cross-tenant integration test suite
  as still outstanding.
- **DECISIONS.md:** added [ADR-0027](docs/DECISIONS.md) (build with the .NET 9 SDK while targeting
  net8.0).
- No code changes.

---

## [2026-06-13 12:25:32 UTC]

CHG-0006 — Audit Log module (automatic, atomic before/after capture)

- Implemented the Phase 1 **Audit Log** with automatic, tamper-evident change capture
  (ADR-0006/0026, SECURITY.md §4). Verified end-to-end against a real database (a login's
  `LastLoginAtUtc` change was captured and returned by the API).
- **Shared kernel:** `AuditEntry` (append-only primitive: tenant/company, entity type+id, action,
  before/after JSON, user, timestamp) + `AuditAction`; plus a general `PagedResult<T>`.
- **Infrastructure.Common:** `AuditCaptureInterceptor` records inserts/updates/deletes and adds an
  `AuditEntry` to the **same transaction** as the change (runs before soft-delete conversion).
  `BaseDbContext` maps the shared `audit.AuditEntries` table, excluded from each module's
  migrations.
- **AuditLog module:** `AuditDbContext` owns the table + `InitialAudit` migration; tenant-scoped
  read store + `GET /api/v1/audit-entries` (filtered, paged), gated by the new `Audit.View`
  permission.
- **Wiring:** the capture interceptor is registered in the Identity and Company DbContexts; the
  host registers the module and creates the audit table before modules that write to it.
- **Shared kernel ergonomics:** (none beyond the above).
- **Tests:** audit-capture interceptor tests (insert + update before/after) and AuditLog
  architecture-boundary tests. Full suite now 43, green.
- See [docs/SECURITY.md](docs/SECURITY.md), [docs/DATABASE.md](docs/DATABASE.md),
  [ADR-0026](docs/DECISIONS.md).

---

## [2026-06-13 12:09:43 UTC]

CHG-0005 — Company Management module (tenants, companies, settings)

- Implemented the Phase 1 **Company Management** module across clean-architecture layers
  (Domain / Application / Infrastructure / Api).
- **Domain:** `Tenant` (tenancy root), `Company` (tenant-scoped; functional currency, fiscal-year
  start month, time zone, tax id), `CompanySetting` (company-owned key/value), domain errors.
- **Application:** CreateCompany, UpdateCompany, SetCompanySetting commands + GetCompanies /
  GetCompanyById queries (MediatR + FluentValidation, returning `Result`).
- **Infrastructure:** `CompanyDbContext` (own `company` schema) + configurations, repository,
  dev tenant/company seeder (well-known GUIDs matching the Identity dev admin's grant), DI,
  design-time factory, and the `InitialCompany` migration (verified to apply to a real SQL Server).
- **Api:** `GET/POST/PUT /api/v1/companies`, `GET /api/v1/companies/{id}`,
  `PUT /api/v1/companies/{id}/settings` (create/update/settings gated by `Admin.Companies`).
- **Host:** registers the module and seeds Company before Identity at startup so the dev admin's
  tenant/company exist.
- **Shared kernel:** added an implicit `Error` → `Result` conversion for ergonomics.
- **Naming:** module uses `Accountrack.CompanyManagement.*` namespaces to avoid the `Company`
  type vs namespace-segment collision.
- **Tests:** 10 Company unit tests (domain invariants + CreateCompany handler) and Company
  architecture-boundary tests. Full suite now 39, green.
- See [docs/MODULES.md](docs/MODULES.md), [docs/DATABASE.md](docs/DATABASE.md).

---

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
