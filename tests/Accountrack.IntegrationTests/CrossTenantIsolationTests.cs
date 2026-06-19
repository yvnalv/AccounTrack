using Accountrack.IntegrationTests.Infrastructure;
using Accountrack.MasterData.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Accountrack.IntegrationTests;

/// <summary>
/// Cross-tenant data-isolation suite (MULTI_TENANCY.md §9, non-negotiable #33). Proves the global
/// query filters + tenancy-stamping interceptor actually isolate data at runtime against a real
/// SQL Server, using Master Data's <c>Customer</c> (a tenant- and company-owned entity) as the probe.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class CrossTenantIsolationTests
{
    private readonly SqlServerFixture _fx;

    public CrossTenantIsolationTests(SqlServerFixture fx) => _fx = fx;

    [SkippableFact]
    public async Task Cross_tenant_query_returns_zero_foreign_rows()
    {
        Skip.IfNot(_fx.Available, _fx.SkipReason);

        var (tenantA, companyA) = (Guid.NewGuid(), Guid.NewGuid());
        var (tenantB, companyB) = (Guid.NewGuid(), Guid.NewGuid());

        var idA = await SeedCustomerAsync(tenantA, companyA, "CUST-A");
        var idB = await SeedCustomerAsync(tenantB, companyB, "CUST-B");

        await using var asTenantA = _fx.NewContext(FakeTenantContext.For(tenantA, companyA));
        var visible = await asTenantA.Customers.ToListAsync();

        visible.Select(c => c.Id).Should().Contain(idA).And.NotContain(idB);
        (await asTenantA.Customers.AnyAsync(c => c.Id == idB)).Should().BeFalse(
            "tenant A must never see tenant B's rows");
    }

    [SkippableFact]
    public async Task Company_filter_isolates_within_a_tenant()
    {
        Skip.IfNot(_fx.Available, _fx.SkipReason);

        var tenant = Guid.NewGuid();
        var (companyOne, companyTwo) = (Guid.NewGuid(), Guid.NewGuid());

        var idOne = await SeedCustomerAsync(tenant, companyOne, "CUST-1");
        var idTwo = await SeedCustomerAsync(tenant, companyTwo, "CUST-2");

        await using var asCompanyOne = _fx.NewContext(FakeTenantContext.For(tenant, companyOne));
        var visible = await asCompanyOne.Customers.Select(c => c.Id).ToListAsync();

        visible.Should().Contain(idOne).And.NotContain(idTwo,
            "the active company filter isolates companies inside one tenant");
    }

    [SkippableFact]
    public async Task Insert_stamps_tenant_and_company_from_ambient_context()
    {
        Skip.IfNot(_fx.Available, _fx.SkipReason);

        var (tenant, company) = (Guid.NewGuid(), Guid.NewGuid());

        await using var ctx = _fx.NewContext(FakeTenantContext.For(tenant, company));
        var customer = Customer.Create("STAMP-1", "Stamped", taxId: null, paymentTermDays: 30, creditLimit: 0);
        // App code never sets these; the interceptor stamps them from the ambient context.
        customer.TenantId.Should().Be(Guid.Empty);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        customer.TenantId.Should().Be(tenant);
        customer.CompanyId.Should().Be(company);

        // Confirm the persisted row carries the stamp (read back bypassing the filter).
        await using var verify = _fx.NewContext(FakeTenantContext.None());
        var row = await verify.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == customer.Id);
        row.TenantId.Should().Be(tenant);
        row.CompanyId.Should().Be(company);
    }

    [SkippableFact]
    public async Task Cross_tenant_modify_is_rejected()
    {
        Skip.IfNot(_fx.Available, _fx.SkipReason);

        var (tenantA, companyA) = (Guid.NewGuid(), Guid.NewGuid());
        var idA = await SeedCustomerAsync(tenantA, companyA, "OWNED-A");

        var (tenantB, companyB) = (Guid.NewGuid(), Guid.NewGuid());
        await using var asTenantB = _fx.NewContext(FakeTenantContext.For(tenantB, companyB));

        // Reach A's row by bypassing the filter, then try to mutate it while acting as tenant B.
        var foreign = await asTenantB.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == idA);
        foreign.Deactivate();

        var act = async () => await asTenantB.SaveChangesAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Tenant mismatch*");
    }

    [SkippableFact]
    public async Task Insert_without_tenant_context_is_rejected()
    {
        Skip.IfNot(_fx.Available, _fx.SkipReason);

        await using var ctx = _fx.NewContext(FakeTenantContext.None());
        ctx.Customers.Add(Customer.Create("NOCTX-1", "No context", null, 0, 0));

        var act = async () => await ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*without an established tenant context*");
    }

    private async Task<Guid> SeedCustomerAsync(Guid tenant, Guid company, string code)
    {
        await using var ctx = _fx.NewContext(FakeTenantContext.For(tenant, company));
        var customer = Customer.Create(code, code, taxId: null, paymentTermDays: 30, creditLimit: 0);
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();
        return customer.Id;
    }
}
