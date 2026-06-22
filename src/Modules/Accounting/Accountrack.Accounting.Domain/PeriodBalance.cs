using Accountrack.SharedKernel.Domain;

namespace Accountrack.Accounting.Domain;

/// <summary>
/// A rebuildable snapshot of one account's cumulative balance as of a fiscal period's end, captured
/// when the period is closed (ADR-0022). It lets reports show opening balances and month-end positions
/// without re-summing the whole ledger, and is always re-derivable from the GL (rebuild on demand).
/// Account code/name are denormalized so the snapshot stays a stable point-in-time record.
/// </summary>
public sealed class PeriodBalance : TenantOwnedEntity
{
    private PeriodBalance() { }

    public PeriodBalance(Guid fiscalPeriodId, Guid accountId, string accountCode, string accountName, decimal debit, decimal credit)
    {
        FiscalPeriodId = fiscalPeriodId;
        AccountId = accountId;
        AccountCode = accountCode;
        AccountName = accountName;
        Debit = debit;
        Credit = credit;
    }

    public Guid FiscalPeriodId { get; private set; }
    public Guid AccountId { get; private set; }
    public string AccountCode { get; private set; } = default!;
    public string AccountName { get; private set; } = default!;
    public decimal Debit { get; private set; }
    public decimal Credit { get; private set; }
}
