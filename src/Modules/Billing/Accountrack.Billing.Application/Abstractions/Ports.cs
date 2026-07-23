using Accountrack.Billing.Domain;

namespace Accountrack.Billing.Application.Abstractions;

public interface IPlanRepository
{
    void Add(Plan plan);
    Task<Plan?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Plan?> GetByCodeAsync(string code, CancellationToken ct);

    /// <summary>Active, public plans for the pricing page, ordered for display.</summary>
    Task<IReadOnlyList<Plan>> ListPublicAsync(CancellationToken ct);
}

public interface ISubscriptionRepository
{
    void Add(Subscription subscription);

    /// <summary>The current tenant's subscription (tenant resolved by the global query filter), or null.</summary>
    Task<Subscription?> GetForCurrentTenantAsync(CancellationToken ct);

    /// <summary>
    /// A subscription by id <b>bypassing the tenant query filter</b>. Used only by the anonymous,
    /// signature-verified payment webhook, which has no ambient tenant (a reviewed admin path, Rule 33).
    /// </summary>
    Task<Subscription?> GetByIdIgnoringFiltersAsync(Guid id, CancellationToken ct);
}

public interface IBillingInvoiceRepository
{
    void Add(BillingInvoice invoice);

    /// <summary>Count of this tenant's invoices, for a per-tenant document number.</summary>
    Task<int> CountForCurrentTenantAsync(CancellationToken ct);

    /// <summary>
    /// A billing invoice by its gateway invoice id, <b>bypassing the tenant query filter</b> — used by
    /// the anonymous, signature-verified payment webhook (no ambient tenant; reviewed admin path, Rule 33).
    /// </summary>
    Task<BillingInvoice?> GetByGatewayInvoiceIdIgnoringFiltersAsync(string gatewayInvoiceId, CancellationToken ct);
}

public interface IBillingUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
