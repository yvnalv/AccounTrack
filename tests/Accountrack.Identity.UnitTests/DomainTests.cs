using Accountrack.Identity.Domain;
using FluentAssertions;
using Xunit;

namespace Accountrack.Identity.UnitTests;

public class DomainTests
{
    private static readonly DateTime Now = new(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("USER@Accountrack.LOCAL", "user@accountrack.local")]
    [InlineData("  a@b.co  ", "a@b.co")]
    public void Email_is_normalized(string input, string expected) =>
        Email.Create(input).Value.Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("missing@domain")]
    public void Email_rejects_invalid_values(string input) =>
        FluentActions.Invoking(() => Email.Create(input)).Should().Throw<ArgumentException>();

    [Fact]
    public void New_refresh_token_is_active_and_not_consumed()
    {
        var token = NewToken(expiresInDays: 7);
        token.IsActive(Now).Should().BeTrue();
        token.WasConsumed.Should().BeFalse();
    }

    [Fact]
    public void Consumed_or_revoked_or_expired_token_is_not_active()
    {
        var consumed = NewToken(7);
        consumed.Consume(Now);
        consumed.IsActive(Now).Should().BeFalse();
        consumed.WasConsumed.Should().BeTrue();

        var revoked = NewToken(7);
        revoked.Revoke(Now);
        revoked.IsActive(Now).Should().BeFalse();

        var expired = NewToken(-1);
        expired.IsActive(Now).Should().BeFalse();
    }

    [Fact]
    public void Assigning_the_same_role_twice_is_idempotent()
    {
        var user = User.Create(Guid.NewGuid(), Email.Create("a@b.co"), "hash", "Tester");
        var roleId = Guid.NewGuid();

        user.AssignRole(roleId);
        user.AssignRole(roleId);

        user.Roles.Should().HaveCount(1);
    }

    [Fact]
    public void Granting_companies_tracks_access()
    {
        var user = User.Create(Guid.NewGuid(), Email.Create("a@b.co"), "hash", "Tester");
        var companyId = Guid.NewGuid();

        user.GrantCompany(companyId);

        user.HasCompany(companyId).Should().BeTrue();
        user.HasCompany(Guid.NewGuid()).Should().BeFalse();
    }

    private static RefreshToken NewToken(int expiresInDays) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "hash", Guid.NewGuid(), Now.AddDays(expiresInDays));
}
