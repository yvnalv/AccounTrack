using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Idempotency;
using Accountrack.SharedKernel.Results;
using MediatR;

namespace Accountrack.Application.Abstractions.Behaviors;

/// <summary>
/// Makes <see cref="IIdempotentCommand"/> commands replay-safe (ADR-0021). When an
/// <c>Idempotency-Key</c> is present, a key is derived per (tenant, command type, key); if that key
/// already produced a result the command is short-circuited and the original id is returned, so a
/// retried request never double-posts. Applies to commands returning <c>Result&lt;Guid&gt;</c> or
/// <c>Result&lt;T&gt;</c> where <c>T</c> is an <see cref="IIdempotentResult"/> (on a replay only the id
/// is known — the other fields of a richer result come back as their defaults).
///
/// The key is published on the <see cref="IIdempotencyScope"/> before the handler runs. Handlers that
/// commit through the cross-module transaction coordinator persist the key <em>inside</em> that
/// transaction (exactly-once) and mark the scope written; this behavior then skips its own write. For
/// any other handler the behavior records the key afterwards on a separate connection (at-least-once).
/// </summary>
public sealed class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IIdempotencyContext _context;
    private readonly IIdempotencyStore _store;
    private readonly ITenantContext _tenant;
    private readonly IIdempotencyScope _scope;

    public IdempotencyBehavior(
        IIdempotencyContext context, IIdempotencyStore store, ITenantContext tenant, IIdempotencyScope scope)
    {
        _context = context;
        _store = store;
        _tenant = tenant;
        _scope = scope;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var key = _context.Key;
        var innerType = ResultInnerType(typeof(TResponse));
        if (request is not IIdempotentCommand || string.IsNullOrWhiteSpace(key)
            || innerType is null || !IdempotentResults.IsAddressable(innerType))
        {
            return await next();
        }

        var scopedKey = $"{_tenant.TenantId:N}:{typeof(TRequest).Name}:{key}";

        var existing = await _store.TryGetAsync(scopedKey, ct);
        if (existing is Guid priorResult)
        {
            return (TResponse)IdempotentResults.RebuildResult(innerType, priorResult);
        }

        _scope.Begin(scopedKey);
        try
        {
            var response = await next();

            // The coordinator records the key atomically with the effects and sets Written; only fall
            // back to a separate-connection write for handlers that did not run inside that transaction.
            if (!_scope.Written && response is Result { IsSuccess: true }
                && IdempotentResults.IdOf(IdempotentResults.SuccessValueOf(response!)) is Guid producedId)
            {
                await _store.SaveAsync(scopedKey, producedId, ct);
            }

            return response;
        }
        finally
        {
            _scope.Clear();
        }
    }

    /// <summary>The <c>T</c> of a <c>Result&lt;T&gt;</c> response, or null for any other response type.</summary>
    private static Type? ResultInnerType(Type responseType) =>
        responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>)
            ? responseType.GetGenericArguments()[0]
            : null;
}
