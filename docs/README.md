# Accountrack Documentation Index

`CLAUDE.md` (repository root) is the single source of truth. These documents elaborate it.
Decisions confirmed 2026-06-13 are recorded in [DECISIONS.md](DECISIONS.md).

## Reading order for new contributors
1. [`../CLAUDE.md`](../CLAUDE.md) — master instructions (read fully).
2. [STATUS.md](STATUS.md) — current milestones & what to do next.
3. [GLOSSARY.md](GLOSSARY.md) — shared vocabulary.
4. [ARCHITECTURE.md](ARCHITECTURE.md) — how the system is built.
5. [DECISIONS.md](DECISIONS.md) — why it's built that way (ADRs).
6. The design doc for your area (below).

## Catalog

### Product
- [STATUS.md](STATUS.md) — **start here**: milestones, where we are, what's next.
- [END_TO_END_GUIDE.md](END_TO_END_GUIDE.md) — click-through walkthrough of a full business cycle
  (master data → purchasing → sales → expenses → inventory) and how to read the resulting accounting.
- [PRD.md](PRD.md) — product requirements, MVP scope.
- [ROADMAP.md](ROADMAP.md) — phased delivery plan.
- [SUBSCRIPTION_BILLING.md](SUBSCRIPTION_BILLING.md) — monetization: how Accountrack charges its own
  tenants (plans, payment providers, subscription lifecycle, tax) — *draft for discussion*.

### Architecture & Integration
- [ARCHITECTURE.md](ARCHITECTURE.md) — modular monolith, clean architecture, boundaries, pipeline.
- [MODULES.md](MODULES.md) — module catalog (purpose, deps, events, MVP scope).
- [INTEGRATION_EVENTS.md](INTEGRATION_EVENTS.md) — inter-module contracts, events, consistency.

### Data & API
- [DATABASE.md](DATABASE.md) — conventions, shared kernel, core schema, indexing, migrations.
- [API_SPEC.md](API_SPEC.md) — REST conventions, response envelope, resource catalog.
- [ERROR_HANDLING.md](ERROR_HANDLING.md) — error model, codes, status mapping.

### Accounting
- [ACCOUNTING_DESIGN.md](ACCOUNTING_DESIGN.md) — GL, journals, periods, AR/AP, reports, tax.
- [POSTING_RULES.md](POSTING_RULES.md) — account determination matrix per event.

### Inventory
- [INVENTORY_DESIGN.md](INVENTORY_DESIGN.md) — ledger, moving-average costing, operations.

### Workflow
- [WORKFLOW_APPROVAL.md](WORKFLOW_APPROVAL.md) — approval engine, lifecycle, process tracker.

### Security & Tenancy
- [SECURITY.md](SECURITY.md) — auth, RBAC, SoD, threats.
- [MULTI_TENANCY.md](MULTI_TENANCY.md) — tenant/company isolation (mandatory controls).

### Rules & Decisions
- [BUSINESS_RULES.md](BUSINESS_RULES.md) — catalog of business rules (`BR-*` ids).
- [DECISIONS.md](DECISIONS.md) — Architectural Decision Records.
- [GLOSSARY.md](GLOSSARY.md) — ubiquitous language.
- [adr/0000-template.md](adr/0000-template.md) — ADR template.
- [../CHANGELOG.md](../CHANGELOG.md) — immutable change history (`CHG-*` ids; see CLAUDE.md → CHANGELOG Rules).

### Engineering
- [CODING_STANDARDS.md](CODING_STANDARDS.md) — code conventions and quality rules.
- [TESTING.md](TESTING.md) — testing strategy and high-priority suites.
- [DEPLOYMENT.md](DEPLOYMENT.md) — environments, CI/CD, migrations, secrets.
- [VPS_DEPLOYMENT_GUIDE.md](VPS_DEPLOYMENT_GUIDE.md) — step-by-step runbook for deploying onto a VPS
  that already runs a Docker stack (reuse existing PostgreSQL + Nginx, expand SAN cert, pgAdmin tunnel).
- [CONTRIBUTING.md](CONTRIBUTING.md) — workflow for human and AI contributors.

## Maintenance rule
Documentation is part of the product. A change to schema, architecture, API, or business rules
updates the matching doc in the **same PR** (`CLAUDE.md` Documentation Rules).
