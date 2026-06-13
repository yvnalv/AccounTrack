using Accountrack.Application.Abstractions.Context;
using Accountrack.Identity.Application.Abstractions;
using Accountrack.Identity.Application.Features;
using Accountrack.Identity.Domain;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Identity.UnitTests;

public class AuthHandlerTests
{
    private static readonly DateTime Now = new(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IIdentityUnitOfWork _uow = Substitute.For<IIdentityUnitOfWork>();
    private readonly IClock _clock = new TestClock(Now);

    public AuthHandlerTests()
    {
        _tokenService.GenerateAccessToken(Arg.Any<TokenSubject>())
            .Returns(new AccessTokenDescriptor("access-token", Now.AddMinutes(15)));
        _tokenService.GenerateRefreshToken()
            .Returns(new RefreshTokenDescriptor("raw-refresh", "hash-refresh", Now.AddDays(7)));
        _users.GetAuthDataAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new UserAuthData(new[] { "Administrator" }, new[] { "Admin.Users" }, Array.Empty<Guid>()));
    }

    private LoginCommandHandler CreateLoginHandler() =>
        new(_users, _refreshTokens, _passwordHasher, _tokenService, _uow, _clock);

    private RefreshTokenCommandHandler CreateRefreshHandler() =>
        new(_users, _refreshTokens, _tokenService, _uow, _clock);

    private static User ActiveUser() =>
        User.Create(Guid.NewGuid(), Email.Create("user@accountrack.local"), "stored-hash", "Tester");

    [Fact]
    public async Task Login_with_valid_credentials_issues_tokens_and_stores_a_refresh_token()
    {
        var user = ActiveUser();
        _users.GetByEmailAsync("user@accountrack.local", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("stored-hash", "correct").Returns(true);

        var result = await CreateLoginHandler()
            .Handle(new LoginCommand("user@accountrack.local", "correct"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-token");
        result.Value.RefreshToken.Should().Be("raw-refresh");
        _refreshTokens.Received(1).Add(Arg.Is<RefreshToken>(t => t.TokenHash == "hash-refresh"));
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_with_unknown_email_fails_with_invalid_credentials()
    {
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await CreateLoginHandler()
            .Handle(new LoginCommand("nobody@accountrack.local", "x"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IdentityErrors.InvalidCredentials);
        _refreshTokens.DidNotReceive().Add(Arg.Any<RefreshToken>());
    }

    [Fact]
    public async Task Login_with_wrong_password_fails_with_invalid_credentials()
    {
        var user = ActiveUser();
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var result = await CreateLoginHandler()
            .Handle(new LoginCommand("user@accountrack.local", "wrong"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IdentityErrors.InvalidCredentials);
    }

    [Fact]
    public async Task Refreshing_a_consumed_token_revokes_the_family_and_is_rejected()
    {
        var familyId = Guid.NewGuid();
        var consumed = new RefreshToken(Guid.NewGuid(), Guid.NewGuid(), "hash-refresh", familyId, Now.AddDays(7));
        consumed.Consume(Now);

        _tokenService.HashRefreshToken("raw-refresh").Returns("hash-refresh");
        _refreshTokens.GetByHashAsync("hash-refresh", Arg.Any<CancellationToken>()).Returns(consumed);
        _refreshTokens.GetActiveFamilyAsync(familyId, Arg.Any<CancellationToken>())
            .Returns(new[] { consumed });

        var result = await CreateRefreshHandler()
            .Handle(new RefreshTokenCommand("raw-refresh"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IdentityErrors.InvalidRefreshToken);
        consumed.RevokedAtUtc.Should().NotBeNull();
        await _uow.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refreshing_an_active_token_rotates_within_the_same_family()
    {
        var familyId = Guid.NewGuid();
        var user = ActiveUser();
        var active = new RefreshToken(user.TenantId, user.Id, "hash-refresh", familyId, Now.AddDays(7));

        _tokenService.HashRefreshToken("raw-refresh").Returns("hash-refresh");
        _refreshTokens.GetByHashAsync("hash-refresh", Arg.Any<CancellationToken>()).Returns(active);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateRefreshHandler()
            .Handle(new RefreshTokenCommand("raw-refresh"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        active.WasConsumed.Should().BeTrue();
        _refreshTokens.Received(1).Add(Arg.Any<RefreshToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
