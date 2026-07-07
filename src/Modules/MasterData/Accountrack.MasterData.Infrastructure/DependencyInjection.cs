using Accountrack.Infrastructure.Common.Persistence.Interceptors;
using Accountrack.MasterData.Application.Abstractions;
using Accountrack.MasterData.Application.Features;
using Accountrack.MasterData.Infrastructure.Persistence;
using Accountrack.MasterData.Infrastructure.Seed;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Accountrack.MasterData.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMasterDataModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        services.TryAddScoped<AuditCaptureInterceptor>();
        services.TryAddScoped<AuditingSaveChangesInterceptor>();

        services.AddDbContext<MasterDataDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", MasterDataDbContext.Schema));
            options.AddInterceptors(
                sp.GetRequiredService<AuditCaptureInterceptor>(),
                sp.GetRequiredService<AuditingSaveChangesInterceptor>());
        });

        services.AddScoped<IMasterDataUnitOfWork>(sp => sp.GetRequiredService<MasterDataDbContext>());
        services.AddScoped(typeof(ICodedRepository<>), typeof(CodedRepository<>));
        services.AddScoped<IPriceListRepository, PriceListRepository>();
        services.AddScoped<Modules.Contracts.MasterData.IMasterDataLookup, MasterDataLookup>();

        var applicationAssembly = typeof(CreateProductCommand).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly));
        services.AddValidatorsFromAssembly(applicationAssembly);

        return services;
    }

    public static async Task InitializeMasterDataModuleAsync(
        this IServiceProvider services, bool migrate, bool seedDev, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();

        if (migrate)
        {
            await db.Database.MigrateAsync(ct);
        }

        if (seedDev)
        {
            await MasterDataSeeder.SeedAsync(db, ct);
        }
    }
}
