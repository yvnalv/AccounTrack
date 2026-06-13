using Accountrack.CompanyManagement.Domain;
using FluentAssertions;
using Xunit;

namespace Accountrack.CompanyManagement.UnitTests;

public class CompanyDomainTests
{
    [Fact]
    public void Create_normalizes_currency_to_uppercase()
    {
        var company = Company.Create(Guid.NewGuid(), "MAIN", "Main Co", "idr");
        company.FunctionalCurrency.Should().Be("IDR");
        company.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("ID")]
    [InlineData("IDRX")]
    [InlineData("")]
    public void Create_rejects_invalid_currency(string currency) =>
        FluentActions.Invoking(() => Company.Create(Guid.NewGuid(), "C", "N", currency))
            .Should().Throw<ArgumentException>();

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Create_rejects_invalid_fiscal_month(int month) =>
        FluentActions.Invoking(() => Company.Create(Guid.NewGuid(), "C", "N", "IDR", month))
            .Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void UpdateProfile_changes_editable_fields()
    {
        var company = Company.Create(Guid.NewGuid(), "MAIN", "Main Co", "IDR");
        company.UpdateProfile("New Name", "PT New Name", "01.234.567.8-901.000", "Asia/Makassar");

        company.Name.Should().Be("New Name");
        company.LegalName.Should().Be("PT New Name");
        company.TaxId.Should().Be("01.234.567.8-901.000");
        company.TimeZone.Should().Be("Asia/Makassar");
    }

    [Fact]
    public void Tenant_can_be_suspended_and_reactivated()
    {
        var tenant = Tenant.Create("Acme");
        tenant.Status.Should().Be(TenantStatus.Active);

        tenant.Suspend();
        tenant.Status.Should().Be(TenantStatus.Suspended);

        tenant.Reactivate();
        tenant.Status.Should().Be(TenantStatus.Active);
    }
}
