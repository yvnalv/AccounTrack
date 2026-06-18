using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Idempotency;
using Accountrack.SharedKernel.Results;
using MediatR;

namespace Accountrack.Application.Abstractions.Behaviors;

/// <summary>
/// Makes <see cref="IIdempotentCommand"/> commands replay-safe (ADR-0021). When an
/// <c>Idempotency-Key</c> is present, a key is derived per (tenant, command type, key); if that key
/// already produced a result the command is short-circuited and the original id is returned, so a
/// retried request never double-posts. Applies only to commands returning <c>Result&lt;Guid&gt;</c>.
/// </summary>
public sealed class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IIdempotencyContext _context;
    private readonly IIdempotencyStore _store;
    private readonly ITenantContext _tenant;

    public IdempotencyBehavior(IIdempotencyContext context, IIdempotencyStore store, ITenantContext tenant)
    {
        _context = context;
        _store = store;
        _tenant = tenant;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var key = _context.Key;
        if (request is not IIdempotentCommand || string.IsNullOrWhiteSpace(key) || typeof(TResponse) != typeof(Result<Guid>))
        {
            return await next();
        }

        var scopedKey = $"{_tenant.TenantId:N}:{typeof(TRequest).Name}:{key}";

        var existing = await _store.TryGetAsync(scopedKey, ct);
        if (existing is Guid priorResult)
        {
            return (TResponse)(object)Result.Success(priorResult);
        }

        var response = await next();
        if (response is Result<Guid> { IsSuccess: true } ok)
        {
            await _store.SaveAsync(scopedKey, ok.Value, ct);
        }

        return response;
    }
}
