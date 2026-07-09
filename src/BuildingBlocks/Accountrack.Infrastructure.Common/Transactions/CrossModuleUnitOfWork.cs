using System.Data;
using Accountrack.Application.Abstractions.Idempotency;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Infrastructure.Common.Transactions;

/// <summary>
/// Default <see cref="ICrossModuleUnitOfWork"/>. Opens one transaction on the shared connection,
/// enlists every participating module context, runs the work, then persists all contexts and
/// commits — rolling everything back on failure or exception (INTEGRATION_EVENTS.md §2/§5).
/// When the in-flight command carries an idempotency key (ADR-0021), the key is written on this same
/// transaction before commit, so the business effects and the key are recorded atomically
/// (exactly-once): a crash can never leave effects posted without the key, and a concurrent replay
/// loses the unique-key race and is rolled back with the winner's result returned.
/// </summary>
internal sealed class CrossModuleUnitOfWork : ICrossModuleUnitOfWork
{
    private readonly ISharedDbConnection _shared;
    private readonly IEnumerable<ITransactionalDbContext> _contexts;
    private readonly IIdempotencyStore _idempotency;
    private readonly IIdempotencyScope _scope;

    public CrossModuleUnitOfWork(
        ISharedDbConnection shared,
        IEnumerable<ITransactionalDbContext> contexts,
        IIdempotencyStore idempotency,
        IIdempotencyScope scope)
    {
        _shared = shared;
        _contexts = contexts;
        _idempotency = idempotency;
        _scope = scope;
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

            // Persist the idempotency key in this same transaction (exactly-once). If a concurrent
            // request already claimed it, we lost the race: roll back our effects and return the id
            // the winner produced, so the replay is a no-op that yields the original result. The id is
            // the result itself for Result<Guid>, or its IdempotentId for a richer IIdempotentResult.
            if (_scope.Key is { } scopedKey && IdempotentResults.IdOf(result.Value) is Guid producedId)
            {
                var winningId = await _idempotency.WriteInTransactionAsync(
                    connection, transaction, scopedKey, producedId, ct);
                _scope.Written = true;

                if (winningId != producedId)
                {
                    await transaction.RollbackAsync(ct);
                    return (Result<T>)IdempotentResults.RebuildResult(typeof(T), winningId);
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
