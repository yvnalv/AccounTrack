using Accountrack.SharedKernel.Domain;

namespace Accountrack.Expenses.Domain;

/// <summary>
/// An operating-expense category (electricity, transport, rent, supplies, salaries-as-cash, …).
/// Maps to an expense GL account via the posting-rule engine: <see cref="PostingRuleKey"/> is the
/// key the engine resolves (ADR-0024/0030), so accounts are configuration, never hardcoded.
/// </summary>
public sealed class ExpenseCategory : TenantOwnedEntity, IAggregateRoot, IHasCode
{
    private ExpenseCategory() { }

    private ExpenseCategory(Guid id, string code, string name, string postingRuleKey) : base(id)
    {
        Code = code;
        Name = name;
        PostingRuleKey = postingRuleKey;
        IsActive = true;
    }

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;

    /// <summary>The posting-rule key resolving this category to its expense GL account.</summary>
    public string PostingRuleKey { get; private set; } = default!;

    public bool IsActive { get; private set; }

    public static ExpenseCategory Create(string code, string name, string postingRuleKey) =>
        new(Guid.NewGuid(), code.Trim().ToUpperInvariant(), name.Trim(), postingRuleKey.Trim());

    public void Update(string name, string postingRuleKey)
    {
        Name = name.Trim();
        PostingRuleKey = postingRuleKey.Trim();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
