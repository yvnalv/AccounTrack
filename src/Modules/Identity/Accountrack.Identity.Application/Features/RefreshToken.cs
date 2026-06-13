using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Identity.Application.Abstractions;
using Accountrack.Identity.Application.Contracts;
using Accountrack.Identity.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Identity.Application.Features;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<AuthResponse>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public sealed class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand, AuthResponse>
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ITokenService _tokenService;
    private readonly IIdentityUnitOfWork _uow;
    private readonly IClock _clock;

    public RefreshTokenCommandHandler(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        ITokenService tokenService,
        IIdentityUnitOfWork uow,
        IClock clock)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _tokenService = tokenService;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var hash = _tokenService.HashRefreshToken(request.RefreshToken);
        var token = await _refreshTokens.GetByHashAsync(hash, cancellationToken);

        if (token is null)
        {
            return IdentityErrors.InvalidRefreshToken;
        }

        // Reuse detection: a consumed token being presented again signals theft — revoke the
        // entire family and reject (SECURITY.md §1, ADR-0020).
        if (token.WasConsumed)
        {
            await RevokeFamilyAsync(token.FamilyId, now, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return IdentityErrors.InvalidRefreshToken;
        }

        if (!token.IsActive(now))
        {
            return IdentityErrors.InvalidRefreshToken;
        }

        var user = await _users.GetByIdAsync(token.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return IdentityErrors.InvalidRefreshToken;
        }

        token.Consume(now);

        var authData = await _users.GetAuthDataAsync(user.Id, cancellationToken);

        // Rotate within the same family.
        var response = AuthFactory.Issue(
            user, authData, token.FamilyId, _tokenService, _refreshTokens, _clock);

        await _uow.SaveChangesAsync(cancellationToken);
        return response;
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken ct)
    {
        var family = await _refreshTokens.GetActiveFamilyAsync(familyId, ct);
        foreach (var t in family)
        {
            t.Revoke(now);
        }
    }
}
