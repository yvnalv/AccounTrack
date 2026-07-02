using Accountrack.Application.Abstractions.Integration;
using Accountrack.Infrastructure.Common.Persistence.Interceptors;
using Accountrack.Modules.Contracts.Events;
using Accountrack.ProcessTracker.Application;
using Accountrack.ProcessTracker.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Accountrack.ProcessTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProcessTrackerModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        services.TryAddScoped<AuditCaptureInterceptor>();
        services.TryAddScoped<AuditingSaveChangesInterceptor>();

        services.AddDbContext<ProcessTrackerDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", ProcessTrackerDbContext.Schema));
            options.AddInterceptors(
                sp.GetRequiredService<AuditCaptureInterceptor>(),
                sp.GetRequiredService<AuditingSaveChangesInterceptor>());
        });

        services.AddScoped<IProcessTrackerUnitOfWork>(sp => sp.GetRequiredService<ProcessTrackerDbContext>());
        services.AddScoped<IProcessTimelineRepository, ProcessTimelineRepository>();

        // One consumer subscribes to both approval events.
        services.AddScoped<ApprovalProcessConsumer>();
        services.AddScoped<IIntegrationEventHandler<ApprovalSubmitted>>(sp => sp.GetRequiredService<ApprovalProcessConsumer>());
        services.AddScoped<IIntegrationEventHandler<ApprovalDecided>>(sp => sp.GetRequiredService<ApprovalProcessConsumer>());

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetTimelineQuery).Assembly));

        return services;
    }

    public static async Task InitializeProcessTrackerModuleAsync(
        this IServiceProvider services, bool migrate, CancellationToken ct = default)
    {
        if (!migrate)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcessTrackerDbContext>();
        await db.Database.MigrateAsync(ct);
    }
}
