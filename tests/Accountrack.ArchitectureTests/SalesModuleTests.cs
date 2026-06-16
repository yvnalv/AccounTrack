using System.Reflection;
using Accountrack.Sales.Application.Features;
using Accountrack.Sales.Domain;
using Accountrack.Sales.Infrastructure;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Accountrack.ArchitectureTests;

/// <summary>Clean-architecture boundary rules for the Sales module (ADR-0002/0023).</summary>
public class SalesModuleTests
{
    private static readonly Assembly Domain = typeof(SalesOrder).Assembly;
    private static readonly Assembly Application = typeof(CreateSalesOrderCommand).Assembly;
    private static readonly Assembly Infrastructure = typeof(DependencyInjection).Assembly;

    [Fact]
    public void Domain_should_not_depend_on_framework_or_other_layers()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "MediatR",
                "FluentValidation", "Accountrack.Sales.Application", "Accountrack.Sales.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Application_should_not_depend_on_EfCore_AspNet_or_Infrastructure()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore",
                "Accountrack.Sales.Infrastructure")
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
}
