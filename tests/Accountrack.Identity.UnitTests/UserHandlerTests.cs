using Accountrack.Identity.Application.Abstractions;
using Accountrack.Identity.Application.Features;
using Accountrack.Identity.Domain;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Identity.UnitTests;

public class UserHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IIdentityUnitOfWork _uow = Substitute.For<IIdentityUnitOfWork>();

    private static User NewUser(out Guid roleA, out Guid companyA)
    {
        roleA = Guid.NewGuid();
        companyA = Guid.NewGuid();
        var user = User.Create(TenantId, Email.Create("jo@acme.test"), "hash", "Jo");
        user.AssignRole(roleA);
        user.GrantCompany(companyA);
        return user;
    }

    [Fact]
    public async Task Update_renames_and_replaces_roles_and_companies()
    {
        var user = NewUser(out _, out _);
        _users.GetWithDetailsAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var newRole = Guid.NewGuid();
        var newCompany = Guid.NewGuid();

        var result = await new UpdateUserHandler(_users, _uow).Handle(
            new UpdateUserCommand(user.Id, "Jo Tan", new[] { newRole }, new[] { newCompany }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.FullName.Should().Be("Jo Tan");
        user.Roles.Select(r => r.RoleId).Should().Equal(newRole);
        user.Companies.Select(c => c.CompanyId).Should().Equal(newCompany);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_returns_not_found_for_an_unknown_user()
    {
        _users.GetWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await new UpdateUserHandler(_users, _uow).Handle(
            new UpdateUserCommand(Guid.NewGuid(), "X", Array.Empty<Guid>(), Array.Empty<Guid>()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("IDENTITY.USER_NOT_FOUND");
    }

    [Fact]
    public async Task SetActive_can_disable_a_user()
    {
        var user = NewUser(out _, out _);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await new SetUserActiveHandler(_users, _uow).Handle(
            new SetUserActiveCommand(user.Id, false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeFalse();
    }
}
