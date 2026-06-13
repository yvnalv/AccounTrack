namespace Accountrack.SharedKernel.Domain;

/// <summary>
/// Base class for entities owned by a tenant but not by a single company (e.g. User, Role).
/// <see cref="TenantId"/> is stamped from the ambient tenant context and enforced by a
/// tenant-only global query filter (docs/MULTI_TENANCY.md).
/// </summary>
public abstract class TenantScopedEntity : Entity, ITenantScoped
{
    protected TenantScopedEntity() { }

    protected TenantScopedEntity(Guid id) : base(id) { }

    public Guid TenantId { get; set; }
}
