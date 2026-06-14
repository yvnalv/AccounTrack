using Accountrack.Approval.Application.Abstractions;
using Accountrack.Approval.Application.Features;
using Accountrack.Approval.Infrastructure.Persistence;
using Accountrack.Infrastructure.Common.Persistence.Interceptors;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Accountrack.Approval.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddApprovalModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        services.TryAddScoped<AuditCaptureInterceptor>();
        services.TryAddScoped<AuditingSaveChangesInterceptor>();

        services.AddDbContext<ApprovalDbContext>((sp, options) =>
        {
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", ApprovalDbContext.Schema));
            options.AddInterceptors(
                sp.GetRequiredService<AuditCaptureInterceptor>(),
                sp.GetRequiredService<AuditingSaveChangesInterceptor>());
        });

        services.AddScoped<IApprovalUnitOfWork>(sp => sp.GetRequiredService<ApprovalDbContext>());
        services.AddScoped<IApprovalDefinitionRepository, ApprovalDefinitionRepository>();
        services.AddScoped<IApprovalRequestRepository, ApprovalRequestRepository>();

        var applicationAssembly = typeof(SubmitForApprovalCommand).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly));
        services.AddValidatorsFromAssembly(applicationAssembly);

        return services;
    }

    public static async Task InitializeApprovalModuleAsync(
        this IServiceProvider services, bool migrate, CancellationToken ct = default)
    {
        if (!migrate)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApprovalDbContext>();
        await db.Database.MigrateAsync(ct);
    }
}
