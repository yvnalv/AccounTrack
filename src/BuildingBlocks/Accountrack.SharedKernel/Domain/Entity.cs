namespace Accountrack.SharedKernel.Domain;

/// <summary>
/// Base class for all business entities. Provides a GUID identity (ADR-0005),
/// standard audit fields, soft-delete (ADR-0006), an optimistic-concurrency token,
/// and a domain-event buffer.
/// Tenancy fields live on <see cref="TenantOwnedEntity"/>.
/// </summary>
public abstract class Entity : IAuditable, ISoftDeletable, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();

    protected Entity() { }

    protected Entity(Guid id) => Id = id;

    public Guid Id { get; protected set; } = Guid.NewGuid();

    // Audit (IAuditable) — stamped by the persistence interceptor, not by application code.
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    // Soft delete (ISoftDeletable).
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Optimistic-concurrency token (ADR-0021). Mapped to a SQL rowversion.</summary>
    public byte[]? RowVersion { get; set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    public override bool Equals(object? obj) =>
        obj is Entity other && GetType() == other.GetType() && Id == other.Id && Id != Guid.Empty;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
