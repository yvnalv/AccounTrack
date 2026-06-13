# Accountrack

**A modern ERP and accounting platform for small–medium enterprises** — built for trading,
distribution, and manufacturing businesses, designed to grow into a complete business operating
system.

> Status: **architecture & design phase.** No application code yet — the documentation below
> defines the system before implementation begins.

## What it is

Accountrack is a multi-company, multi-tenant ERP with a correct, auditable, double-entry
accounting core and a transaction-based inventory ledger. MVP covers Foundation (identity,
tenancy, audit, approvals) plus core ERP (Sales, Purchasing, Inventory, Accounting, Reporting),
with Indonesian PPN (VAT 11%) support.

## Tech stack

- **Backend:** .NET 8, ASP.NET Core Web API, EF Core, SQL Server
- **Frontend:** Vue 3, TypeScript, Pinia, Vue Router, Tailwind CSS
- **Architecture:** Modular Monolith + Clean Architecture (microservice-extraction ready)
- **Infra:** Docker, Docker Compose, Nginx, GitHub Actions

## Core invariants

Multi-company from day one · strict tenant isolation · double-entry accounting (GL is the source
of truth) · inventory ledger is the source of truth · moving-average costing · soft delete +
full audit history · RBAC with segregation of duties · automatic journal posting.

## Documentation

`CLAUDE.md` is the single source of truth. Detailed design lives in [`docs/`](docs/) — start with
the [docs index](docs/README.md).

| Area | Doc |
|---|---|
| Product & roadmap | [PRD](docs/PRD.md) · [ROADMAP](docs/ROADMAP.md) |
| Architecture | [ARCHITECTURE](docs/ARCHITECTURE.md) · [MODULES](docs/MODULES.md) · [INTEGRATION_EVENTS](docs/INTEGRATION_EVENTS.md) |
| Data & API | [DATABASE](docs/DATABASE.md) · [API_SPEC](docs/API_SPEC.md) · [ERROR_HANDLING](docs/ERROR_HANDLING.md) |
| Accounting | [ACCOUNTING_DESIGN](docs/ACCOUNTING_DESIGN.md) · [POSTING_RULES](docs/POSTING_RULES.md) |
| Inventory | [INVENTORY_DESIGN](docs/INVENTORY_DESIGN.md) |
| Workflow | [WORKFLOW_APPROVAL](docs/WORKFLOW_APPROVAL.md) |
| Security & tenancy | [SECURITY](docs/SECURITY.md) · [MULTI_TENANCY](docs/MULTI_TENANCY.md) |
| Rules & decisions | [BUSINESS_RULES](docs/BUSINESS_RULES.md) · [DECISIONS](docs/DECISIONS.md) · [GLOSSARY](docs/GLOSSARY.md) |
| Engineering | [CODING_STANDARDS](docs/CODING_STANDARDS.md) · [TESTING](docs/TESTING.md) · [DEPLOYMENT](docs/DEPLOYMENT.md) · [CONTRIBUTING](docs/CONTRIBUTING.md) |
| History | [CHANGELOG](CHANGELOG.md) — notable changes (`CHG-*`, reverse-chronological) |

## Contributing

See [CONTRIBUTING](docs/CONTRIBUTING.md). All contributors (human and AI) must read `CLAUDE.md`
first; it overrides default behavior.
