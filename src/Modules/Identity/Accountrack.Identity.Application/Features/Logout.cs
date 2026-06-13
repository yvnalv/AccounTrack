using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Identity.Application.Abstractions;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Identity.Application.Features;

public sealed record LogoutCommand(string RefreshToken) : ICommand;

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public sealed class LogoutCommandHandler : ICommandHandler<LogoutCommand>
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ITokenService _tokenService;
    private readonly IIdentityUnitOfWork _uow;
    private readonly IClock _clock;

    public LogoutCommandHandler(
        IRefreshTokenRepository refreshTokens,
        ITokenService tokenService,
        IIdentityUnitOfWork uow,
        IClock clock)
    {
        _refreshTokens = refreshTokens;
        _tokenService = tokenService;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var hash = _tokenService.HashRefreshToken(request.RefreshToken);
        var token = await _refreshTokens.GetByHashAsync(hash, cancellationToken);

        // Idempotent: revoke the family if found; succeed silently regardless (no enumeration).
        if (token is not null)
        {
            var family = await _refreshTokens.GetActiveFamilyAsync(token.FamilyId, cancellationToken);
            foreach (var t in family)
            {
                t.Revoke(_clock.UtcNow);
            }

            await _uow.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
