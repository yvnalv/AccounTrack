using Accountrack.AuditLog.Application;
using Accountrack.Infrastructure.Common.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Accountrack.AuditLog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAuditLogModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        services.TryAddScoped<AuditingSaveChangesInterceptor>();

        services.AddDbContext<AuditDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", BaseAuditSchema)));

        services.AddScoped<IAuditReadStore, AuditReadStore>();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetAuditEntriesQuery).Assembly));

        return services;
    }

    private const string BaseAuditSchema = "audit";

    public static async Task InitializeAuditLogModuleAsync(
        this IServiceProvider services, bool migrate, CancellationToken ct = default)
    {
        if (!migrate)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        await db.Database.MigrateAsync(ct);
    }
}
