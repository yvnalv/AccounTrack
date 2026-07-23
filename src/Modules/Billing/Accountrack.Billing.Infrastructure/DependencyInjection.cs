using Accountrack.Billing.Application.Abstractions;
using Accountrack.Billing.Application.Features;
using Accountrack.Billing.Infrastructure.Persistence;
using Accountrack.Billing.Infrastructure.Seed;
using Accountrack.Infrastructure.Common.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Accountrack.Billing.Infrastructure;

/// <summary>
/// Wires the Billing module (SUBSCRIPTION_BILLING.md, ADR-0039). A standalone context on its own
/// connection — Billing is our commercial ledger and must never enlist in the cross-module GL
/// transaction (§5). No <c>ITransactionalDbContext</c>/<c>ISharedDbConnection</c> registration.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddBillingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        services.TryAddScoped<AuditCaptureInterceptor>();
        services.TryAddScoped<AuditingSaveChangesInterceptor>();

        services.AddDbContext<BillingDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", BillingDbContext.Schema));
            options.AddInterceptors(
                sp.GetRequiredService<AuditCaptureInterceptor>(),
                sp.GetRequiredService<AuditingSaveChangesInterceptor>());
        });

        services.AddScoped<IBillingUnitOfWork>(sp => sp.GetRequiredService<BillingDbContext>());
        services.AddScoped<IPlanRepository, PlanRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();

        // Resolves what the calling tenant's plan/subscription permits (SUBSCRIPTION_BILLING.md §7).
        // Consumed by the host's enforcement middleware and by modules honouring plan limits.
        services.AddScoped<Accountrack.Modules.Contracts.Billing.ITenantEntitlements,
            Accountrack.Billing.Application.Features.EntitlementResolver>();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetPlansQuery).Assembly));

        return services;
    }

    public static async Task InitializeBillingModuleAsync(
        this IServiceProvider services, bool migrate, bool seedDev, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        if (migrate)
        {
            await db.Database.MigrateAsync(ct);
        }

        if (seedDev)
        {
            await BillingSeeder.SeedAsync(db, ct);
        }
    }
}
