using Accountrack.SharedKernel.Domain;

namespace Accountrack.CompanyManagement.Domain;

/// <summary>
/// A typed key/value configuration entry scoped to a company (e.g.
/// "Inventory.AllowNegativeStock"). Company-owned (TenantId + CompanyId).
/// </summary>
public sealed class CompanySetting : TenantOwnedEntity, IAggregateRoot
{
    private CompanySetting() { }

    public CompanySetting(string key, string value)
    {
        Key = key.Trim();
        Value = value;
    }

    public string Key { get; private set; } = default!;

    public string Value { get; private set; } = default!;

    public void SetValue(string value) => Value = value;
}
