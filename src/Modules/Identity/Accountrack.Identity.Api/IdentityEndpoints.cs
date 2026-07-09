using System.Security.Claims;
using Accountrack.Identity.Application.Features;
using Accountrack.Identity.Domain;
using Accountrack.Web.Common.Contracts;
using Accountrack.Web.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Accountrack.Identity.Api;

public static class IdentityEndpoints
{
    /// <summary>Rate-limit policy for the anonymous auth endpoints; registered by the host
    /// (<c>AuthRateLimiting</c>, SECURITY.md §5). Kept as a plain string so the module needs no
    /// dependency on the bootstrapper.</summary>
    public const string RateLimitPolicy = "auth";

    public sealed record LoginRequest(string Email, string Password);
    public sealed record RegisterRequest(
        string OrganizationName, string CompanyName, string FunctionalCurrency,
        string FullName, string Email, string Password);
    public sealed record RefreshRequest(string RefreshToken);
    public sealed record LogoutRequest(string RefreshToken);
    public sealed record CreateUserRequest(
        string Email,
        string Password,
        string FullName,
        Guid[] RoleIds,
        Guid[] CompanyIds);

    public sealed record SaveRoleRequest(string Name, string? Description, string[] Permissions);
    public sealed record UpdateUserRequest(string FullName, Guid[] RoleIds, Guid[] CompanyIds);
    public sealed record SetActiveRequest(bool IsActive);

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
        .RequireRateLimiting(RateLimitPolicy)
        .WithName("Login");

        auth.MapPost("/register", async (RegisterRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RegisterOrganizationCommand(
                body.OrganizationName, body.CompanyName, body.FunctionalCurrency,
                body.FullName, body.Email, body.Password), ct);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitPolicy)
        .WithName("RegisterOrganization");

        auth.MapPost("/refresh", async (RefreshRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RefreshTokenCommand(body.RefreshToken), ct);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitPolicy)
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

        users.MapGet("/", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetUsersQuery(), ct)).ToHttpResult())
            .RequireAuthorization(PermissionCatalog.AdminUsers).WithName("GetUsers");

        users.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateUserCommand(id, body.FullName, body.RoleIds, body.CompanyIds), ct)).ToHttpResult())
            .RequireAuthorization(PermissionCatalog.AdminUsers).WithName("UpdateUser");

        users.MapPut("/{id:guid}/active", async (Guid id, SetActiveRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SetUserActiveCommand(id, body.IsActive), ct)).ToHttpResult())
            .RequireAuthorization(PermissionCatalog.AdminUsers).WithName("SetUserActive");

        // --- Roles & permissions (Admin.Roles) ---
        var roles = app.MapGroup("/api/v1/roles").WithTags("Roles").RequireAuthorization();

        roles.MapGet("/", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetRolesQuery(), ct)).ToHttpResult())
            .RequireAuthorization(PermissionCatalog.AdminRoles).WithName("GetRoles");

        roles.MapPost("/", async (SaveRoleRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CreateRoleCommand(body.Name, body.Description, body.Permissions), ct))
                    .ToCreatedResult("/api/v1/roles"))
            .RequireAuthorization(PermissionCatalog.AdminRoles).WithName("CreateRole");

        roles.MapPut("/{id:guid}", async (Guid id, SaveRoleRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateRoleCommand(id, body.Name, body.Description, body.Permissions), ct)).ToHttpResult())
            .RequireAuthorization(PermissionCatalog.AdminRoles).WithName("UpdateRole");

        roles.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DeleteRoleCommand(id), ct)).ToHttpResult())
            .RequireAuthorization(PermissionCatalog.AdminRoles).WithName("DeleteRole");

        app.MapGet("/api/v1/permissions", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetPermissionsQuery(), ct)).ToHttpResult())
            .RequireAuthorization(PermissionCatalog.AdminRoles).WithName("GetPermissions");

        return app;
    }
}
