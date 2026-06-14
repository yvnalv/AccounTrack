using Accountrack.SharedKernel.Results;

namespace Accountrack.Modules.Contracts.Transactions;

/// <summary>
/// Runs a unit of work that spans more than one module's data, atomically (INTEGRATION_EVENTS.md §2).
/// The work delegate calls the participating modules' synchronous contracts (which are save-less);
/// the coordinator opens one shared database transaction, persists every enlisted module context,
/// and commits — or rolls everything back if the work fails or throws. Used where a failure would
/// otherwise leave the GL or inventory ledger inconsistent (e.g. Goods Receipt: stock + journal).
/// </summary>
public interface ICrossModuleUnitOfWork
{
    Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken ct);
}
