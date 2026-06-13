using System.Security.Claims;
using Accountrack.Identity.Application.Features;
using Accountrack.Identity.Domain;
using Accountrack.Web.Common.Contracts;
using Accountrack.Web.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Accountrack.Identity.Api;

public static class IdentityEndpoints
{
    public sealed record LoginRequest(string Email, string Password);
    public sealed record RefreshRequest(string RefreshToken);
    public sealed record LogoutRequest(string RefreshToken);
    public sealed record CreateUserRequest(
        string Email,
        string Password,
        string FullName,
        Guid[] RoleIds,
        Guid[] CompanyIds);

    /// <summary>Maps the Identity module's HTTP endpoints under /api/v1.</summary>
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/v1/auth").WithTags("Authentication");

        auth.MapPost("/login", async (LoginRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new LoginCommand(body.Email, body.Password), ct);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithName("Login");

        auth.MapPost("/refresh", async (RefreshRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RefreshTokenCommand(body.RefreshToken), ct);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithName("RefreshToken");

        auth.MapPost("/logout", async (LogoutRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new LogoutCommand(body.RefreshToken), ct);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithName("Logout");

        auth.MapGet("/me", (ClaimsPrincipal principal) =>
        {
            var me = new
            {
                userId = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier),
                email = principal.FindFirstValue("email") ?? principal.FindFirstValue(ClaimTypes.Email),
                roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray(),
                permissions = principal.FindAll("perm").Select(c => c.Value).ToArray(),
                companies = principal.FindAll("company").Select(c => c.Value).ToArray(),
            };

            return TypedResults.Ok(ApiResponse<object>.Ok(me));
        })
        .RequireAuthorization()
        .WithName("Me");

        var users = app.MapGroup("/api/v1/users").WithTags("Users");

        users.MapPost("/", async (CreateUserRequest body, ISender sender, CancellationToken ct) =>
        {
            var command = new CreateUserCommand(
                body.Email, body.Password, body.FullName, body.RoleIds, body.CompanyIds);
            var result = await sender.Send(command, ct);
            return result.ToCreatedResult("/api/v1/users");
        })
        .RequireAuthorization(PermissionCatalog.AdminUsers)
        .WithName("CreateUser");

        return app;
    }
}
