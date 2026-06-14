using Accountrack.SharedKernel.Results;

namespace Accountrack.Modules.Contracts.Accounting;

/// <summary>Dimension values supplied to refine account determination (docs/POSTING_RULES.md §1).</summary>
public sealed record PostingSelector(
    Guid? ProductCategoryId = null, Guid? WarehouseId = null, Guid? TaxCodeId = null, Guid? BankAccountId = null)
{
    public static readonly PostingSelector None = new();
}

/// <summary>
/// Well-known posting-rule keys that other modules reference when asking Accounting to resolve an
/// account. Values must match the Accounting module's rule catalog (docs/POSTING_RULES.md §2).
/// </summary>
public static class PostingKeys
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
}

/// <summary>
/// Public contract over the Accounting posting-rule engine (ADR-0024): resolves the GL account a
/// business event should post to, so other modules never hardcode account ids.
/// </summary>
public interface IPostingAccountResolver
{
    Task<Result<Guid>> ResolveAsync(string eventType, string ruleKey, PostingSelector selector, CancellationToken ct);
}
