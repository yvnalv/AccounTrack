using Accountrack.Application.Abstractions.Integration;
using Accountrack.Infrastructure.Common.Persistence.Interceptors;
using Accountrack.Infrastructure.Common.Transactions;
using Accountrack.Modules.Contracts.Events;
using Accountrack.Sales.Application;
using Accountrack.Sales.Application.Abstractions;
using Accountrack.Sales.Application.Features;
using Accountrack.Sales.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Accountrack.Sales.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSalesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddScoped<AuditCaptureInterceptor>();
        services.TryAddScoped<AuditingSaveChangesInterceptor>();

        // Shares the cross-module connection so later slices (delivery → COGS, invoice → AR) can
        // commit stock + journal atomically with the document (INTEGRATION_EVENTS.md §2).
        services.AddDbContext<SalesDbContext>((sp, options) =>
        {
            options.UseSqlServer(sp.GetRequiredService<ISharedDbConnection>().Connection, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", SalesDbContext.Schema));
            options.AddInterceptors(
                sp.GetRequiredService<AuditCaptureInterceptor>(),
                sp.GetRequiredService<AuditingSaveChangesInterceptor>());
        });

        services.AddScoped<ISalesUnitOfWork>(sp => sp.GetRequiredService<SalesDbContext>());
        services.AddScoped<ITransactionalDbContext>(sp => sp.GetRequiredService<SalesDbContext>());
        services.AddScoped<ISalesOrderRepository, SalesOrderRepository>();
        services.AddScoped<IDeliveryOrderRepository, DeliveryOrderRepository>();
        services.AddScoped<ISalesInvoiceRepository, SalesInvoiceRepository>();
        services.AddScoped<ICustomerPaymentRepository, CustomerPaymentRepository>();
        services.AddScoped<ISalesReturnRepository, SalesReturnRepository>();

        // Advance SO status when its approval is decided.
        services.AddScoped<IIntegrationEventHandler<ApprovalDecided>, ApprovalDecidedConsumer>();

        var applicationAssembly = typeof(CreateSalesOrderCommand).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly));
        services.AddValidatorsFromAssembly(applicationAssembly);

        return services;
    }

    public static async Task InitializeSalesModuleAsync(
        this IServiceProvider services, bool migrate, CancellationToken ct = default)
    {
        if (!migrate)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
        await db.Database.MigrateAsync(ct);
    }
}
