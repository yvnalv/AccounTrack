using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Identity.Application.Abstractions;
using Accountrack.Identity.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Identity.Application.Features;

/// <summary>
/// Creates a user within the current tenant (admin use case — requires Admin.Users).
/// Roles and granted companies are assigned at creation.
/// </summary>
public sealed record CreateUserCommand(
    string Email,
    string Password,
    string FullName,
    IReadOnlyCollection<Guid> RoleIds,
    IReadOnlyCollection<Guid> CompanyIds) : ICommand<Guid>;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateUserCommandHandler : ICommandHandler<CreateUserCommand, Guid>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITenantContext _tenant;
    private readonly IIdentityUnitOfWork _uow;

    public CreateUserCommandHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ITenantContext tenant,
        IIdentityUnitOfWork uow)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tenant = tenant;
        _uow = uow;
    }

    public async Task<Result<Guid>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var email = Email.Create(request.Email);

        if (await _users.EmailExistsAsync(email.Value, cancellationToken))
        {
            return IdentityErrors.EmailAlreadyExists;
        }

        var user = User.Create(
            _tenant.TenantId,
            email,
            _passwordHasher.Hash(request.Password),
            request.FullName.Trim());

        foreach (var roleId in request.RoleIds)
        {
            user.AssignRole(roleId);
        }

        foreach (var companyId in request.CompanyIds)
        {
            user.GrantCompany(companyId);
        }

        _users.Add(user);
        await _uow.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}
