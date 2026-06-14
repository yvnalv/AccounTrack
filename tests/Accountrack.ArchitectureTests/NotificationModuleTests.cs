using System.Reflection;
using Accountrack.Notification.Application;
using Accountrack.Notification.Infrastructure;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Accountrack.ArchitectureTests;

/// <summary>Clean-architecture boundary rules for the Notification module (ADR-0002/0023).</summary>
public class NotificationModuleTests
{
    private static readonly Assembly Domain = typeof(Accountrack.Notification.Domain.Notification).Assembly;
    private static readonly Assembly Application = typeof(GetMyNotificationsQuery).Assembly;
    private static readonly Assembly Infrastructure = typeof(DependencyInjection).Assembly;

    [Fact]
    public void Domain_should_not_depend_on_framework_or_other_layers()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "MediatR")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Application_should_not_depend_on_EfCore_AspNet_or_Infrastructure()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore",
                "Accountrack.Notification.Infrastructure")
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
