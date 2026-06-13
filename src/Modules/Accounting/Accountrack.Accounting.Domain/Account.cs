using Accountrack.SharedKernel.Domain;

namespace Accountrack.Accounting.Domain;

/// <summary>
/// A general-ledger account in a company's chart of accounts (ADR-0008). Company-scoped: each
/// company has its own chart. Postings are allowed only to active, postable (leaf) accounts.
/// </summary>
public sealed class Account : TenantOwnedEntity, IAggregateRoot
{
    private Account() { }

    private Account(
        Guid id,
        string code,
        string name,
        AccountType type,
        Guid? parentAccountId,
        bool isControlAccount,
        ControlType controlType,
        bool isSystem) : base(id)
    {
        Code = code;
        Name = name;
        Type = type;
        NormalBalance = NormalBalanceFor(type);
        ParentAccountId = parentAccountId;
        IsControlAccount = isControlAccount;
        ControlType = controlType;
        IsSystem = isSystem;
        AllowPosting = true;
        IsActive = true;
    }

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public AccountType Type { get; private set; }
    public NormalBalance NormalBalance { get; private set; }
    public Guid? ParentAccountId { get; private set; }
    public bool IsControlAccount { get; private set; }
    public ControlType ControlType { get; private set; }

    /// <summary>System accounts are seeded and required by posting rules; they cannot be deleted.</summary>
    public bool IsSystem { get; private set; }

    /// <summary>Whether journals may post directly to this account (parents/rollups are false).</summary>
    public bool AllowPosting { get; private set; }

    public bool IsActive { get; private set; }

    public static Account Create(
        string code,
        string name,
        AccountType type,
        Guid? parentAccountId = null,
        bool isControlAccount = false,
        ControlType controlType = ControlType.None,
        bool isSystem = false) =>
        new(Guid.NewGuid(), code.Trim(), name.Trim(), type, parentAccountId, isControlAccount, controlType, isSystem);

    public static Account CreateWithId(
        Guid id,
        string code,
        string name,
        AccountType type,
        bool isControlAccount = false,
        ControlType controlType = ControlType.None,
        bool isSystem = false) =>
        new(id, code.Trim(), name.Trim(), type, null, isControlAccount, controlType, isSystem);

    public void Rename(string name) => Name = name.Trim();

    public void SetPostingAllowed(bool allow) => AllowPosting = allow;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public static NormalBalance NormalBalanceFor(AccountType type) => type switch
    {
        AccountType.Asset or AccountType.Expense => NormalBalance.Debit,
        _ => NormalBalance.Credit,
    };
}
