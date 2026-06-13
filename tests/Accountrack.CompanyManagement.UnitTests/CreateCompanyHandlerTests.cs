using Accountrack.Application.Abstractions.Context;
using Accountrack.CompanyManagement.Application.Abstractions;
using Accountrack.CompanyManagement.Application.Features;
using Accountrack.CompanyManagement.Domain;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.CompanyManagement.UnitTests;

public class CreateCompanyHandlerTests
{
    private readonly ICompanyRepository _companies = Substitute.For<ICompanyRepository>();
    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();
    private readonly ICompanyUnitOfWork _uow = Substitute.For<ICompanyUnitOfWork>();

    private CreateCompanyCommandHandler CreateHandler() => new(_companies, _tenant, _uow);

    [Fact]
    public async Task Creates_a_company_in_the_current_tenant()
    {
        var tenantId = Guid.NewGuid();
        _tenant.TenantId.Returns(tenantId);
        _companies.CodeExistsAsync("MAIN", Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler().Handle(
            new CreateCompanyCommand("MAIN", "Main Co", "IDR", 1, "Asia/Jakarta"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _companies.Received(1).Add(Arg.Is<Company>(c => c.Code == "MAIN" && c.TenantId == tenantId));
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_a_duplicate_company_code()
    {
        _tenant.TenantId.Returns(Guid.NewGuid());
        _companies.CodeExistsAsync("MAIN", Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler().Handle(
            new CreateCompanyCommand("MAIN", "Main Co", "IDR", 1, "Asia/Jakarta"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CompanyErrors.CodeAlreadyExists);
        _companies.DidNotReceive().Add(Arg.Any<Company>());
    }
}
