using Accountrack.Application.Abstractions.Context;
using Accountrack.CompanyManagement.Application.Abstractions;
using Accountrack.CompanyManagement.Application.Features;
using Accountrack.CompanyManagement.Domain;
using Accountrack.Modules.Contracts.Company;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.CompanyManagement.UnitTests;

public class CreateCompanyHandlerTests
{
    private static readonly DateTime Now = new(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);

    private readonly ICompanyRepository _companies = Substitute.For<ICompanyRepository>();
    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();
    private readonly ICompanyUnitOfWork _uow = Substitute.For<ICompanyUnitOfWork>();
    private readonly ICompanyFoundationSeeder _foundationSeeder = Substitute.For<ICompanyFoundationSeeder>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public CreateCompanyHandlerTests() => _clock.UtcNow.Returns(Now);

    private CreateCompanyCommandHandler CreateHandler() =>
        new(_companies, _tenant, _uow, new[] { _foundationSeeder }, _clock);

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

        // BR-CMP-1: an additional company must also get its operating foundation, otherwise every
        // GL-posting action in it fails.
        await _foundationSeeder.Received(1).SeedAsync(
            Arg.Is<CompanyFoundation>(f =>
                f.TenantId == tenantId && f.FunctionalCurrency == "IDR" && f.Year == Now.Year),
            Arg.Any<CancellationToken>());
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
        await _foundationSeeder.DidNotReceive().SeedAsync(
            Arg.Any<CompanyFoundation>(), Arg.Any<CancellationToken>());
    }
}
