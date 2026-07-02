using Accountrack.Application.Abstractions.Integration;
using Accountrack.Infrastructure.Common.Persistence.Interceptors;
using Accountrack.Modules.Contracts.Events;
using Accountrack.Notification.Application;
using Accountrack.Notification.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Accountrack.Notification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        services.TryAddScoped<AuditCaptureInterceptor>();
        services.TryAddScoped<AuditingSaveChangesInterceptor>();

        services.AddDbContext<NotificationDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", NotificationDbContext.Schema));
            options.AddInterceptors(
                sp.GetRequiredService<AuditCaptureInterceptor>(),
                sp.GetRequiredService<AuditingSaveChangesInterceptor>());
        });

        services.AddScoped<INotificationUnitOfWork>(sp => sp.GetRequiredService<NotificationDbContext>());
        services.AddScoped<INotificationRepository, NotificationRepository>();

        services.AddScoped<ApprovalNotificationConsumer>();
        services.AddScoped<IIntegrationEventHandler<ApprovalSubmitted>>(sp => sp.GetRequiredService<ApprovalNotificationConsumer>());
        services.AddScoped<IIntegrationEventHandler<ApprovalDecided>>(sp => sp.GetRequiredService<ApprovalNotificationConsumer>());

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetMyNotificationsQuery).Assembly));

        return services;
    }

    public static async Task InitializeNotificationModuleAsync(
        this IServiceProvider services, bool migrate, CancellationToken ct = default)
    {
        if (!migrate)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await db.Database.MigrateAsync(ct);
    }
}
