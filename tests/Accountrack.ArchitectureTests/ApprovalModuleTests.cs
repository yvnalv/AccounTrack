using System.Reflection;
using Accountrack.Approval.Application.Features;
using Accountrack.Approval.Domain;
using Accountrack.Approval.Infrastructure;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Accountrack.ArchitectureTests;

/// <summary>Clean-architecture boundary rules for the Approval module (ADR-0002/0023).</summary>
public class ApprovalModuleTests
{
    private static readonly Assembly Domain = typeof(ApprovalRequest).Assembly;
    private static readonly Assembly Application = typeof(SubmitForApprovalCommand).Assembly;
    private static readonly Assembly Infrastructure = typeof(DependencyInjection).Assembly;

    [Fact]
    public void Approval_Domain_should_not_depend_on_framework_or_other_layers()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "MediatR",
                "FluentValidation",
                "Accountrack.Approval.Application",
                "Accountrack.Approval.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Approval.Domain must stay pure. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Approval_Application_should_not_depend_on_EfCore_AspNet_or_Infrastructure()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Accountrack.Approval.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Approval.Application must not depend on Infrastructure or web concerns. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Approval_Infrastructure_should_not_depend_on_AspNet()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOnAny(ArchNamespaces.Web)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Approval.Infrastructure must not depend on the web host. Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}
