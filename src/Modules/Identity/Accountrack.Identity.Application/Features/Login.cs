using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Identity.Application.Abstractions;
using Accountrack.Identity.Application.Contracts;
using Accountrack.Identity.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Identity.Application.Features;

public sealed record LoginCommand(string Email, string Password) : ICommand<AuthResponse>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class LoginCommandHandler : ICommandHandler<LoginCommand, AuthResponse>
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IIdentityUnitOfWork _uow;
    private readonly IClock _clock;

    public LoginCommandHandler(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IIdentityUnitOfWork uow,
        IClock clock)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<AuthResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(email, cancellationToken);

        // Same error whether the user is missing or the password is wrong (no account enumeration).
        if (user is null || !_passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            return IdentityErrors.InvalidCredentials;
        }

        if (!user.IsActive)
        {
            return IdentityErrors.UserInactive;
        }

        var authData = await _users.GetAuthDataAsync(user.Id, cancellationToken);

        var response = AuthFactory.Issue(
            user, authData, familyId: Guid.NewGuid(), _tokenService, _refreshTokens, _clock);

        user.RecordLogin(_clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);

        return response;
    }
}
