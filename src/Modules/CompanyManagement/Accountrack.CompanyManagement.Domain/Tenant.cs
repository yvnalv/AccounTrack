using Accountrack.SharedKernel.Domain;

namespace Accountrack.CompanyManagement.Domain;

public enum TenantStatus
{
    Active = 0,
    Suspended = 1,
}

/// <summary>
/// The subscription owner / organization — the root of tenancy (MULTI_TENANCY.md). A Tenant is
/// itself the boundary, so it is a plain entity (no TenantId of its own). Its <see cref="Entity.Id"/>
/// is the TenantId carried by all tenant-scoped data.
/// </summary>
public sealed class Tenant : Entity, IAggregateRoot
{
    private Tenant() { }

    private Tenant(Guid id, string name) : base(id)
    {
        Name = name;
        Status = TenantStatus.Active;
    }

    public string Name { get; private set; } = default!;

    public TenantStatus Status { get; private set; }

    public static Tenant Create(string name) => new(Guid.NewGuid(), name.Trim());

    /// <summary>Creates a tenant with a specific id (used by seeding / provisioning).</summary>
    public static Tenant CreateWithId(Guid id, string name) => new(id, name.Trim());

    public void Suspend() => Status = TenantStatus.Suspended;

    public void Reactivate() => Status = TenantStatus.Active;
}
