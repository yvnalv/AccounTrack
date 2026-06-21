namespace Accountrack.Modules.Contracts.Accounting;

/// <summary>
/// Read-side contract for other modules to reconcile their subsidiary ledgers to a GL control
/// account (e.g. the Inventory valuation report comparing ledger value to the Inventory account).
/// Returns the signed (debit − credit) balance, so an asset control account is normally positive.
/// </summary>
public interface IGeneralLedgerBalances
{
    /// <summary>Current signed balance of the company's Inventory control account (ControlType.Inventory).</summary>
    Task<decimal> GetInventoryControlBalanceAsync(CancellationToken ct);
}
