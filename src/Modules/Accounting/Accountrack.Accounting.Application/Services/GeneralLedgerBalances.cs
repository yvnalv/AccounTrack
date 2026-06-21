using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Domain;
using Accountrack.Modules.Contracts.Accounting;

namespace Accountrack.Accounting.Application.Services;

/// <summary>
/// Exposes GL control-account balances to other modules via <see cref="IGeneralLedgerBalances"/>
/// (read-only). Used by the Inventory valuation report to reconcile ledger value to the GL.
/// </summary>
public sealed class GeneralLedgerBalances : IGeneralLedgerBalances
{
    private readonly IAccountRepository _accounts;
    private readonly IAccountingReadStore _store;

    public GeneralLedgerBalances(IAccountRepository accounts, IAccountingReadStore store)
    {
        _accounts = accounts;
        _store = store;
    }

    public async Task<decimal> GetInventoryControlBalanceAsync(CancellationToken ct)
    {
        var inventory = (await _accounts.ListAsync(ct))
            .FirstOrDefault(a => a.ControlType == ControlType.Inventory);
        if (inventory is null)
        {
            return 0m;
        }

        var rows = await _store.GetTrialBalanceAsync(null, null, ct);
        var row = rows.FirstOrDefault(r => r.AccountCode == inventory.Code);
        return row is null ? 0m : row.Debit - row.Credit;
    }
}
