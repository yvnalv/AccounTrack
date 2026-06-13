using System.Reflection;
using Accountrack.AuditLog.Application;
using Accountrack.AuditLog.Infrastructure;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Accountrack.ArchitectureTests;

/// <summary>Clean-architecture boundary rules for the AuditLog module (ADR-0002/0023).</summary>
public class AuditLogModuleTests
{
    private static readonly Assembly Application = typeof(GetAuditEntriesQuery).Assembly;
    private static readonly Assembly Infrastructure = typeof(AuditDbContext).Assembly;

    [Fact]
    public void AuditLog_Application_should_not_depend_on_EfCore_AspNet_or_Infrastructure()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Accountrack.AuditLog.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "AuditLog.Application must not depend on Infrastructure or web concerns. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void AuditLog_Infrastructure_should_not_depend_on_AspNet()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOnAny(ArchNamespaces.Web)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "AuditLog.Infrastructure must not depend on the web host. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}
