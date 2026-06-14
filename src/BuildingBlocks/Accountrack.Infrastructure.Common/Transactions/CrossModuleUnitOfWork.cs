using System.Data;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Infrastructure.Common.Transactions;

/// <summary>
/// Default <see cref="ICrossModuleUnitOfWork"/>. Opens one transaction on the shared connection,
/// enlists every participating module context, runs the work, then persists all contexts and
/// commits — rolling everything back on failure or exception (INTEGRATION_EVENTS.md §2/§5).
/// </summary>
internal sealed class CrossModuleUnitOfWork : ICrossModuleUnitOfWork
{
    private readonly ISharedDbConnection _shared;
    private readonly IEnumerable<ITransactionalDbContext> _contexts;

    public CrossModuleUnitOfWork(ISharedDbConnection shared, IEnumerable<ITransactionalDbContext> contexts)
    {
        _shared = shared;
        _contexts = contexts;
    }

    public async Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken ct)
    {
        var connection = _shared.Connection;
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        var enlisted = _contexts.Select(c => c.DbContext).ToList();

        await using var transaction = await connection.BeginTransactionAsync(ct);
        foreach (var db in enlisted)
        {
            db.Database.UseTransaction(transaction);
        }

        try
        {
            var result = await work(ct);
            if (result.IsFailure)
            {
                await transaction.RollbackAsync(ct);
                return result;
            }

            foreach (var db in enlisted)
            {
                if (db.ChangeTracker.HasChanges())
                {
                    await db.SaveChangesAsync(ct);
                }
            }

            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
        finally
        {
            foreach (var db in enlisted)
            {
                db.Database.UseTransaction(null);
            }
        }
    }
}
