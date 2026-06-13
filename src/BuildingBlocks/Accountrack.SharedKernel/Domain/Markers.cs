namespace Accountrack.SharedKernel.Domain;

/// <summary>Marks an aggregate root — the only entities repositories load/save directly.</summary>
public interface IAggregateRoot;

/// <summary>An entity that exposes buffered domain events for dispatch on save.</summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}

/// <summary>Standard audit fields stamped automatically by the persistence interceptor.</summary>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    Guid CreatedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
    Guid? UpdatedBy { get; set; }
}

/// <summary>Soft-delete fields (ADR-0006). Business data is never physically deleted.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    Guid? DeletedBy { get; set; }
}

/// <summary>
/// Belongs to a tenant (ADR-0004) but is not scoped to a single company — e.g. Users and Roles.
/// Enforced by a tenant-only global query filter.
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; set; }
}

/// <summary>
/// Belongs to a tenant AND a company (the majority of business data). Enforced by a
/// tenant + company global query filter.
/// </summary>
public interface ITenantOwned : ITenantScoped
{
    Guid CompanyId { get; set; }
}
