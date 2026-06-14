using Accountrack.SharedKernel.Results;

namespace Accountrack.Accounting.Domain;

public static class AccountingErrors
{
    public static readonly Error AccountNotFound =
        Error.NotFound("ACCOUNTING.ACCOUNT_NOT_FOUND", "Account not found.");

    public static readonly Error AccountCodeExists =
        Error.Conflict("ACCOUNTING.ACCOUNT_CODE_EXISTS", "An account with this code already exists.");

    public static Error AccountNotPostable(string code) =>
        Error.BusinessRule("BR-ACC-5", $"Account {code} is inactive or not postable.", "ACCOUNTING.ACCOUNT_NOT_POSTABLE");

    public static readonly Error Unbalanced =
        Error.BusinessRule("BR-ACC-1", "Journal debits and credits are not equal.", "ACCOUNTING.UNBALANCED");

    public static readonly Error TooFewLines =
        Error.BusinessRule("BR-ACC-2", "A journal must have at least two lines.", "ACCOUNTING.TOO_FEW_LINES");

    public static readonly Error NoOpenPeriod =
        Error.BusinessRule("BR-ACC-4", "No open fiscal period exists for the journal date.", "ACCOUNTING.NO_OPEN_PERIOD");

    public static readonly Error PeriodClosed =
        Error.BusinessRule("BR-ACC-4", "The fiscal period for this date is closed or locked.", "ACCOUNTING.PERIOD_CLOSED");

    public static readonly Error JournalNotFound =
        Error.NotFound("ACCOUNTING.JOURNAL_NOT_FOUND", "Journal entry not found.");

    public static readonly Error JournalNotPosted =
        Error.Conflict("ACCOUNTING.JOURNAL_NOT_POSTED", "Only a posted journal can be reversed.");

    public static readonly Error AlreadyReversed =
        Error.Conflict("ACCOUNTING.ALREADY_REVERSED", "This journal has already been reversed.");

    public static readonly Error FiscalYearExists =
        Error.Conflict("ACCOUNTING.FISCAL_YEAR_EXISTS", "This fiscal year already exists for the company.");

    public static readonly Error PeriodNotFound =
        Error.NotFound("ACCOUNTING.PERIOD_NOT_FOUND", "Fiscal period not found.");

    public static readonly Error OpenItemNotFound =
        Error.NotFound("ACCOUNTING.OPEN_ITEM_NOT_FOUND", "Subledger open item not found.");

    public static readonly Error AllocationExceedsOutstanding =
        Error.BusinessRule("BR-ACC-7", "Allocation exceeds the open item's outstanding amount.", "ACCOUNTING.ALLOCATION_EXCEEDS_OUTSTANDING");

    public static readonly Error OpenItemSettled =
        Error.Conflict("ACCOUNTING.OPEN_ITEM_SETTLED", "This open item is already settled.");

    public static Error PostingRuleUnresolved(string eventType, string ruleKey) =>
        Error.BusinessRule(
            "BR-ACC-6",
            $"No posting rule resolves account '{ruleKey}' for event '{eventType}'. Configure the chart of accounts / posting rules.",
            "ACCOUNTING.POSTING_RULE_UNRESOLVED");
}
