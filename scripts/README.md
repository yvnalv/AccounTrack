# scripts/

Developer utilities. Not part of the deployed application.

## seed_dummy_data.py — realistic demo data

Populates the **dev company** with a coherent set of dummy data by driving the
real HTTP API. Because every record is created through the normal endpoints, the
result is internally consistent: the general ledger balances, AR/AP subledgers
reconcile to their control accounts, the inventory ledger matches stock movements,
and the dashboard reflects real figures.

What it creates:

- **Master data** — 12 products across 4 categories, 8 customers, 6 suppliers.
- **Opening balances** — an owner's-capital journal (Dr Bank / Cr Equity) and one
  fully received + billed + paid purchase order per product (opening stock).
- **Purchasing** — purchase orders in mixed states (draft / submitted / received /
  billed / paid); roughly a third of bills left open to populate Accounts Payable.
- **Sales** — sales orders in mixed states (draft → delivered → invoiced → paid);
  roughly a third of invoices left open to populate Accounts Receivable. The most
  recent orders are dated in the current month so the dashboard shows live revenue.
- **Expenses** — a dozen operating-expense vouchers across the seeded categories.

### Prerequisites

The API must be running with a migrated + base-seeded database:

```bash
cd src/Bootstrapper/Accountrack.Api
ASPNETCORE_ENVIRONMENT=Development Database__Initialize=true \
  Database__AutoMigrate=true Seed__Enabled=true dotnet run
```

### Run

```bash
python scripts/seed_dummy_data.py
# or against a different host / credentials:
python scripts/seed_dummy_data.py http://localhost:5080 admin@accountrack.local 'ChangeMe!123'
```

Uses only the Python standard library (no pip install). Master data is created by
stable codes and re-looked-up on conflict, so re-running is safe — it appends more
transactions. For a perfectly clean slate, drop and recreate the dev database first:

```sql
ALTER DATABASE Accountrack_Dev SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE Accountrack_Dev;
```

then restart the API (which recreates and base-seeds it) and re-run the script.
