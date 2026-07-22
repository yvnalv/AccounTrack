using Accountrack.SharedKernel.Domain;

namespace Accountrack.Accounting.Domain;

/// <summary>
/// Well-known posting-rule keys (the resolved accounting purpose). Account determination maps a
/// business event + these keys to GL accounts (docs/POSTING_RULES.md §2). Stored as strings so the
/// catalog can grow without a schema change.
/// </summary>
public static class PostingRuleKeys
{
    public const string ArControl = "ARControl";
    public const string ApControl = "APControl";
    public const string Inventory = "Inventory";
    public const string GrIrClearing = "GRIRClearing";
    public const string Revenue = "Revenue";
    public const string Cogs = "COGS";
    public const string VatOutput = "VATOutput";
    public const string VatInput = "VATInput";
    public const string CashBank = "CashBank";
    public const string InventoryVariance = "InventoryVariance";
    public const string Rounding = "Rounding";
    public const string CustomerAdvance = "CustomerAdvance";
    public const string SupplierAdvance = "SupplierAdvance";
    public const string RetainedEarnings = "RetainedEarnings";

    // Equity & financing (ADR-0040) — resolved by the guided Cash & Bank flows. Not in Required:
    // a company that never records capital/loans does not need them, and they backfill idempotently.
    public const string OwnerCapital = "OwnerCapital";
    public const string OwnerDrawings = "OwnerDrawings";
    public const string ShareCapital = "ShareCapital";
    public const string LoanPayable = "LoanPayable";
    public const string OpeningBalanceEquity = "OpeningBalanceEquity";

    /// <summary>Keys that must resolve to a control account, with the required control type.</summary>
    public static readonly IReadOnlyDictionary<string, ControlType> ControlKeys =
        new Dictionary<string, ControlType>
        {
            [ArControl] = ControlType.AccountsReceivable,
            [ApControl] = ControlType.AccountsPayable,
            [Inventory] = ControlType.Inventory,
        };

    /// <summary>The keys a company must have configured before it can transact (health check).</summary>
    public static readonly IReadOnlyList<string> Required = new[]
    {
        ArControl, ApControl, Inventory, GrIrClearing, Revenue, Cogs,
        VatOutput, VatInput, InventoryVariance, Rounding, CustomerAdvance,
        SupplierAdvance, RetainedEarnings,
    };
}

/// <summary>
/// A configurable account-determination rule (ADR-0024): for a company, an event + purpose
/// (<see cref="RuleKey"/>) — optionally refined by dimension selectors — resolves to a GL account.
/// Most-specific match wins; a company-wide default (<see cref="AnyEvent"/>, no selectors) is the
/// fallback. See docs/POSTING_RULES.md.
/// </summary>
public sealed class PostingRule : TenantOwnedEntity, IAggregateRoot
{
    /// <summary>EventType value meaning "applies to any event" (the default rule for a key).</summary>
    public const string AnyEvent = "*";

    private PostingRule() { }

    private PostingRule(
        Guid id, string eventType, string ruleKey, Guid accountId,
        Guid? productCategoryId, Guid? warehouseId, Guid? taxCodeId, Guid? bankAccountId) : base(id)
    {
        EventType = eventType;
        RuleKey = ruleKey;
        AccountId = accountId;
        ProductCategoryId = productCategoryId;
        WarehouseId = warehouseId;
        TaxCodeId = taxCodeId;
        BankAccountId = bankAccountId;
        IsActive = true;
    }

    public string EventType { get; private set; } = default!;
    public string RuleKey { get; private set; } = default!;
    public Guid AccountId { get; private set; }

    // Optional dimension selectors that refine a rule (null = wildcard / matches anything).
    public Guid? ProductCategoryId { get; private set; }
    public Guid? WarehouseId { get; private set; }
    public Guid? TaxCodeId { get; private set; }
    public Guid? BankAccountId { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>How specific this rule is — higher beats lower during resolution.</summary>
    public int Specificity =>
        (EventType != AnyEvent ? 1 : 0)
        + (ProductCategoryId.HasValue ? 1 : 0)
        + (WarehouseId.HasValue ? 1 : 0)
        + (TaxCodeId.HasValue ? 1 : 0)
        + (BankAccountId.HasValue ? 1 : 0);

    /// <summary>The company-wide default rule for a key (no event, no selectors).</summary>
    public static PostingRule CreateDefault(string ruleKey, Guid accountId) =>
        new(Guid.NewGuid(), AnyEvent, ruleKey, accountId, null, null, null, null);

    public static PostingRule Create(
        string eventType, string ruleKey, Guid accountId,
        Guid? productCategoryId = null, Guid? warehouseId = null, Guid? taxCodeId = null, Guid? bankAccountId = null) =>
        new(Guid.NewGuid(), eventType.Trim(), ruleKey.Trim(), accountId,
            productCategoryId, warehouseId, taxCodeId, bankAccountId);

    public void Repoint(Guid accountId) => AccountId = accountId;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Whether this rule applies to the requested event + selector. Each non-null dimension on the
    /// rule must equal the requested value; null dimensions are wildcards. EventType matches the
    /// request or the wildcard <see cref="AnyEvent"/>.
    /// </summary>
    public bool Matches(string eventType, PostingSelector selector) =>
        IsActive
        && (EventType == AnyEvent || string.Equals(EventType, eventType, StringComparison.OrdinalIgnoreCase))
        && (!ProductCategoryId.HasValue || ProductCategoryId == selector.ProductCategoryId)
        && (!WarehouseId.HasValue || WarehouseId == selector.WarehouseId)
        && (!TaxCodeId.HasValue || TaxCodeId == selector.TaxCodeId)
        && (!BankAccountId.HasValue || BankAccountId == selector.BankAccountId);
}

/// <summary>Dimension values supplied at resolution time to refine account determination.</summary>
public sealed record PostingSelector(
    Guid? ProductCategoryId = null,
    Guid? WarehouseId = null,
    Guid? TaxCodeId = null,
    Guid? BankAccountId = null)
{
    public static readonly PostingSelector None = new();
}
