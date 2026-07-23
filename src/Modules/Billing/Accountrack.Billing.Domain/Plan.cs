using Accountrack.SharedKernel.Domain;

namespace Accountrack.Billing.Domain;

/// <summary>
/// A subscription plan Accountrack sells to tenants (SUBSCRIPTION_BILLING.md §2/§9, ADR-0039). A plan is
/// a <b>global catalog</b> row — it is not tenant-owned, so it carries no tenant filter and is readable by
/// every tenant (the public pricing page). Money is stored as integer <b>minor units</b> in IDR
/// (scale 0 — one minor unit = one rupiah) plus an explicit currency code (CLAUDE.md Non-Negotiable 31).
/// Illustrative prices are seeded for dev; real prices are a pre-launch pricing exercise (§13).
/// </summary>
public sealed class Plan : Entity, IAggregateRoot
{
    private Plan() { }

    private Plan(
        string code, string name, BillingInterval interval, long basePriceMinor, int includedSeats,
        long perSeatPriceMinor, int? maxCompanies, string currency, string featuresJson, bool isPublic)
    {
        Code = code;
        Name = name;
        Interval = interval;
        BasePriceMinor = basePriceMinor;
        IncludedSeats = includedSeats;
        PerSeatPriceMinor = perSeatPriceMinor;
        MaxCompanies = maxCompanies;
        Currency = currency;
        FeaturesJson = featuresJson;
        IsActive = true;
        IsPublic = isPublic;
    }

    /// <summary>Stable machine code (e.g. "STARTER-MONTHLY"). Unique in the catalog.</summary>
    public string Code { get; private set; } = default!;

    public string Name { get; private set; } = default!;
    public BillingInterval Interval { get; private set; }

    /// <summary>Base price per interval, in IDR minor units.</summary>
    public long BasePriceMinor { get; private set; }

    /// <summary>User seats bundled in the base price.</summary>
    public int IncludedSeats { get; private set; }

    /// <summary>Price per extra active user beyond <see cref="IncludedSeats"/>, in IDR minor units.</summary>
    public long PerSeatPriceMinor { get; private set; }

    /// <summary>Maximum companies the plan allows; <c>null</c> means unlimited.</summary>
    public int? MaxCompanies { get; private set; }

    public string Currency { get; private set; } = default!;

    /// <summary>Feature flags encoded as JSON (approvals, import/export, advanced reports, API…).</summary>
    public string FeaturesJson { get; private set; } = "{}";

    /// <summary>Whether the plan is still sold (soft-deactivate rather than delete).</summary>
    public bool IsActive { get; private set; }

    /// <summary>Whether the plan is shown on the public pricing page.</summary>
    public bool IsPublic { get; private set; }

    public static Plan Create(
        string code, string name, BillingInterval interval, long basePriceMinor, int includedSeats,
        long perSeatPriceMinor, int? maxCompanies, string currency, string featuresJson, bool isPublic = true)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Plan code is required.", nameof(code));
        }

        if (basePriceMinor < 0 || perSeatPriceMinor < 0)
        {
            throw new ArgumentException("Plan prices cannot be negative.");
        }

        if (includedSeats < 0)
        {
            throw new ArgumentException("Included seats cannot be negative.", nameof(includedSeats));
        }

        return new Plan(
            code.Trim().ToUpperInvariant(), name.Trim(), interval, basePriceMinor, includedSeats,
            perSeatPriceMinor, maxCompanies, currency.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(featuresJson) ? "{}" : featuresJson, isPublic);
    }

    public void Deactivate() => IsActive = false;
}
