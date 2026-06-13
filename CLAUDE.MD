# CLAUDE.md

# Accountrack - Master Project Instructions

## Purpose

This file is the single source of truth for AI coding assistants, developers, architects, and contributors working on Accountrack.

All design, implementation, architectural, and business decisions must align with this document.

If any implementation conflicts with this document, this document takes precedence unless explicitly superseded by an approved architectural decision.

---

# Project Overview

## Project Name

Accountrack

## Project Type

Modern ERP and Accounting Platform

## Target Users

* Small Businesses
* Medium Businesses
* Trading Companies
* Distribution Companies
* Manufacturing Companies

## Long-Term Vision

Build a complete business operating system covering:

* Accounting
* Sales
* Purchasing
* Inventory
* Manufacturing
* Reporting
* Workflow Automation
* Audit Trail
* AI Assistant
* Business Intelligence

---

# Core Principles

1. Business correctness over convenience.
2. Maintainability over premature optimization.
3. Security by default.
4. Auditability by default.
5. Multi-company support from day one.
6. Accounting integrity is non-negotiable.
7. Inventory traceability is non-negotiable.
8. Architecture should allow future growth without rewriting the system.
9. Documentation is part of the product.
10. Every major decision must be documented.

---

# Technology Stack

## Backend

* .NET 8
* ASP.NET Core Web API
* Entity Framework Core
* SQL Server

## Frontend

* Vue 3
* TypeScript
* Pinia
* Vue Router
* Tailwind CSS

## Infrastructure

* Docker
* Docker Compose
* Nginx
* GitHub
* GitHub Actions

## Future Infrastructure

* Redis
* Hangfire
* RabbitMQ
* OpenTelemetry
* ElasticSearch

---

# Architecture

## Architecture Style

Use:

* Modular Monolith

Do NOT use:

* Microservices

for MVP.

Modules must be designed so they can be extracted into microservices later if necessary.

---

# Clean Architecture

Each module should contain:

* Domain
* Application
* Infrastructure
* API

Example:

Modules/
├── Identity/
├── Sales/
├── Purchasing/
├── Inventory/
├── Accounting/
├── Manufacturing/
├── Reporting/

Rules:

* Domain layer contains business rules.
* Application layer contains use cases.
* Infrastructure contains persistence and integrations.
* API contains endpoints and contracts.
* Domain must not depend on Infrastructure.
* Modules must remain loosely coupled.

---

# Inter-Module Communication

(Confirmed 2026-06-13 — see [docs/INTEGRATION_EVENTS.md](docs/INTEGRATION_EVENTS.md) and [ADR-0007](docs/DECISIONS.md).)

* Modules MUST NOT read or write another module's tables directly.
* Modules communicate through published **integration events** (in-process via a mediator) and public **application-service contracts**.
* Effects that must be reliable but may be eventually consistent (e.g. posting a journal after an invoice) use a **transactional outbox**.
* Effects that must be atomic with the originating transaction (where business correctness requires it) are committed in the same database transaction.
* Each module owns its own EF Core schema/table-prefix to keep future microservice extraction mechanical.
* Module boundaries are enforced in CI by an architecture-fitness test (NetArchTest).

---

# Multi-Tenant Strategy

## Database Strategy

Single Database

Shared Schema

All business data must contain:

* TenantId
* CompanyId

---

## Tenant Model

Tenant

Represents:

* Subscription owner
* Business owner
* Organization

Company

Represents:

* Legal entity
* Business unit
* Operational company

Example:

Tenant:

* Yovan Account

Companies:

* Shirt Lab
* Future Company A
* Future Company B

---

# Data Isolation Rules

Users must never access:

* Other Tenant data

Every business query must be tenant-aware.

Use:

* Global Query Filters
* Authorization Guards
* Repository Validation

to enforce isolation.

---

# Core Modules

## Foundation Modules

### Identity

Responsibilities:

* Authentication
* Authorization
* Users
* Roles
* Permissions

### Company Management

Responsibilities:

* Company Setup
* Company Configuration
* Tenant Configuration

### Audit Log

Responsibilities:

* Entity Changes
* User Actions
* Security Events

### Approval Workflow

Responsibilities:

* Approval Routing
* Approval Levels
* Escalation

### Process Tracker

Responsibilities:

* Timeline History
* Process Visibility

### Notification

Responsibilities:

* Email
* In-App Notifications

---

# ERP Modules

## Master Data

### Products

* Product Management
* Product Categories
* Product Types
* Units of Measure

### Customers

* Customer Profiles
* Customer Contacts

### Suppliers

* Supplier Profiles
* Supplier Contacts

### Warehouses

* Warehouse Management

### Chart Of Accounts

* Accounting Structure

### Tax Setup

MVP scope (confirmed 2026-06-13 — see [ADR-0012](docs/DECISIONS.md)):

* Indonesia-first. Single-rate **PPN (VAT 11%)** applied on Sales Invoices and Purchase Invoices.
* Tax is captured per invoice line (tax code, rate, tax base, tax amount) and posted to dedicated **VAT Output** (sales) and **VAT Input** (purchase) accounts.
* Schema is designed to support multi-rate, tax-inclusive/exclusive, and withholding tax in future phases without migration.

See [docs/ACCOUNTING_DESIGN.md](docs/ACCOUNTING_DESIGN.md) and [docs/POSTING_RULES.md](docs/POSTING_RULES.md).

---

# Sales Module

Responsibilities:

* Quotations
* Sales Orders
* Delivery Orders
* Sales Invoices
* Customer Payments
* Sales Returns

Features:

* Status Workflow
* Approval Support
* Inventory Integration
* Accounting Integration

---

# Purchasing Module

Responsibilities:

* Purchase Requests
* Purchase Orders
* Goods Receipts
* Purchase Invoices
* Supplier Payments
* Purchase Returns

Features:

* Approval Support
* Inventory Integration
* Accounting Integration

---

# Inventory Module

Responsibilities:

* Inventory Ledger
* Stock Movements
* Stock Adjustment
* Stock Transfer
* Stock Opname

Inventory is transaction-based.

Do NOT rely on Product.CurrentStock as the source of truth.

InventoryTransaction is the source of truth.

---

# Accounting Module

Responsibilities:

* Journal Entries
* General Ledger
* Trial Balance
* Profit & Loss
* Balance Sheet
* Cash Flow
* Accounts Receivable
* Accounts Payable

Accounting must use:

Double Entry Accounting

Additional confirmed design (2026-06-13 — see [docs/ACCOUNTING_DESIGN.md](docs/ACCOUNTING_DESIGN.md)):

* **Fiscal periods** with period open/close/lock. No posting into a closed period.
* **AR and AP are subledgers** with open-item tracking and payment allocation, reconciled to GL control accounts.
* **GR/IR clearing account** (Goods Received / Invoice Received) decouples goods receipt from supplier invoice.
* **Corrections are reversal-only.** A posted journal is immutable; corrections are made via a reversing journal.
* **Single functional currency per company** (e.g. IDR). Amounts carry a currency code and reserved FX fields; foreign-currency transactions and revaluation are a future phase.

---

# Manufacturing Module

Not included in MVP.

Architecture must support future implementation of:

* Bill Of Materials
* Production Orders
* Work In Progress
* Finished Goods
* Production Costing

---

# Accounting Principles

## Double Entry Accounting

Every financial transaction must generate:

* Debit
* Credit

No exceptions.

Accounting Equation:

Assets = Liabilities + Equity

---

## Automatic Journal Generation

Users should not manually create journals for:

* Sales Invoice
* Purchase Invoice
* Payments
* Expenses
* Inventory Adjustments

The system must generate journals automatically.

---

## General Ledger Is Source Of Truth

Reports must derive from Journal Entries.

Never generate accounting reports directly from transactional tables.

---

# Inventory Principles

## Inventory Ledger

InventoryTransaction is the source of truth.

Examples:

* Purchase Receipt
* Sales Shipment
* Stock Adjustment
* Stock Transfer
* Production Consumption
* Production Receipt

---

## Costing Strategy

Initial:

* Moving Average

Future:

* FIFO
* Batch Costing
* Manufacturing Costing

---

# Authentication

Phase 1

* Email Login
* Password Login
* JWT Access Token
* Refresh Token

Phase 2

* Google OAuth
* Microsoft OAuth

---

# Authorization

Use RBAC.

Never hardcode permissions.

Examples:

Sales.View
Sales.Create
Sales.Edit
Sales.Delete

Inventory.View
Inventory.Adjust

Accounting.View
Accounting.Post
Accounting.Approve

Permissions must be configurable.

## Segregation of Duties

(Confirmed 2026-06-13 — see [docs/SECURITY.md](docs/SECURITY.md).)

* The permission model must support **segregation of duties**: the user who creates a financial document must not be the only one able to approve or post it.
* Sensitive actions (Approve, Post, Pay) are distinct permissions from Create/Edit.

## Authorization Scope

* Tenant isolation is a hard wall (enforced by global query filters + ambient tenant context from the JWT).
* **Company access is a per-user grant**: a user belongs to one tenant and is granted access to a subset of companies within it.

---

# Approval Workflow

The system must support:

* Single-Level Approval
* Multi-Level Approval
* Conditional Approval

Examples:

* Purchase Orders
* Expenses
* Inventory Adjustments

---

# Process Tracker

Every document should expose lifecycle history.

Example:

Created
Submitted
Approved
Posted
Completed

Users must be able to see the full document journey.

---

# Audit Log

Audit Log must capture:

* User
* Timestamp
* Entity
* Action
* Before Value
* After Value

Audit records must be immutable.

---

# Internationalization

Documentation Language:

English

Application Default Language:

English

Secondary Language:

Bahasa Indonesia

Requirements:

* No hardcoded UI text
* Translation resources required
* Language switching supported

---

# API Standards

Base Route:

/api/v1

Examples:

/api/v1/products
/api/v1/customers
/api/v1/sales-orders

---

## API Response Standard

Success:

{
"success": true,
"data": {}
}

Failure:

{
"success": false,
"message": "Validation failed"
}

---

# Database Standards

## Primary Keys

Use:

GUID

Avoid:

INT IDENTITY

for business entities.

---

## Standard Audit Fields

Every table should contain:

* CreatedAt
* CreatedBy
* UpdatedAt
* UpdatedBy
* DeletedAt
* DeletedBy
* IsDeleted

---

# Coding Standards

## C#

Examples:

SalesOrder
PurchaseInvoice
InventoryTransaction

## SQL

Examples:

SalesOrders
PurchaseInvoices
InventoryTransactions

## API

Examples:

sales-orders
purchase-invoices
inventory-transactions

---

# Code Quality Rules

Avoid:

* God Classes
* Massive Services
* Circular Dependencies
* Shared Mutable State
* Static Business Logic

Prefer:

* SOLID
* Dependency Injection
* Clean Architecture
* Domain Driven Design principles

---

# Testing Strategy

Required:

* Unit Tests
* Integration Tests

High Priority Coverage:

* Accounting
* Inventory
* Approval Workflow
* Security

---

# Deployment Strategy

Environments:

* Local
* Development
* UAT
* Production

Configuration:

* appsettings.json
* appsettings.Development.json
* appsettings.Production.json

Never hardcode:

* Secrets
* Connection Strings
* API Keys

---

# Documentation Structure

Project docs Structure:

/
├── CLAUDE.md
├── README.md
└── docs/
├── README.md            (docs index)
├── PRD.md
├── ROADMAP.md
├── ARCHITECTURE.md
├── MODULES.md
├── DATABASE.md
├── API_SPEC.md
├── ERROR_HANDLING.md
├── ACCOUNTING_DESIGN.md
├── POSTING_RULES.md
├── INVENTORY_DESIGN.md
├── WORKFLOW_APPROVAL.md
├── MULTI_TENANCY.md
├── SECURITY.md
├── INTEGRATION_EVENTS.md
├── GLOSSARY.md
├── BUSINESS_RULES.md
├── DECISIONS.md
├── CODING_STANDARDS.md
├── TESTING.md
├── DEPLOYMENT.md
├── CONTRIBUTING.md
└── adr/
    └── 0000-template.md

---

# Documentation Rules

Documentation is part of the product.

Whenever a major change occurs:

* Update Architecture
* Update Database Design
* Update API Specification
* Update Module Documentation
* Update Business Rules
* Update Decisions

Documentation must remain synchronized with implementation.

---

# DECISIONS.md Purpose

Stores Architectural Decision Records (ADR).

Examples:

* Why Modular Monolith was chosen
* Why SQL Server was chosen
* Why Moving Average was selected
* Why Shared Database Multi-Tenancy was selected

Every major architecture decision must be documented.

---

# BUSINESS_RULES.md Purpose

Stores business rules.

Examples:

Sales:

* Sales Order requires at least one line item.
* Sales Invoice cannot exceed Sales Order quantity.

Accounting:

* Posted Journal cannot be edited.
* Journal must remain balanced.

Inventory:

* Negative stock policy.
* Stock adjustment approval thresholds.

Purchasing:

* Purchase Order approval requirements.

Business rules must never exist only in source code.

They must be documented.

---

# Roadmap

## Phase 1

Foundation

* Authentication
* Company Management
* Roles
* Permissions
* Audit Log
* Approval Workflow

## Phase 2

Core ERP

* Products
* Customers
* Suppliers
* Sales
* Purchasing
* Inventory
* Accounting
* Reporting

## Phase 3

Advanced ERP

* Manufacturing
* CRM
* Assets
* Payroll
* Tax

## Phase 4

Intelligence

* AI Assistant
* Forecasting
* Analytics
* Workflow Builder

---

# Non-Negotiable Rules

1. Use .NET 8.
2. Use SQL Server.
3. Use Vue 3.
4. Use TypeScript.
5. Use Tailwind CSS.
6. Use Modular Monolith architecture.
7. Support multi-company from day one.
8. Use TenantId and CompanyId.
9. Use double-entry accounting.
10. Use inventory ledger.
11. Use moving average costing initially.
12. Never physically delete business data.
13. Maintain complete audit history.
14. Use RBAC authorization.
15. Make permissions configurable.
16. Design modules for future microservice extraction.
17. Documentation must be written in English.
18. English is the default UI language.
19. Indonesian language must be supported.
20. Business transactions must be traceable end-to-end.
21. Documentation must be maintained continuously.
22. Every major decision must be documented.
23. Every major business rule must be documented.
24. Security takes precedence over convenience.
25. Maintainability takes precedence over premature optimization.
26. Business correctness takes precedence over implementation speed.
27. Modules must never access another module's tables directly; communicate via integration events or application contracts.
28. Posted journals are immutable; corrections are reversal-only.
29. AR and AP are maintained as subledgers reconciled to GL control accounts.
30. No financial posting into a closed fiscal period.
31. Money is always stored with an explicit currency code; one functional currency per company.
32. The permission model must support segregation of duties.
33. Tenant isolation must be enforced by ambient context, never by a client-supplied parameter; `IgnoreQueryFilters` is banned outside reviewed admin paths.
