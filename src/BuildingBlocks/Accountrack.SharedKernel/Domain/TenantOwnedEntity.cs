namespace Accountrack.SharedKernel.Domain;

/// <summary>
/// Base class for every tenant-owned business entity (the vast majority).
/// Carries <see cref="TenantId"/> and <see cref="CompanyId"/> which are stamped from the
/// ambient tenant context and enforced by global query filters (docs/MULTI_TENANCY.md).
/// Application code must never set these manually.
/// </summary>
public abstract class TenantOwnedEntity : TenantScopedEntity, ITenantOwned
{
    protected TenantOwnedEntity() { }

    protected TenantOwnedEntity(Guid id) : base(id) { }

    public Guid CompanyId { get; set; }
}
