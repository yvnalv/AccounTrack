using Accountrack.Identity.Application.Abstractions;
using Accountrack.Identity.Application.Features;
using Accountrack.Identity.Infrastructure.Authentication;
using Accountrack.Identity.Infrastructure.Persistence;
using Accountrack.Identity.Infrastructure.Persistence.Repositories;
using Accountrack.Identity.Infrastructure.Security;
using Accountrack.Identity.Infrastructure.Seed;
using Accountrack.Infrastructure.Common.Persistence.Interceptors;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Accountrack.Identity.Infrastructure;

/// <summary>Registers the Identity module's services. Called from the host composition root.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Configure<IdentitySeedSettings>(configuration.GetSection(IdentitySeedSettings.SectionName));

        var connectionString = configuration.GetConnectionString("Default");

        services.AddScoped<AuditCaptureInterceptor>();
        services.AddScoped<AuditingSaveChangesInterceptor>();

        services.AddDbContext<IdentityDbContext>((sp, options) =>
        {
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema));
            // Capture audit BEFORE soft-delete conversion, then stamp audit fields (ADR-0026).
            options.AddInterceptors(
                sp.GetRequiredService<AuditCaptureInterceptor>(),
                sp.GetRequiredService<AuditingSaveChangesInterceptor>());
        });

        services.AddScoped<IIdentityUnitOfWork>(sp => sp.GetRequiredService<IdentityDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();

        var applicationAssembly = typeof(LoginCommand).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly));
        services.AddValidatorsFromAssembly(applicationAssembly);

        return services;
    }

    /// <summary>Applies migrations (if configured) and seeds permissions / dev admin.</summary>
    public static async Task InitializeIdentityModuleAsync(
        this IServiceProvider services,
        bool migrate,
        CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        if (migrate)
        {
            await db.Database.MigrateAsync(ct);
        }

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var settings = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<IdentitySeedSettings>>().Value;

        await IdentityDataSeeder.SeedAsync(db, hasher, settings, ct);
    }
}
