using Microsoft.EntityFrameworkCore;

namespace Accountrack.Infrastructure.Common.Transactions;

/// <summary>
/// Marker exposed by a module's DbContext so the <see cref="CrossModuleUnitOfWork"/> can enlist it
/// in a shared transaction and persist it. Only contexts registered against the shared connection
/// (the modules that participate in atomic cross-module flows) expose this.
/// </summary>
public interface ITransactionalDbContext
{
    DbContext DbContext { get; }
}
