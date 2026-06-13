using System.Reflection;
using Accountrack.MasterData.Application.Features;
using Accountrack.MasterData.Domain;
using Accountrack.MasterData.Infrastructure;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Accountrack.ArchitectureTests;

/// <summary>Clean-architecture boundary rules for the Master Data module (ADR-0002/0023).</summary>
public class MasterDataModuleTests
{
    private static readonly Assembly Domain = typeof(Product).Assembly;
    private static readonly Assembly Application = typeof(CreateProductCommand).Assembly;
    private static readonly Assembly Infrastructure = typeof(DependencyInjection).Assembly;

    [Fact]
    public void MasterData_Domain_should_not_depend_on_framework_or_other_layers()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "MediatR",
                "FluentValidation",
                "Accountrack.MasterData.Application",
                "Accountrack.MasterData.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "MasterData.Domain must stay pure. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void MasterData_Application_should_not_depend_on_EfCore_AspNet_or_Infrastructure()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Accountrack.MasterData.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "MasterData.Application must not depend on Infrastructure or web concerns. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void MasterData_Infrastructure_should_not_depend_on_AspNet()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOnAny(ArchNamespaces.Web)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "MasterData.Infrastructure must not depend on the web host. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}
