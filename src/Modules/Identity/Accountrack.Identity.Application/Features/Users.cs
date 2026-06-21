using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Identity.Application.Abstractions;
using Accountrack.Identity.Application.Contracts;
using Accountrack.Identity.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Identity.Application.Features;

// --- Query ---

public sealed record GetUsersQuery : IQuery<IReadOnlyList<UserDto>>;

public sealed class GetUsersHandler : IQueryHandler<GetUsersQuery, IReadOnlyList<UserDto>>
{
    private readonly IUserRepository _users;
    public GetUsersHandler(IUserRepository users) => _users = users;

    public async Task<Result<IReadOnlyList<UserDto>>> Handle(GetUsersQuery request, CancellationToken ct)
    {
        var users = await _users.ListAsync(ct);
        return Result.Success<IReadOnlyList<UserDto>>(users
            .Select(u => new UserDto(
                u.Id, u.Email, u.FullName, u.IsActive, u.LastLoginAtUtc,
                u.Roles.Select(r => r.RoleId).ToList(),
                u.Companies.Select(c => c.CompanyId).ToList()))
            .ToList());
    }
}

// --- Commands ---

public sealed record UpdateUserCommand(
    Guid Id, string FullName, IReadOnlyCollection<Guid> RoleIds, IReadOnlyCollection<Guid> CompanyIds) : ICommand;

public sealed class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateUserHandler : ICommandHandler<UpdateUserCommand>
{
    private readonly IUserRepository _users;
    private readonly IIdentityUnitOfWork _uow;
    public UpdateUserHandler(IUserRepository users, IIdentityUnitOfWork uow)
    {
        _users = users;
        _uow = uow;
    }

    public async Task<Result> Handle(UpdateUserCommand request, CancellationToken ct)
    {
        var user = await _users.GetWithDetailsAsync(request.Id, ct);
        if (user is null)
        {
            return IdentityErrors.UserNotFound;
        }

        user.Rename(request.FullName);
        user.ReplaceRoles(request.RoleIds);
        user.ReplaceCompanies(request.CompanyIds);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record SetUserActiveCommand(Guid Id, bool IsActive) : ICommand;

public sealed class SetUserActiveHandler : ICommandHandler<SetUserActiveCommand>
{
    private readonly IUserRepository _users;
    private readonly IIdentityUnitOfWork _uow;
    public SetUserActiveHandler(IUserRepository users, IIdentityUnitOfWork uow)
    {
        _users = users;
        _uow = uow;
    }

    public async Task<Result> Handle(SetUserActiveCommand request, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(request.Id, ct);
        if (user is null)
        {
            return IdentityErrors.UserNotFound;
        }

        if (request.IsActive)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
        }

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
