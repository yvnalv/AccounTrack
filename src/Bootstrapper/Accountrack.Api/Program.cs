using System.Text;
using Accountrack.Api.Authorization;
using Accountrack.Api.Infrastructure;
using Accountrack.Application.Abstractions.Behaviors;
using Accountrack.Accounting.Api;
using Accountrack.Accounting.Infrastructure;
using Accountrack.Application.Abstractions.Context;
using Accountrack.Approval.Api;
using Accountrack.Approval.Infrastructure;
using Accountrack.AuditLog.Api;
using Accountrack.AuditLog.Infrastructure;
using Accountrack.CompanyManagement.Api;
using Accountrack.CompanyManagement.Infrastructure;
using Accountrack.Inventory.Api;
using Accountrack.Inventory.Infrastructure;
using Accountrack.MasterData.Api;
using Accountrack.MasterData.Infrastructure;
using Accountrack.Notification.Api;
using Accountrack.Notification.Infrastructure;
using Accountrack.ProcessTracker.Api;
using Accountrack.ProcessTracker.Infrastructure;
using Accountrack.Purchasing.Api;
using Accountrack.Purchasing.Infrastructure;
using Accountrack.Sales.Api;
using Accountrack.Sales.Infrastructure;
using Accountrack.Expenses.Api;
using Accountrack.Expenses.Infrastructure;
using Accountrack.Identity.Api;
using Accountrack.Identity.Infrastructure;
using Accountrack.Identity.Infrastructure.Authentication;
using Accountrack.Infrastructure.Common.Transactions;
using Accountrack.Web.Common.Contracts;
using Accountrack.Web.Common.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// QuestPDF Community License (ADR-0031) — free for companies/individuals under USD 1M revenue.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// --- Ambient context ports (now backed by the authenticated principal) ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
// Settable tenant scope used by the outbox dispatcher; the request tenant context falls back to it
// when there is no HTTP request (background delivery).
builder.Services.AddScoped<Accountrack.Application.Abstractions.Integration.IAmbientTenant,
    Accountrack.Infrastructure.Common.Outbox.AmbientTenant>();
builder.Services.AddScoped<ITenantContext, HttpContextTenantContext>();

// Idempotency for atomic posting flows (ADR-0021): key from the Idempotency-Key header; results are
// recorded in platform.IdempotencyKeys so a replayed command returns the original id instead of
// double-posting. The store uses its own connection (never the shared cross-module transaction).
builder.Services.AddScoped<Accountrack.Application.Abstractions.Idempotency.IIdempotencyContext,
    HttpContextIdempotencyContext>();
builder.Services.AddSingleton<Accountrack.Application.Abstractions.Idempotency.IIdempotencyStore>(
    new Accountrack.Infrastructure.Common.Idempotency.IdempotencyStore(
        builder.Configuration.GetConnectionString("Default")!));

// In-process integration-event dispatch (ADR-0007). Scoped so handlers resolve from the request scope.
builder.Services.AddScoped<Accountrack.Application.Abstractions.Integration.IIntegrationEventPublisher,
    Accountrack.Infrastructure.Common.Integration.IntegrationEventPublisher>();

// Durable transactional outbox (ADR-0007, INTEGRATION_EVENTS.md §5): approval events are staged in
// the Approval transaction and delivered by a background dispatcher with per-(handler,event) de-dup
// so retries never double-apply a consumer. The inbox store uses its own connection.
builder.Services.AddSingleton<Accountrack.Application.Abstractions.Integration.IInboxStore>(
    new Accountrack.Infrastructure.Common.Outbox.InboxStore(builder.Configuration.GetConnectionString("Default")!));
builder.Services.AddScoped<Accountrack.Application.Abstractions.Integration.IOutboxProcessor,
    Accountrack.Infrastructure.Common.Outbox.OutboxProcessor>();
builder.Services.AddHostedService<Accountrack.Infrastructure.Common.Outbox.OutboxDispatcherService>();

// Cross-module atomic transactions (INTEGRATION_EVENTS.md §2): one shared connection + unit of work
// so flows like Goods Receipt commit stock + journal together. Participating modules (Purchasing,
// Inventory, Accounting) bind their DbContext to this connection.
builder.Services.AddCrossModuleTransactions(builder.Configuration.GetConnectionString("Default")!);

// --- CQRS pipeline (ARCHITECTURE.md §4). Modules register their own handlers. ---
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

// --- Modules ---
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddCompanyModule(builder.Configuration);
builder.Services.AddAuditLogModule(builder.Configuration);
builder.Services.AddAccountingModule(builder.Configuration);
builder.Services.AddMasterDataModule(builder.Configuration);
builder.Services.AddInventoryModule(builder.Configuration);
builder.Services.AddApprovalModule(builder.Configuration);
builder.Services.AddProcessTrackerModule(builder.Configuration);
builder.Services.AddNotificationModule(builder.Configuration);
builder.Services.AddPurchasingModule(builder.Configuration);
builder.Services.AddSalesModule(builder.Configuration);
builder.Services.AddExpensesModule(builder.Configuration);

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
app.MapInventoryEndpoints();
app.MapApprovalEndpoints();
app.MapProcessTrackerEndpoints();
app.MapNotificationEndpoints();
app.MapPurchasingEndpoints();
app.MapSalesEndpoints();
app.MapExpensesEndpoints();

// Optionally migrate + seed module schemas at startup (off by default; needs a database).
if (builder.Configuration.GetValue("Database:Initialize", false))
{
    var migrate = builder.Configuration.GetValue("Database:AutoMigrate", false);
    var seedDev = builder.Configuration.GetValue("Seed:Enabled", false);

    // Audit owns the shared audit table; create it before modules that write to it. (Its migration
    // also creates the database on a first run, so it must precede the platform idempotency table.)
    await app.Services.InitializeAuditLogModuleAsync(migrate);
    // Company before Identity so the dev tenant/company exist before Identity seeds its admin.
    await app.Services.InitializeCompanyModuleAsync(migrate, seedDev);
    await app.Services.InitializeIdentityModuleAsync(migrate);
    await app.Services.InitializeAccountingModuleAsync(migrate, seedDev, DateTime.UtcNow.Year);
    await app.Services.InitializeMasterDataModuleAsync(migrate, seedDev);
    await app.Services.InitializeInventoryModuleAsync(migrate);
    await app.Services.InitializeApprovalModuleAsync(migrate);
    await app.Services.InitializeProcessTrackerModuleAsync(migrate);
    await app.Services.InitializeNotificationModuleAsync(migrate);
    await app.Services.InitializePurchasingModuleAsync(migrate);
    await app.Services.InitializeSalesModuleAsync(migrate);
    await app.Services.InitializeExpensesModuleAsync(migrate, seedDev);

    // Platform-level idempotency key store (ADR-0021), independent of any module schema. Created
    // after the modules so the database exists (the audit migration creates it on a first run).
    await Accountrack.Infrastructure.Common.Idempotency.IdempotencyStore.EnsureTableAsync(
        builder.Configuration.GetConnectionString("Default")!);

    // Platform-level outbox inbox/de-dup store (ADR-0007), independent of any module schema.
    await Accountrack.Infrastructure.Common.Outbox.InboxStore.EnsureTableAsync(
        builder.Configuration.GetConnectionString("Default")!);
}

app.Run();

internal sealed record PingInfo(string Service, string ApiVersion, DateTime TimestampUtc);

// Exposed so WebApplicationFactory-based integration tests can reference the entry assembly.
public partial class Program;
