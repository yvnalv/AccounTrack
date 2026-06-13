using Accountrack.SharedKernel.Domain;

namespace Accountrack.Identity.Domain;

/// <summary>
/// A persisted, rotating refresh token (ADR-0020, SECURITY.md §1). Only the hash is stored.
/// Tokens belong to a <see cref="FamilyId"/>; presenting a consumed token (reuse) is a theft
/// signal that revokes the whole family.
/// </summary>
public sealed class RefreshToken : TenantScopedEntity, IAggregateRoot
{
    private RefreshToken() { }

    public RefreshToken(Guid tenantId, Guid userId, string tokenHash, Guid familyId, DateTime expiresAtUtc)
    {
        TenantId = tenantId;
        UserId = userId;
        TokenHash = tokenHash;
        FamilyId = familyId;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = default!;

    public Guid FamilyId { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? ConsumedAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public bool IsActive(DateTime nowUtc) =>
        ConsumedAtUtc is null && RevokedAtUtc is null && ExpiresAtUtc > nowUtc;

    public bool WasConsumed => ConsumedAtUtc is not null;

    public void Consume(DateTime nowUtc) => ConsumedAtUtc ??= nowUtc;

    public void Revoke(DateTime nowUtc) => RevokedAtUtc ??= nowUtc;
}
