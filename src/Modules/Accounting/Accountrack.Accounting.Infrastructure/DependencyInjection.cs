using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Application.Features;
using Accountrack.Accounting.Application.Services;
using Accountrack.Accounting.Infrastructure.Persistence;
using Accountrack.Accounting.Infrastructure.Seed;
using Accountrack.Infrastructure.Common.Persistence.Interceptors;
using Accountrack.Infrastructure.Common.Transactions;
using Accountrack.Modules.Contracts.Accounting;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Accountrack.Accounting.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAccountingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddScoped<AuditCaptureInterceptor>();
        services.TryAddScoped<AuditingSaveChangesInterceptor>();

        // Shares the cross-module connection so journals posted by other modules (Goods Receipt,
        // invoices, payments) commit atomically with their source document (INTEGRATION_EVENTS.md §2).
        services.AddDbContext<AccountingDbContext>((sp, options) =>
        {
            options.UseSqlServer(sp.GetRequiredService<ISharedDbConnection>().Connection, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", AccountingDbContext.Schema));
            options.AddInterceptors(
                sp.GetRequiredService<AuditCaptureInterceptor>(),
                sp.GetRequiredService<AuditingSaveChangesInterceptor>());
        });

        services.AddScoped<IAccountingUnitOfWork>(sp => sp.GetRequiredService<AccountingDbContext>());
        services.AddScoped<ITransactionalDbContext>(sp => sp.GetRequiredService<AccountingDbContext>());
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IFiscalPeriodRepository, FiscalPeriodRepository>();
        services.AddScoped<IJournalRepository, JournalRepository>();
        services.AddScoped<IPostingRuleRepository, PostingRuleRepository>();
        services.AddScoped<ISubledgerRepository, SubledgerRepository>();
        services.AddScoped<IAccountingReadStore, AccountingReadStore>();
        services.AddScoped<IJournalPoster, JournalPostingService>();
        services.AddScoped<IPostingRuleResolver, PostingRuleResolver>();
        services.AddScoped<ISubledgerService, SubledgerService>();

        // Public cross-module contracts (consumed by Purchasing/Sales atomic flows).
        services.AddScoped<IGeneralLedgerPoster, GeneralLedgerPoster>();
        services.AddScoped<IGeneralLedgerBalances, GeneralLedgerBalances>();
        services.AddScoped<IPostingAccountResolver, PostingAccountResolverAdapter>();
        services.AddScoped<ISubledgerPosting, SubledgerPostingAdapter>();

        var applicationAssembly = typeof(PostJournalCommand).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly));
        services.AddValidatorsFromAssembly(applicationAssembly);

        return services;
    }

    public static async Task InitializeAccountingModuleAsync(
        this IServiceProvider services, bool migrate, bool seedDev, int currentYear, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountingDbContext>();

        if (migrate)
        {
            await db.Database.MigrateAsync(ct);
        }

        if (seedDev)
        {
            await AccountingDataSeeder.SeedAsync(db, currentYear, ct);
        }
    }
}
