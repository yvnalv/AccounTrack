using Accountrack.Application.Abstractions.Context;
using Accountrack.Identity.Application.Abstractions;
using Accountrack.Identity.Application.Features;
using Accountrack.Identity.Domain;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Identity.UnitTests;

public class RoleHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();
    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();
    private readonly IIdentityUnitOfWork _uow = Substitute.For<IIdentityUnitOfWork>();

    private readonly Guid _salesView = Guid.NewGuid();
    private readonly Guid _salesCreate = Guid.NewGuid();

    public RoleHandlerTests()
    {
        _tenant.TenantId.Returns(TenantId);
        _roles.GetPermissionIdByCodeAsync(Arg.Any<CancellationToken>()).Returns(
            new Dictionary<string, Guid>
            {
                [PermissionCatalog.SalesView] = _salesView,
                [PermissionCatalog.SalesCreate] = _salesCreate,
            });
    }

    [Fact]
    public async Task Create_persists_a_role_with_the_given_permissions()
    {
        Role? captured = null;
        _roles.When(r => r.Add(Arg.Any<Role>())).Do(ci => captured = ci.Arg<Role>());

        var result = await new CreateRoleHandler(_roles, _tenant, _uow).Handle(
            new CreateRoleCommand("Cashier", "Front desk", new[] { PermissionCatalog.SalesView, "Bogus.Perm" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.IsSystem.Should().BeFalse();
        captured.Permissions.Should().ContainSingle(p => p.PermissionId == _salesView); // unknown code dropped
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_name()
    {
        _roles.NameExistsAsync("Sales", null, Arg.Any<CancellationToken>()).Returns(true);

        var result = await new CreateRoleHandler(_roles, _tenant, _uow).Handle(
            new CreateRoleCommand("Sales", null, Array.Empty<string>()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("IDENTITY.ROLE_NAME_EXISTS");
    }

    [Fact]
    public async Task Update_blocks_editing_the_Administrator_role()
    {
        var admin = new Role(TenantId, SystemRoles.Administrator, "Full access", isSystem: true);
        _roles.GetByIdAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(admin);

        var result = await new UpdateRoleHandler(_roles, _uow).Handle(
            new UpdateRoleCommand(admin.Id, "Admin", null, Array.Empty<string>()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("IDENTITY.ROLE_IS_ADMINISTRATOR");
    }

    [Fact]
    public async Task Update_replaces_permissions_on_a_system_role_but_keeps_its_name()
    {
        var sales = new Role(TenantId, SystemRoles.SalesUser, "Sales", isSystem: true);
        sales.GrantPermission(_salesView);
        _roles.GetByIdAsync(sales.Id, Arg.Any<CancellationToken>()).Returns(sales);

        var result = await new UpdateRoleHandler(_roles, _uow).Handle(
            new UpdateRoleCommand(sales.Id, "Renamed", "x", new[] { PermissionCatalog.SalesCreate }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sales.Name.Should().Be(SystemRoles.SalesUser);                       // system role name unchanged
        sales.Permissions.Select(p => p.PermissionId).Should().Equal(_salesCreate); // permissions replaced
    }

    [Fact]
    public async Task Delete_blocks_a_system_role()
    {
        var sales = new Role(TenantId, SystemRoles.SalesUser, "Sales", isSystem: true);
        _roles.GetByIdAsync(sales.Id, Arg.Any<CancellationToken>()).Returns(sales);

        var result = await new DeleteRoleHandler(_roles, _uow).Handle(
            new DeleteRoleCommand(sales.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("IDENTITY.ROLE_IS_SYSTEM");
    }

    [Fact]
    public async Task Delete_blocks_a_role_still_assigned_to_users()
    {
        var custom = new Role(TenantId, "Cashier", null, isSystem: false);
        _roles.GetByIdAsync(custom.Id, Arg.Any<CancellationToken>()).Returns(custom);
        _roles.CountUsersAsync(custom.Id, Arg.Any<CancellationToken>()).Returns(2);

        var result = await new DeleteRoleHandler(_roles, _uow).Handle(
            new DeleteRoleCommand(custom.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("IDENTITY.ROLE_IN_USE");
    }

    [Fact]
    public async Task Delete_removes_an_unused_custom_role()
    {
        var custom = new Role(TenantId, "Cashier", null, isSystem: false);
        _roles.GetByIdAsync(custom.Id, Arg.Any<CancellationToken>()).Returns(custom);
        _roles.CountUsersAsync(custom.Id, Arg.Any<CancellationToken>()).Returns(0);

        var result = await new DeleteRoleHandler(_roles, _uow).Handle(
            new DeleteRoleCommand(custom.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _roles.Received(1).Remove(custom);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
