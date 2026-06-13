using Accountrack.Api.Contracts;
using Accountrack.Api.Infrastructure;
using Accountrack.Api.Middleware;
using Accountrack.Application.Abstractions.Behaviors;
using Accountrack.Application.Abstractions.Context;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

// --- Ambient context ports (placeholder adapters until Identity/Company modules land) ---
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ICurrentUser, AnonymousCurrentUser>();
builder.Services.AddScoped<ITenantContext, UnsetTenantContext>();

// --- CQRS pipeline (ARCHITECTURE.md §4). Modules register their handlers as they are added. ---
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

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

app.MapHealthChecks("/health");

// Minimal liveness/version endpoint demonstrating the standard success envelope.
app.MapGet("/api/v1/ping", () => Results.Ok(ApiResponse<PingInfo>.Ok(
    new PingInfo("Accountrack", "v1", DateTime.UtcNow))));

app.Run();

internal sealed record PingInfo(string Service, string ApiVersion, DateTime TimestampUtc);

// Exposed so WebApplicationFactory-based integration tests can reference the entry assembly.
public partial class Program;
