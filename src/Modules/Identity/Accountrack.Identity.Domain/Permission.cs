using Accountrack.SharedKernel.Domain;

namespace Accountrack.Identity.Domain;

/// <summary>
/// A named, assignable capability (e.g. "Sales.Post"). The catalog is global and seeded;
/// permissions are never hardcoded into authorization checks (ADR-0019, SECURITY.md §2).
/// </summary>
public sealed class Permission : Entity, IAggregateRoot
{
    private Permission() { }

    public Permission(string code, string name, string? description = null)
    {
        Code = code;
        Name = name;
        Description = description;
        IsSystem = true;
    }

    /// <summary>Stable code in the form "Module.Action".</summary>
    public string Code { get; private set; } = default!;

    public string Name { get; private set; } = default!;

    public string? Description { get; private set; }

    /// <summary>System permissions are seeded and cannot be deleted.</summary>
    public bool IsSystem { get; private set; }
}
