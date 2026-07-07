using Accountrack.Infrastructure.Common.Persistence.Interceptors;
using Accountrack.Infrastructure.Common.Transactions;
using Accountrack.Inventory.Application.Abstractions;
using Accountrack.Inventory.Application.Features;
using Accountrack.Inventory.Application.Services;
using Accountrack.Inventory.Infrastructure.Persistence;
using Accountrack.Modules.Contracts.Inventory;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Accountrack.Inventory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInventoryModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddScoped<AuditCaptureInterceptor>();
        services.TryAddScoped<AuditingSaveChangesInterceptor>();

        // Shares the cross-module connection so stock movements can commit atomically with the
        // originating document + its GL journal (INTEGRATION_EVENTS.md Â§2).
        services.AddDbContext<InventoryDbContext>((sp, options) =>
        {
            options.UseNpgsql(sp.GetRequiredService<ISharedDbConnection>().Connection, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", InventoryDbContext.Schema));
            options.AddInterceptors(
                sp.GetRequiredService<AuditCaptureInterceptor>(),
                sp.GetRequiredService<AuditingSaveChangesInterceptor>());
        });

        services.AddScoped<IInventoryUnitOfWork>(sp => sp.GetRequiredService<InventoryDbContext>());
        services.AddScoped<ITransactionalDbContext>(sp => sp.GetRequiredService<InventoryDbContext>());
        services.AddScoped<IStockBucketRepository, StockBucketRepository>();
        services.AddScoped<IStockCostLayerRepository, StockCostLayerRepository>();
        services.AddScoped<IInventoryTransactionRepository, InventoryTransactionRepository>();
        services.AddScoped<IInventoryLedger, InventoryLedgerService>();
        services.AddScoped<IInventoryPosting, InventoryPostingService>();

        var applicationAssembly = typeof(ReceiveStockCommand).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly));
        services.AddValidatorsFromAssembly(applicationAssembly);

        return services;
    }

    public static async Task InitializeInventoryModuleAsync(
        this IServiceProvider services, bool migrate, CancellationToken ct = default)
    {
        if (!migrate)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await db.Database.MigrateAsync(ct);
    }
}
