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
}

public interface IBillingUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
