# DATABASE.md

Database conventions and the core schema for Accountrack. This is a **logical design** — the
authoritative schema is the EF Core model + migrations. SQL Server, EF Core (ADR-0003).

## 1. Conventions

| Topic | Rule |
|---|---|
| PK | `Id uniqueidentifier` (sequential GUID, ADR-0005), named `Id` |
| Tenancy | `TenantId`, `CompanyId` on every business table (ADR-0004) |
| Audit | `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `DeletedAt`, `DeletedBy`, `IsDeleted` |
| Concurrency | `RowVersion rowversion` on mutable documents (ADR-0021) |
| Money | `decimal(19,4)` amount + `CurrencyCode char(3)`; never float (ADR-0013) |
| Quantity | `decimal(19,6)` (supports fractional UoM) |
| Rates/% | `decimal(9,6)` |
| Tables | PascalCase plural (`SalesOrders`, `InventoryTransactions`) |
| Schemas | one per module: `identity`, `company`, `masterdata`, `sales`, `purchasing`, `inventory`, `accounting`, `approval`, `audit`, `notification`, `reporting` |
| Columns | PascalCase |
| FK | `<Entity>Id` (e.g. `CustomerId`); FKs do not cross module schemas (boundary rule) |
| Enums | stored as `tinyint`/`int` with a documented mapping (or string for stable external values) |
| Timestamps | UTC `datetime2`; convert to user TZ in presentation only |
| Booleans | `bit`, non-null with default |

### Indexing
- Every index/unique constraint on a business table **leads with `TenantId, CompanyId`**.
- Business document numbers: `UNIQUE (CompanyId, DocumentNumber)` — per-company, not global.
- Foreign-key columns are indexed.
- Filtered indexes exclude soft-deleted rows where it helps (`WHERE IsDeleted = 0`).

### Cross-module references
A module never has a FK into another module's table. To reference another module's entity it
stores the **id only** (no DB-level FK) and resolves via that module's contract/event. This
keeps schemas independently migratable and extraction-ready.

## 2. Shared Kernel (applies to all business entities)

```
Entity (BaseEntity)              TenantScopedEntity : Entity        TenantOwnedEntity : TenantScopedEntity
├── Id            : guid (PK)     ├── (all Entity)                   ├── (all TenantScopedEntity)
├── CreatedAt     : datetime2     └── TenantId : guid (indexed)      └── CompanyId : guid (indexed)
├── CreatedBy     : guid
├── UpdatedAt     : datetime2?    Money (value object, owned)        Inheritance:
├── UpdatedBy     : guid?         ├── Amount       : decimal(19,4)     Entity → TenantScopedEntity → TenantOwnedEntity
├── DeletedAt     : datetime2?    └── CurrencyCode : char(3)
├── DeletedBy     : guid?
├── IsDeleted     : bit          PagedResult<T> (query helper: Items, Page, PageSize, TotalItems)
└── RowVersion    : rowversion   (mutable docs only)
```

Three entity bases (marker interfaces `ITenantScoped` ⊂ `ITenantOwned`):
- **Entity** — not tenant-owned (rare; cross-tenant/system data). Soft-delete filter only.
- **TenantScopedEntity** — belongs to a tenant but not a single company (e.g. Users, Roles,
  Companies). Tenant-only query filter.
- **TenantOwnedEntity** — belongs to a tenant **and** a company (most business data). Tenant +
  company query filter.

Stamping of tenancy + audit fields is automatic (SaveChanges interceptor); application code does
not set them. Query filters and `RowVersion` are applied by convention in `BaseDbContext`.

### AuditEntry (shared, append-only — ADR-0026)
A SharedKernel primitive (not an `Entity`; never soft-deleted/updated) mapped to a single
`audit.AuditEntries` table that every module context can write to in the same transaction as a
change. The AuditLog module's context owns the table's migration; all other module contexts map it
with `ExcludeFromMigrations()`.
```
AuditEntry
├── Id : guid (PK)        ├── EntityType : nvarchar(200)   ├── ChangesJson : nvarchar(max)  (before/after)
├── TenantId : guid        ├── EntityId   : nvarchar(128)   ├── UserId      : guid
├── CompanyId : guid?      ├── Action     : int {Insert,Update,Delete}   ├── TimestampUtc : datetime2
└── CorrelationId : nvarchar(128)?
   indexes: (TenantId, EntityType, EntityId), (TimestampUtc)
```

## 3. Core Tables by Module (logical)

> Columns below omit the standard audit/tenancy/rowversion set unless noteworthy. `→` = stores
> id reference to another module (no DB FK).

### identity
- `Users` (Email unique-per-tenant, PasswordHash, IsActive, …)
- `Roles` (Name, Description)
- `Permissions` (Code e.g. `Sales.Post`, Description) — seeded catalog
- `RolePermissions` (RoleId, PermissionId)
- `UserRoles` (UserId, RoleId)
- `UserCompanies` (UserId, → CompanyId) — granted company scope
- `RefreshTokens` (UserId, TokenHash, FamilyId, ExpiresAt, ConsumedAt, RevokedAt)

### company
- `Tenants` (Name, Status, … ) — note: not tenant-scoped itself
- `Companies` (TenantId, Name, LegalName, FunctionalCurrency, FiscalYearStartMonth, TimeZone, …)
- `CompanySettings` (CompanyId, Key, Value) — e.g. `Inventory.AllowNegativeStock`, default
  posting-rule overrides, numbering formats

### masterdata
- `ProductCategories`, `ProductTypes`
- `UnitsOfMeasure` (Code, Name), `UomConversions` (FromUomId, ToUomId, Factor)
- `Products` (Sku unique-per-company, Name, CategoryId, BaseUomId, IsStockTracked, IsSold,
  IsPurchased, SalesAccountId?, PurchaseAccountId?, TaxCodeId?) — *CurrentStock is NOT stored
  here as truth (ADR-0014)*
- `Customers` (Code, Name, → AR terms, CreditLimit, TaxId/NPWP) + `CustomerContacts`
- `Suppliers` (Code, Name, AP terms, TaxId/NPWP) + `SupplierContacts`
- `Warehouses` (Code, Name, Address)
- `TaxCodes` (Code e.g. `PPN11`, Rate, IsInclusive, OutputAccountId, InputAccountId) — ADR-0012

### accounting
- `Accounts` (Code, Name, Type {Asset,Liability,Equity,Revenue,Expense}, NormalBalance {D,C},
  ParentAccountId?, IsControlAccount, ControlType {AR,AP,Inventory,null}, IsSystem, IsActive)
- `FiscalYears` (Year, StartDate, EndDate, Status)
- `FiscalPeriods` (FiscalYearId, PeriodNo, StartDate, EndDate, Status {Open,Closed,Locked})
- `JournalEntries` (EntryNo unique-per-company, Date, PeriodId, Source {SalesInvoice, …},
  SourceDocumentId, Description, Status {Draft,Posted,Reversed}, ReversedByEntryId?,
  ReversesEntryId?, PostedAt, PostedBy)
- `JournalLines` (JournalEntryId, AccountId, Debit Money, Credit Money, Description,
  → optional SubledgerRef {Customer/Supplier}, optional dimension tags)
- `PostingRules` (CompanyId, EventType, RuleKey, AccountId, Selector) — account determination
  (POSTING_RULES.md)
- `AccountBalanceSnapshots` (AccountId, PeriodId, OpeningDebit, OpeningCredit, PeriodDebit,
  PeriodCredit, ClosingDebit, ClosingCredit) — ADR-0022
- `ArOpenItems` (CustomerId, SourceInvoiceId, OriginalAmount, OutstandingAmount, DueDate,
  Status) ; `ApOpenItems` (SupplierId, SourceBillId, …) — ADR-0011
- `PaymentAllocations` (PaymentId, OpenItemId, AllocatedAmount)
- `OutboxMessages`, `InboxConsumed` (idempotency) — may live in infrastructure schema

### inventory
- `InventoryTransactions` (→ ProductId, WarehouseId, Type {Receipt,Issue,AdjustIn,AdjustOut,
  TransferOut,TransferIn,ProductionConsume,ProductionReceive}, Quantity, UnitCost Money,
  TotalCost Money, MovementDate, SourceModule, SourceDocumentId, RunningQtyAfter,
  RunningAvgCostAfter) — append-only, source of truth (ADR-0014)
- `StockCostBuckets` (→ ProductId, WarehouseId, OnHandQty, AvgUnitCost, Version) — projection,
  serialization point for moving average (ADR-0015/0021)
- `StockAdjustments`, `StockTransfers`, `StockOpnames` (+ lines) — operational documents
- *(reserved for FIFO)* `CostLayers` (ReceiptTxnId, RemainingQty, UnitCost) — not used in MVP

### sales
- `Quotations`, `SalesOrders`, `DeliveryOrders`, `SalesInvoices`, `CustomerPayments`,
  `SalesReturns` — each with `*Lines`, `Status`, `DocumentNumber`, → CustomerId, → product/
  warehouse ids, totals (Subtotal, TaxTotal, GrandTotal as Money)

### purchasing
- `PurchaseRequests`, `PurchaseOrders`, `GoodsReceipts`, `PurchaseInvoices`, `SupplierPayments`,
  `PurchaseReturns` — symmetric to sales; GoodsReceipt/PurchaseInvoice drive GR/IR (ADR-0018)

### approval
- `ApprovalDefinitions` (DocumentType, Conditions), `ApprovalSteps` (level, approver role/user),
  `ApprovalRequests` (DocumentType, DocumentId, Status), `ApprovalActions` (StepId, ApproverId,
  Decision, Comment, ActedAt) — WORKFLOW_APPROVAL.md

### audit
- `AuditEntries` (EntityType, EntityId, Action {Insert,Update,Delete}, ChangesJson{before/after},
  UserId, At, CorrelationId) — append-only, no update/delete

### process tracker (in audit or its own schema)
- `ProcessTimelines` (DocumentType, DocumentId), `ProcessEvents` (Milestone, At, By, Note)

### notification
- `Notifications` (UserId, Type, Title, Body, ReadAt), `NotificationTemplates`,
  `OutboundEmails` (queued)

## 4. Document Numbering
- `NumberSequences` (CompanyId, DocumentType, Prefix, NextNumber, PeriodReset, Format) —
  generates per-company sequences (e.g. `INV/2026/06/0001`). Gapless within a transaction;
  documented behavior on cancellation in BUSINESS_RULES.md.

## 5. Migrations
- Per-module migrations under each module's Infrastructure project.
- Naming: `<UtcTimestamp>_<Module>_<Change>`.
- Applied in dependency order: identity/company/masterdata → accounting → inventory →
  sales/purchasing → approval/audit/notification/reporting.
- Dev: auto-apply at startup behind a flag. UAT/Prod: applied via the deployment pipeline, never
  silently in production.
- Seed data (permissions catalog, default CoA template, `PPN11` tax code, system accounts,
  default posting rules) via idempotent seeders.

## 6. Reporting Reads
Reports read the GL and `AccountBalanceSnapshots` (ADR-0008/0022), never transactional tables
directly. Inventory valuation reads `InventoryTransactions` / `StockCostBuckets`. Drill-down
paths: Report → JournalEntry → SourceDocument.
