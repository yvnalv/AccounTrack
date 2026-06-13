using Accountrack.CompanyManagement.Application.Abstractions;
using Accountrack.CompanyManagement.Application.Features;
using Accountrack.CompanyManagement.Infrastructure.Persistence;
using Accountrack.CompanyManagement.Infrastructure.Seed;
using Accountrack.Infrastructure.Common.Persistence.Interceptors;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Accountrack.CompanyManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCompanyModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        services.TryAddScoped<AuditCaptureInterceptor>();
        services.TryAddScoped<AuditingSaveChangesInterceptor>();

        services.AddDbContext<CompanyDbContext>((sp, options) =>
        {
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", CompanyDbContext.Schema));
            // Capture audit BEFORE soft-delete conversion, then stamp audit fields (ADR-0026).
            options.AddInterceptors(
                sp.GetRequiredService<AuditCaptureInterceptor>(),
                sp.GetRequiredService<AuditingSaveChangesInterceptor>());
        });

        services.AddScoped<ICompanyUnitOfWork>(sp => sp.GetRequiredService<CompanyDbContext>());
        services.AddScoped<ICompanyRepository, CompanyRepository>();

        var applicationAssembly = typeof(CreateCompanyCommand).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly));
        services.AddValidatorsFromAssembly(applicationAssembly);

        return services;
    }

    /// <summary>Applies migrations (if configured) and seeds the dev tenant/company.</summary>
    public static async Task InitializeCompanyModuleAsync(
        this IServiceProvider services,
        bool migrate,
        bool seedDev,
        CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CompanyDbContext>();

        if (migrate)
        {
            await db.Database.MigrateAsync(ct);
        }

        if (seedDev)
        {
            await CompanyDataSeeder.SeedAsync(db, ct);
        }
    }
}
