using Accountrack.Application.Abstractions.Context;
using Accountrack.Identity.Application.Abstractions;
using Accountrack.Identity.Application.Contracts;
using Accountrack.Identity.Domain;

namespace Accountrack.Identity.Application.Features;

/// <summary>
/// Builds an access + refresh token pair for a user and stores the refresh token.
/// Shared by login (new family) and refresh (same family rotation).
/// </summary>
internal static class AuthFactory
{
    public static AuthResponse Issue(
        User user,
        UserAuthData authData,
        Guid familyId,
        ITokenService tokenService,
        IRefreshTokenRepository refreshTokens,
        IClock clock)
    {
        var subject = new TokenSubject(
            user.Id, user.TenantId, user.Email, authData.Roles, authData.Permissions, authData.CompanyIds);

        var access = tokenService.GenerateAccessToken(subject);
        var refresh = tokenService.GenerateRefreshToken();

        refreshTokens.Add(new RefreshToken(
            user.TenantId, user.Id, refresh.Hash, familyId, refresh.ExpiresAtUtc));

        return new AuthResponse(
            access.Value,
            access.ExpiresAtUtc,
            refresh.RawValue,
            refresh.ExpiresAtUtc,
            user.Id,
            user.Email,
            user.FullName,
            authData.Roles,
            authData.Permissions,
            authData.CompanyIds);
    }
}
