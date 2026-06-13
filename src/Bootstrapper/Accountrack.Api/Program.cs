using System.Text;
using Accountrack.Api.Authorization;
using Accountrack.Api.Infrastructure;
using Accountrack.Application.Abstractions.Behaviors;
using Accountrack.Accounting.Api;
using Accountrack.Accounting.Infrastructure;
using Accountrack.Application.Abstractions.Context;
using Accountrack.AuditLog.Api;
using Accountrack.AuditLog.Infrastructure;
using Accountrack.CompanyManagement.Api;
using Accountrack.CompanyManagement.Infrastructure;
using Accountrack.MasterData.Api;
using Accountrack.MasterData.Infrastructure;
using Accountrack.Identity.Api;
using Accountrack.Identity.Infrastructure;
using Accountrack.Identity.Infrastructure.Authentication;
using Accountrack.Web.Common.Contracts;
using Accountrack.Web.Common.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// --- Ambient context ports (now backed by the authenticated principal) ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddScoped<ITenantContext, HttpContextTenantContext>();

// --- CQRS pipeline (ARCHITECTURE.md §4). Modules register their own handlers. ---
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

// --- Modules ---
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddCompanyModule(builder.Configuration);
builder.Services.AddAuditLogModule(builder.Configuration);
builder.Services.AddAccountingModule(builder.Configuration);
builder.Services.AddMasterDataModule(builder.Configuration);

// Accept/emit enums as strings in JSON (nicer API ergonomics).
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// --- Authentication & authorization ---
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claim types exactly as issued (sub, tenant_id, perm, company) rather than
        // remapping to the legacy SOAP URIs.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(string.IsNullOrEmpty(jwt.SigningKey)
                    ? new string('0', 32)
                    : jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddAuthorization();

builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Error envelope first so it wraps everything downstream (ERROR_HANDLING.md).
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapGet("/api/v1/ping", () => Results.Ok(ApiResponse<PingInfo>.Ok(
    new PingInfo("Accountrack", "v1", DateTime.UtcNow))));

app.MapIdentityEndpoints();
app.MapCompanyEndpoints();
app.MapAuditEndpoints();
app.MapAccountingEndpoints();
app.MapMasterDataEndpoints();

// Optionally migrate + seed module schemas at startup (off by default; needs a database).
if (builder.Configuration.GetValue("Database:Initialize", false))
{
    var migrate = builder.Configuration.GetValue("Database:AutoMigrate", false);
    var seedDev = builder.Configuration.GetValue("Seed:Enabled", false);

    // Audit owns the shared audit table; create it before modules that write to it.
    await app.Services.InitializeAuditLogModuleAsync(migrate);
    // Company before Identity so the dev tenant/company exist before Identity seeds its admin.
    await app.Services.InitializeCompanyModuleAsync(migrate, seedDev);
    await app.Services.InitializeIdentityModuleAsync(migrate);
    await app.Services.InitializeAccountingModuleAsync(migrate, seedDev, DateTime.UtcNow.Year);
    await app.Services.InitializeMasterDataModuleAsync(migrate, seedDev);
}

app.Run();

internal sealed record PingInfo(string Service, string ApiVersion, DateTime TimestampUtc);

// Exposed so WebApplicationFactory-based integration tests can reference the entry assembly.
public partial class Program;
