using Accountrack.Application.Abstractions.Context;
using Accountrack.Identity.Application.Abstractions;
using Accountrack.Identity.Application.Features;
using Accountrack.Identity.Domain;
using Accountrack.Modules.Contracts.Company;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Identity.UnitTests;

public class RegisterOrganizationTests
{
    private static readonly DateTime Now = new(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc);

    private readonly ICompanyProvisioning _provisioning = Substitute.For<ICompanyProvisioning>();
    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IIdentityUnitOfWork _uow = Substitute.For<IIdentityUnitOfWork>();
    private readonly IClock _clock = new TestClock(Now);

    public RegisterOrganizationTests()
    {
        _passwordHasher.Hash(Arg.Any<string>()).Returns("hash");
        _tokenService.GenerateAccessToken(Arg.Any<TokenSubject>())
            .Returns(new AccessTokenDescriptor("access-token", Now.AddMinutes(15)));
        _tokenService.GenerateRefreshToken()
            .Returns(new RefreshTokenDescriptor("raw-refresh", "hash-refresh", Now.AddDays(7)));
        _roles.GetPermissionIdByCodeAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, Guid>());
        _users.GetAuthDataAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new UserAuthData(new[] { "Administrator" }, Array.Empty<string>(), Array.Empty<Guid>()));
    }

    private RegisterOrganizationHandler Handler() =>
        new(_provisioning, _roles, _users, _refreshTokens, _passwordHasher, _tokenService, _uow, _clock);

    private RegisterOrganizationCommand Command() =>
        new("Acme Group", "Acme Trading", "IDR", "Jane Founder", "jane@acme.test", "ChangeMe!123");

    [Fact]
    public async Task Register_provisions_a_tenant_seeds_roles_and_creates_an_administrator()
    {
        var companyId = Guid.NewGuid();
        _users.EmailExistsAsync("jane@acme.test", Arg.Any<CancellationToken>()).Returns(false);
        _provisioning.ProvisionTenantAsync(
                Arg.Any<Guid>(), "Acme Group", Arg.Any<string>(), "Acme Trading",
                "IDR", Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(companyId);

        var addedRoles = new List<Role>();
        _roles.When(r => r.Add(Arg.Any<Role>())).Do(ci => addedRoles.Add(ci.Arg<Role>()));
        User? user = null;
        _users.When(u => u.Add(Arg.Any<User>())).Do(ci => user = ci.Arg<User>());

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-token");

        await _provisioning.Received(1).ProvisionTenantAsync(
            Arg.Any<Guid>(), "Acme Group", Arg.Any<string>(), "Acme Trading",
            "IDR", Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        // All six standard roles seeded for the new tenant.
        addedRoles.Select(r => r.Name).Should().BeEquivalentTo(new[]
        {
            SystemRoles.Administrator, SystemRoles.Accountant, SystemRoles.SalesUser,
            SystemRoles.PurchasingUser, SystemRoles.WarehouseUser, SystemRoles.Viewer,
        });
        // The registrant is the Administrator of the new company.
        var adminRole = addedRoles.Single(r => r.Name == SystemRoles.Administrator);
        user.Should().NotBeNull();
        user!.Roles.Select(r => r.RoleId).Should().Contain(adminRole.Id);
        user.Companies.Select(c => c.CompanyId).Should().Equal(companyId);
    }

    [Fact]
    public async Task Register_rejects_a_taken_email_without_provisioning_anything()
    {
        _users.EmailExistsAsync("jane@acme.test", Arg.Any<CancellationToken>()).Returns(true);

        var result = await Handler().Handle(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("IDENTITY.EMAIL_EXISTS");
        await _provisioning.DidNotReceive().ProvisionTenantAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
