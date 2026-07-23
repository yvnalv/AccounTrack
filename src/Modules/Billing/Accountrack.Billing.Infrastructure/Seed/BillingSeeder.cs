using Accountrack.Billing.Domain;
using Accountrack.Billing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Billing.Infrastructure.Seed;

/// <summary>
/// Seeds the global plan catalog (SUBSCRIPTION_BILLING.md §2/§13). Plans are NOT tenant-owned — they are a
/// shared catalog readable by every tenant — so no tenant is stamped. Prices are <b>illustrative
/// placeholders</b> in IDR minor units (scale 0 = rupiah); real prices are a pre-launch pricing exercise
/// (open item §13). Annual = monthly × 10 ("2 months free"). Idempotent by plan code.
/// </summary>
public static class BillingSeeder
{
    private const string Currency = "IDR";

    private static readonly (string Code, string Name, BillingInterval Interval, long Base, int Seats,
        long PerSeat, int? MaxCompanies, string Features)[] DefaultPlans =
    {
        ("STARTER-MONTHLY", "Starter (Monthly)", BillingInterval.Monthly, 149_000, 2, 59_000, 1,
            "{\"approvals\":false,\"multiWarehouse\":false,\"exports\":false,\"api\":false}"),
        ("STARTER-ANNUAL", "Starter (Annual)", BillingInterval.Annual, 1_490_000, 2, 590_000, 1,
            "{\"approvals\":false,\"multiWarehouse\":false,\"exports\":false,\"api\":false}"),
        ("BUSINESS-MONTHLY", "Business (Monthly)", BillingInterval.Monthly, 499_000, 8, 49_000, 3,
            "{\"approvals\":true,\"multiWarehouse\":true,\"exports\":true,\"api\":false}"),
        ("BUSINESS-ANNUAL", "Business (Annual)", BillingInterval.Annual, 4_990_000, 8, 490_000, 3,
            "{\"approvals\":true,\"multiWarehouse\":true,\"exports\":true,\"api\":false}"),
        ("ENTERPRISE-MONTHLY", "Enterprise (Monthly)", BillingInterval.Monthly, 1_490_000, 25, 39_000, null,
            "{\"approvals\":true,\"multiWarehouse\":true,\"exports\":true,\"api\":true,\"prioritySupport\":true}"),
        ("ENTERPRISE-ANNUAL", "Enterprise (Annual)", BillingInterval.Annual, 14_900_000, 25, 390_000, null,
            "{\"approvals\":true,\"multiWarehouse\":true,\"exports\":true,\"api\":true,\"prioritySupport\":true}"),
    };

    public static async Task SeedAsync(BillingDbContext db, CancellationToken ct = default)
    {
        foreach (var (code, name, interval, basePrice, seats, perSeat, maxCompanies, features) in DefaultPlans)
        {
            var exists = await db.Plans.AnyAsync(p => p.Code == code, ct);
            if (exists)
            {
                continue;
            }

            db.Plans.Add(Plan.Create(code, name, interval, basePrice, seats, perSeat, maxCompanies,
                Currency, features));
        }

        await db.SaveChangesAsync(ct);
    }
}
