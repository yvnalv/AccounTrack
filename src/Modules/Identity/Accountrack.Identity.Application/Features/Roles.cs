using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Application.Abstractions.Context;
using Accountrack.Identity.Application.Abstractions;
using Accountrack.Identity.Application.Contracts;
using Accountrack.Identity.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Identity.Application.Features;

// --- Queries ---

public sealed record GetRolesQuery : IQuery<IReadOnlyList<RoleDto>>;

public sealed class GetRolesHandler : IQueryHandler<GetRolesQuery, IReadOnlyList<RoleDto>>
{
    private readonly IRoleRepository _roles;
    public GetRolesHandler(IRoleRepository roles) => _roles = roles;

    public async Task<Result<IReadOnlyList<RoleDto>>> Handle(GetRolesQuery request, CancellationToken ct)
    {
        var roles = await _roles.ListAsync(ct);
        var codeById = (await _roles.GetPermissionIdByCodeAsync(ct)).ToDictionary(kv => kv.Value, kv => kv.Key);

        var list = new List<RoleDto>(roles.Count);
        foreach (var role in roles)
        {
            var count = await _roles.CountUsersAsync(role.Id, ct);
            var codes = role.Permissions
                .Select(p => codeById.GetValueOrDefault(p.PermissionId))
                .Where(c => c is not null)
                .Select(c => c!)
                .OrderBy(c => c)
                .ToList();
            list.Add(new RoleDto(
                role.Id, role.Name, role.Description, role.IsSystem,
                role.Name == SystemRoles.Administrator, count, codes));
        }

        return list;
    }
}

public sealed record GetPermissionsQuery : IQuery<IReadOnlyList<PermissionDto>>;

public sealed class GetPermissionsHandler : IQueryHandler<GetPermissionsQuery, IReadOnlyList<PermissionDto>>
{
    public Task<Result<IReadOnlyList<PermissionDto>>> Handle(GetPermissionsQuery request, CancellationToken ct)
    {
        // The catalog itself is the source of truth (the DB rows mirror it); group by the code prefix.
        var list = PermissionCatalog.All
            .Select(p => new PermissionDto(p.Code, p.Name, p.Code.Split('.')[0]))
            .ToList();
        return Task.FromResult(Result.Success<IReadOnlyList<PermissionDto>>(list));
    }
}

// --- Commands ---

public sealed record CreateRoleCommand(string Name, string? Description, IReadOnlyCollection<string> Permissions)
    : ICommand<Guid>;

public sealed class CreateRoleValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Description).MaximumLength(256);
    }
}

public sealed class CreateRoleHandler : ICommandHandler<CreateRoleCommand, Guid>
{
    private readonly IRoleRepository _roles;
    private readonly ITenantContext _tenant;
    private readonly IIdentityUnitOfWork _uow;
    public CreateRoleHandler(IRoleRepository roles, ITenantContext tenant, IIdentityUnitOfWork uow)
    {
        _roles = roles;
        _tenant = tenant;
        _uow = uow;
    }

    public async Task<Result<Guid>> Handle(CreateRoleCommand request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (await _roles.NameExistsAsync(name, null, ct))
        {
            return IdentityErrors.RoleNameExists;
        }

        var role = new Role(_tenant.TenantId, name, request.Description?.Trim(), isSystem: false);
        await ApplyPermissionsAsync(role, request.Permissions, ct);

        _roles.Add(role);
        await _uow.SaveChangesAsync(ct);
        return role.Id;
    }

    private async Task ApplyPermissionsAsync(Role role, IReadOnlyCollection<string> codes, CancellationToken ct)
    {
        var idByCode = await _roles.GetPermissionIdByCodeAsync(ct);
        role.ReplacePermissions(codes
            .Where(idByCode.ContainsKey)
            .Select(c => idByCode[c]));
    }
}

public sealed record UpdateRoleCommand(Guid Id, string Name, string? Description, IReadOnlyCollection<string> Permissions)
    : ICommand;

public sealed class UpdateRoleValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Description).MaximumLength(256);
    }
}

public sealed class UpdateRoleHandler : ICommandHandler<UpdateRoleCommand>
{
    private readonly IRoleRepository _roles;
    private readonly IIdentityUnitOfWork _uow;
    public UpdateRoleHandler(IRoleRepository roles, IIdentityUnitOfWork uow)
    {
        _roles = roles;
        _uow = uow;
    }

    public async Task<Result> Handle(UpdateRoleCommand request, CancellationToken ct)
    {
        var role = await _roles.GetByIdAsync(request.Id, ct);
        if (role is null)
        {
            return IdentityErrors.RoleNotFound;
        }

        // The Administrator role is always full-access — never editable (prevents lock-out).
        if (role.Name == SystemRoles.Administrator)
        {
            return IdentityErrors.RoleIsAdministrator;
        }

        var name = request.Name.Trim();
        // Built-in roles keep their name; only their permission set is editable.
        if (!role.IsSystem)
        {
            if (await _roles.NameExistsAsync(name, role.Id, ct))
            {
                return IdentityErrors.RoleNameExists;
            }

            role.Rename(name, request.Description);
        }

        var idByCode = await _roles.GetPermissionIdByCodeAsync(ct);
        role.ReplacePermissions(request.Permissions.Where(idByCode.ContainsKey).Select(c => idByCode[c]));

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record DeleteRoleCommand(Guid Id) : ICommand;

public sealed class DeleteRoleHandler : ICommandHandler<DeleteRoleCommand>
{
    private readonly IRoleRepository _roles;
    private readonly IIdentityUnitOfWork _uow;
    public DeleteRoleHandler(IRoleRepository roles, IIdentityUnitOfWork uow)
    {
        _roles = roles;
        _uow = uow;
    }

    public async Task<Result> Handle(DeleteRoleCommand request, CancellationToken ct)
    {
        var role = await _roles.GetByIdAsync(request.Id, ct);
        if (role is null)
        {
            return IdentityErrors.RoleNotFound;
        }

        if (role.IsSystem)
        {
            return IdentityErrors.RoleIsSystem;
        }

        if (await _roles.CountUsersAsync(role.Id, ct) > 0)
        {
            return IdentityErrors.RoleInUse;
        }

        _roles.Remove(role);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
