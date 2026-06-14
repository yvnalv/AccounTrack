using Accountrack.Application.Abstractions.Integration;
using Accountrack.Infrastructure.Common.Persistence.Interceptors;
using Accountrack.Modules.Contracts.Events;
using Accountrack.Purchasing.Application;
using Accountrack.Purchasing.Application.Abstractions;
using Accountrack.Purchasing.Application.Features;
using Accountrack.Purchasing.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Accountrack.Purchasing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPurchasingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        services.TryAddScoped<AuditCaptureInterceptor>();
        services.TryAddScoped<AuditingSaveChangesInterceptor>();

        services.AddDbContext<PurchasingDbContext>((sp, options) =>
        {
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", PurchasingDbContext.Schema));
            options.AddInterceptors(
                sp.GetRequiredService<AuditCaptureInterceptor>(),
                sp.GetRequiredService<AuditingSaveChangesInterceptor>());
        });

        services.AddScoped<IPurchasingUnitOfWork>(sp => sp.GetRequiredService<PurchasingDbContext>());
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();

        // Advance PO status when its approval is decided.
        services.AddScoped<IIntegrationEventHandler<ApprovalDecided>, ApprovalDecidedConsumer>();

        var applicationAssembly = typeof(CreatePurchaseOrderCommand).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly));
        services.AddValidatorsFromAssembly(applicationAssembly);

        return services;
    }

    public static async Task InitializePurchasingModuleAsync(
        this IServiceProvider services, bool migrate, CancellationToken ct = default)
    {
        if (!migrate)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PurchasingDbContext>();
        await db.Database.MigrateAsync(ct);
    }
}
