using System.Reflection;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Infrastructure.Common.Persistence;
using Accountrack.SharedKernel.Domain;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Accountrack.ArchitectureTests;

/// <summary>
/// Enforces the clean-architecture dependency rule across the building blocks (ADR-0002/0023).
/// These run in CI and fail the build on any violation.
/// </summary>
public class LayeringTests
{
    private static readonly Assembly SharedKernel = typeof(Entity).Assembly;
    private static readonly Assembly ApplicationAbstractions = typeof(ICommand).Assembly;
    private static readonly Assembly InfrastructureCommon = typeof(BaseDbContext).Assembly;

    [Fact]
    public void SharedKernel_should_not_depend_on_any_framework_or_module()
    {
        var result = Types.InAssembly(SharedKernel)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "MediatR",
                "FluentValidation",
                "Accountrack.Application",
                "Accountrack.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the SharedKernel must stay dependency-free (ADR-0002). Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Application_should_not_depend_on_EfCore_AspNet_or_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAbstractions)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Accountrack.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the Application layer must not depend on Infrastructure or web concerns (ADR-0002). Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Infrastructure_should_not_depend_on_AspNet()
    {
        var result = Types.InAssembly(InfrastructureCommon)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Infrastructure must not depend on the web host (ARCHITECTURE.md). Offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}
