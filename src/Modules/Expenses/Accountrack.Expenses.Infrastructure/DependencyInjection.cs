using Accountrack.Application.Abstractions.Integration;
using Accountrack.Expenses.Application;
using Accountrack.Expenses.Application.Abstractions;
using Accountrack.Expenses.Application.Features;
using Accountrack.Expenses.Infrastructure.Persistence;
using Accountrack.Expenses.Infrastructure.Seed;
using Accountrack.Infrastructure.Common.Persistence.Interceptors;
using Accountrack.Infrastructure.Common.Transactions;
using Accountrack.Modules.Contracts.Events;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Accountrack.Expenses.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddExpensesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddScoped<AuditCaptureInterceptor>();
        services.TryAddScoped<AuditingSaveChangesInterceptor>();

        // Shares the cross-module connection so posting an expense commits the voucher + its GL
        // journal atomically (INTEGRATION_EVENTS.md §2).
        services.AddDbContext<ExpensesDbContext>((sp, options) =>
        {
            options.UseSqlServer(sp.GetRequiredService<ISharedDbConnection>().Connection, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", ExpensesDbContext.Schema));
            options.AddInterceptors(
                sp.GetRequiredService<AuditCaptureInterceptor>(),
                sp.GetRequiredService<AuditingSaveChangesInterceptor>());
        });

        services.AddScoped<IExpensesUnitOfWork>(sp => sp.GetRequiredService<ExpensesDbContext>());
        services.AddScoped<ITransactionalDbContext>(sp => sp.GetRequiredService<ExpensesDbContext>());
        services.AddScoped<IExpenseCategoryRepository, ExpenseCategoryRepository>();
        services.AddScoped<IExpenseVoucherRepository, ExpenseVoucherRepository>();
        services.AddScoped<IExpenseVoucherPoster, ExpenseVoucherPoster>();
        services.AddScoped<IIntegrationEventHandler<ApprovalDecided>, ApprovalDecidedConsumer>();

        var applicationAssembly = typeof(PostExpenseVoucherCommand).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly));
        services.AddValidatorsFromAssembly(applicationAssembly);

        return services;
    }

    public static async Task InitializeExpensesModuleAsync(
        this IServiceProvider services, bool migrate, bool seedDev, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ExpensesDbContext>();

        if (migrate)
        {
            await db.Database.MigrateAsync(ct);
        }

        if (seedDev)
        {
            await ExpensesSeeder.SeedAsync(db, ct);
        }
    }
}
