namespace Accountrack.SharedKernel.Auditing;

public enum AuditAction
{
    Insert = 0,
    Update = 1,
    Delete = 2,
}

/// <summary>
/// An immutable, append-only record of a change to a business entity (ADR-0006, SECURITY.md §4):
/// who changed which entity, when, and the before/after values. Captured automatically by the
/// audit interceptor and never updated or deleted. A cross-cutting primitive shared by all modules.
/// </summary>
public sealed class AuditEntry
{
    private AuditEntry() { }

    public AuditEntry(
        Guid tenantId,
        Guid? companyId,
        string entityType,
        string entityId,
        AuditAction action,
        string changesJson,
        Guid userId,
        DateTime timestampUtc,
        string? correlationId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CompanyId = companyId;
        EntityType = entityType;
        EntityId = entityId;
        Action = action;
        ChangesJson = changesJson;
        UserId = userId;
        TimestampUtc = timestampUtc;
        CorrelationId = correlationId;
    }

    public Guid Id { get; private set; }

    /// <summary>Owning tenant (Guid.Empty for system/seed operations without a tenant context).</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Owning company when the changed entity is company-scoped; otherwise null.</summary>
    public Guid? CompanyId { get; private set; }

    public string EntityType { get; private set; } = default!;

    public string EntityId { get; private set; } = default!;

    public AuditAction Action { get; private set; }

    /// <summary>JSON document of changed values (before/after for updates, snapshot otherwise).</summary>
    public string ChangesJson { get; private set; } = default!;

    public Guid UserId { get; private set; }

    public DateTime TimestampUtc { get; private set; }

    public string? CorrelationId { get; private set; }
}
