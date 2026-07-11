using System.Reflection;
using Accountrack.Billing.Application.Features;
using Accountrack.Billing.Domain;
using Accountrack.Billing.Infrastructure;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Accountrack.ArchitectureTests;

/// <summary>
/// Clean-architecture boundary rules for the Billing module (ADR-0002, ADR-0039). Billing is Accountrack's
/// commercial ledger; it must stay isolated from the ERP's business modules and — critically — must never
/// depend on Accounting or any tenant GL (SUBSCRIPTION_BILLING.md §5).
/// </summary>
public class BillingModuleTests
{
    private static readonly Assembly Domain = typeof(Plan).Assembly;
    private static readonly Assembly Application = typeof(GetPlansQuery).Assembly;
    private static readonly Assembly Infrastructure = typeof(DependencyInjection).Assembly;

    [Fact]
    public void Domain_should_not_depend_on_framework_or_other_layers()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "MediatR",
                "FluentValidation", "Accountrack.Billing.Application", "Accountrack.Billing.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Application_should_not_depend_on_EfCore_AspNet_or_Infrastructure()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore",
                "Accountrack.Billing.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Infrastructure_should_not_depend_on_AspNet()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOnAny(ArchNamespaces.Web)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Billing_must_not_depend_on_the_accounting_or_other_business_modules()
    {
        // Billing is our commercial ledger — it must never post into a tenant's GL, nor reach into any
        // ERP business module (SUBSCRIPTION_BILLING.md §5). It talks to the outside world only via the
        // shared building blocks and (later) integration events.
        foreach (var assembly in new[] { Domain, Application, Infrastructure })
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(
                    "Accountrack.Accounting", "Accountrack.Sales", "Accountrack.Purchasing",
                    "Accountrack.Inventory", "Accountrack.Expenses", "Accountrack.MasterData")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"{assembly.GetName().Name}: " +
                string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
        }
    }
}
