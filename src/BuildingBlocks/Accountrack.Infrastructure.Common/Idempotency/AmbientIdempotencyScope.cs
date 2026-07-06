using Accountrack.Application.Abstractions.Idempotency;

namespace Accountrack.Infrastructure.Common.Idempotency;

/// <summary>
/// Scoped (per-request) <see cref="IIdempotencyScope"/> — a mutable holder the IdempotencyBehavior and
/// the cross-module transaction coordinator share for the duration of one command (ADR-0021).
/// </summary>
public sealed class AmbientIdempotencyScope : IIdempotencyScope
{
    public string? Key { get; private set; }

    public bool Written { get; set; }

    public void Begin(string scopedKey)
    {
        Key = scopedKey;
        Written = false;
    }

    public void Clear()
    {
        Key = null;
        Written = false;
    }
}
