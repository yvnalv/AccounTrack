using System.Reflection;
using Accountrack.CompanyManagement.Application.Features;
using Accountrack.CompanyManagement.Domain;
using Accountrack.CompanyManagement.Infrastructure;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Accountrack.ArchitectureTests;

/// <summary>Clean-architecture boundary rules for the Company Management module (ADR-0002/0023).</summary>
public class CompanyModuleTests
{
    private static readonly Assembly Domain = typeof(Company).Assembly;
    private static readonly Assembly Application = typeof(CreateCompanyCommand).Assembly;
    private static readonly Assembly Infrastructure = typeof(DependencyInjection).Assembly;

    [Fact]
    public void Company_Domain_should_not_depend_on_framework_or_other_layers()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "MediatR",
                "FluentValidation",
                "Accountrack.CompanyManagement.Application",
                "Accountrack.CompanyManagement.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Company.Domain must stay pure. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Company_Application_should_not_depend_on_EfCore_AspNet_or_Infrastructure()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Accountrack.CompanyManagement.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Company.Application must not depend on Infrastructure or web concerns. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Company_Infrastructure_should_not_depend_on_AspNet()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOnAny(ArchNamespaces.Web)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Company.Infrastructure must not depend on the web host. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}
