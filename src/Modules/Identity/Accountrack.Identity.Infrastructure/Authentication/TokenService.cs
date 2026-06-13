using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Accountrack.Application.Abstractions.Context;
using Accountrack.Identity.Application.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Accountrack.Identity.Infrastructure.Authentication;

/// <summary>
/// Issues HS256 JWT access tokens and cryptographically-random refresh tokens (ADR-0020).
/// Only the SHA-256 hash of a refresh token is ever stored. RS256 is a planned upgrade.
/// </summary>
public sealed class TokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly IClock _clock;

    public TokenService(IOptions<JwtOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public AccessTokenDescriptor GenerateAccessToken(TokenSubject subject)
    {
        var expiresAt = _clock.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, subject.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(AccountrackClaims.TenantId, subject.TenantId.ToString()),
        };

        claims.AddRange(subject.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(subject.Permissions.Select(p => new Claim(AccountrackClaims.Permission, p)));
        claims.AddRange(subject.CompanyIds.Select(c => new Claim(AccountrackClaims.Company, c.ToString())));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: _clock.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        var value = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessTokenDescriptor(value, expiresAt);
    }

    public RefreshTokenDescriptor GenerateRefreshToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var hash = HashRefreshToken(raw);
        var expiresAt = _clock.UtcNow.AddDays(_options.RefreshTokenDays);
        return new RefreshTokenDescriptor(raw, hash, expiresAt);
    }

    public string HashRefreshToken(string rawValue)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));
        return Convert.ToBase64String(bytes);
    }
}
