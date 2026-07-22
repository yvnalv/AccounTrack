# ADR-0040: General Journal, Cash & Bank flows, and equity/loan chart of accounts

- **Status:** Accepted
- **Date:** 2026-07-22
- **Deciders:** Product owner, Architecture
- **Tags:** accounting, process

## Context

Accountrack automates the journals behind Sales, Purchasing, Inventory, and Expenses
(CLAUDE.md — Automatic Journal Generation). But a business also has financial events that
are none of those: an owner injecting **paid-in capital**, an owner **drawing** money out,
a **bank/cash transfer**, a **loan** received or repaid, **opening balances** at onboarding,
and period-end **accruals / prepayments / depreciation** corrections. Today there is no way
to record any of these from the app:

- The backend already has a manual balanced-journal engine (`PostJournalCommand`,
  `JournalSource.Manual`), but it is **not surfaced in the UI**, there is **no journal
  register** (list) endpoint, and it **posts immediately** with no approval — the most
  powerful posting in the system has the least control.
- The default chart of accounts seeds **only `3900 Retained Earnings`** in the equity range
  (AccountingDataSeeder). There is **no Owner's Capital, Owner's Drawings, Share Capital, or
  Loan Payable** account to post capital/financing events to.
- The seeder short-circuits (`SeedChartAsync` returns if any account exists), so new accounts
  are **not backfilled to existing companies**.

The permission model must support segregation of duties (CLAUDE.md Rule 32); a raw manual
journal is exactly where that matters most.

## Decision

We will add a **General Journal** and a set of guided **Cash & Bank** flows to the Accounting
module, surfaced as two new tabs under Accounting (names i18n-driven: EN "Journals" / "Cash &
Bank"; ID "Jurnal Umum" / "Kas & Bank"), and extend the default chart of accounts with equity
and loan accounts covering both sole-proprietor and PT (corporation) structures.

1. **Manual journals route through the Approval Workflow from the start.** A manual journal (and
   every guided flow) is created and **submitted to the approval engine** exactly as Expense
   vouchers are (ADR-0030): when no approval definition matches it is auto-approved and posted
   atomically; when a definition matches it is held and posted by an Accounting approval consumer
   once approved. A single document type `ManualJournal` carries the `Total` attribute so admins
   configure thresholds; the submitter cannot self-approve (BR-APR-2). Auto-posted journals from
   other modules are unaffected — they never enter this lifecycle.

2. **`JournalEntry` gains an approval lifecycle.** New statuses `PendingApproval` and `Rejected`
   are added (Draft → PendingApproval → Posted | Rejected; Posted → Reversed). A held journal is
   persisted in a non-posted status so it does not affect the GL. **All report/read-store queries
   change from `Status != Draft` to `Status ∈ {Posted, Reversed}`** so that PendingApproval and
   Rejected journals are excluded from balances (Reversed must remain included — its posted lines
   are offset by the reversal). `Post()` accepts a Draft or a PendingApproval entry.

3. **Guided Cash & Bank flows are thin, opinionated wrappers** that build a balanced journal via
   the posting-rule engine (accounts never hardcoded — Rule 27) and route it through the same
   approval path. Each gets its own `JournalSource` for a clear register/audit trail:
   Capital Contribution, Owner Drawing, Bank/Cash Transfer, Money Received, Money Spent,
   Loan Receipt, Loan Repayment.

4. **A journal register** (`GET /api/v1/journal-entries`, paged, filter by date/source/status/text)
   backs the Journals list; each row drills to the existing journal detail and can be reversed
   (reversal is itself the only correction — posted journals are immutable, BR-ACC-3).

5. **Default chart of accounts is extended (both entity models)** and the seeder is made
   **per-item idempotent** so the new accounts and posting rules backfill onto existing companies:

   | Code | Name | Type | Normal | For |
   |------|------|------|--------|-----|
   | 3000 | Owner's Capital (Modal Pemilik) | Equity | Credit | sole proprietor / CV |
   | 3100 | Additional Paid-in Capital (Agio Saham) | Equity | Credit | PT |
   | 3200 | Owner's Drawings (Prive) | Equity | Debit | sole proprietor / CV |
   | 3300 | Share Capital (Modal Saham) | Equity | Credit | PT |
   | 3400 | Dividends Declared | Equity | Debit | PT |
   | 3950 | Opening Balance Equity | Equity | Credit | onboarding |
   | 2500 | Bank Loan Payable | Liability | Credit | financing |
   | 2600 | Dividends Payable | Liability | Credit | PT |

   New posting-rule keys map the guided flows to their default accounts: `OwnerCapital→3000`,
   `OwnerDrawings→3200`, `ShareCapital→3300`, `LoanPayable→2500`, `OpeningBalanceEquity→3950`.
   Cash/Bank stays a per-account selector (as for payments), not a single default.

6. **Cash-flow classification.** The new loan accounts (2500/2600) and equity accounts are
   classified into the **Financing** section of the indirect cash-flow statement (best practice),
   not Operating.

## Options Considered

1. **Dedicated `ManualJournal` aggregate (like ExpenseVoucher), producing a GL journal only on
   approval** — cleanest separation, but a whole new entity/table/repository/migration to hold a
   transient pending state that `JournalEntry` can already represent. Rejected as over-built.
2. **Reuse `JournalEntry` with an approval lifecycle (chosen)** — one aggregate, the register and
   detail already exist; requires changing the read-store status filter to be explicit
   (`∈ {Posted, Reversed}`) and letting `Post()` accept a held entry. Minimal surface, keeps the GL
   the single source of truth.
3. **Surface the immediate-posting `PostJournalCommand` as-is (no approval)** — fastest, but leaves
   the most sensitive posting with no segregation of duties. Rejected against Rule 32.
4. **Equity model: sole-proprietor only / PT only / both** — chose **both** so no customer scenario
   (perorangan/CV or PT) is stuck; unused accounts are simply inactive/ignored.

## Consequences

- **Positive:** capital, drawings, transfers, loans, opening balances, and period-end adjustments
  become first-class, auditable, approvable operations; the GL remains the single source of truth;
  non-accountants get guided flows without touching debits/credits; existing companies gain the new
  accounts via idempotent backfill.
- **Negative / trade-offs:** `JournalEntry` gains lifecycle states it previously lacked; every
  read-store balance query must use the explicit `∈ {Posted, Reversed}` filter (a correctness-
  critical change — verified by trial-balance/reversal tests). A single `ManualJournal` approval
  document type means per-flow thresholds are not yet distinguishable (amount-based only).
- **Follow-ups:** update ACCOUNTING_DESIGN, POSTING_RULES, BUSINESS_RULES, API_SPEC, MODULES,
  STATUS, CHANGELOG. Consider per-flow approval document types and non-cash capital (capital in
  kind) later. Consider seeding a default "require approval for all manual journals" definition for
  businesses that want mandatory SoD out of the box.

## References

- [ADR-0030](../DECISIONS.md) — Expenses module & approval-routed atomic posting (the pattern reused).
- [ADR-0009](../DECISIONS.md) — double-entry & reversal-only corrections.
- [ADR-0007](../DECISIONS.md) — inter-module contracts / integration events (approval).
- [ACCOUNTING_DESIGN.md](../ACCOUNTING_DESIGN.md), [POSTING_RULES.md](../POSTING_RULES.md),
  [WORKFLOW_APPROVAL.md](../WORKFLOW_APPROVAL.md).
