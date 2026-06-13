using System.IdentityModel.Tokens.Jwt;
using Accountrack.Identity.Application.Abstractions;
using Accountrack.Identity.Infrastructure.Authentication;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Accountrack.Identity.UnitTests;

public class TokenServiceTests
{
    private readonly TokenService _service;

    public TokenServiceTests()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "Accountrack",
            Audience = "Accountrack",
            SigningKey = "unit-test-signing-key-that-is-long-enough-1234",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7,
        });

        _service = new TokenService(options, new TestClock(new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Access_token_carries_subject_tenant_roles_permissions_and_companies()
    {
        var companyId = Guid.NewGuid();
        var subject = new TokenSubject(
            UserId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            Email: "user@accountrack.local",
            Roles: new[] { "Administrator" },
            Permissions: new[] { "Admin.Users" },
            CompanyIds: new[] { companyId });

        var descriptor = _service.GenerateAccessToken(subject);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(descriptor.Value);

        jwt.Claims.Should().Contain(c => c.Type == "sub" && c.Value == subject.UserId.ToString());
        jwt.Claims.Should().Contain(c => c.Type == AccountrackClaims.TenantId && c.Value == subject.TenantId.ToString());
        jwt.Claims.Should().Contain(c => c.Type == AccountrackClaims.Permission && c.Value == "Admin.Users");
        jwt.Claims.Should().Contain(c => c.Type == AccountrackClaims.Company && c.Value == companyId.ToString());
        descriptor.ExpiresAtUtc.Should().Be(new DateTime(2026, 6, 13, 0, 15, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Refresh_token_hash_is_deterministic_and_raw_values_are_unique()
    {
        var a = _service.GenerateRefreshToken();
        var b = _service.GenerateRefreshToken();

        a.RawValue.Should().NotBe(b.RawValue);
        _service.HashRefreshToken(a.RawValue).Should().Be(a.Hash);
        a.Hash.Should().NotBe(a.RawValue);
    }
}
