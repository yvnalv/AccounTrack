using System.Reflection;
using Accountrack.Application.Abstractions.Context;
using Accountrack.IntegrationTests.Infrastructure;
using Accountrack.Infrastructure.Common.Persistence;
using Accountrack.SharedKernel.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Accountrack.IntegrationTests;

/// <summary>
/// Static guarantees that hold without a database (MULTI_TENANCY.md §9, TESTING.md): every module's
/// model is inspected to prove that <b>every</b> tenant-scoped entity carries a global query filter.
/// This catches a new entity that forgets to derive from the tenant base, or a context that skips
/// <c>ApplyAccountrackConventions</c> — the failure modes that would silently leak data across tenants.
/// </summary>
public sealed class ModelConventionTests
{
    // Offline: building a model never opens a connection, so any valid Npgsql string works.
    private const string OfflineConnection = "Host=localhost;Database=ModelOnly;Username=postgres";

    public static IEnumerable<object[]> ModuleContexts()
    {
        foreach (var ctxType in DiscoverContextTypes())
        {
            yield return new object[] { ctxType };
        }
    }

    [Fact]
    public void Discovers_every_module_context()
    {
        // Guards the data-driven tests below from silently passing on an empty set.
        DiscoverContextTypes().Should().HaveCountGreaterThanOrEqualTo(11);
    }

    [Theory]
    [MemberData(nameof(ModuleContexts))]
    public void Every_tenant_entity_has_a_tenant_query_filter(Type contextType)
    {
        using var ctx = BuildContext(contextType);

        var unprotected = new List<string>();
        foreach (var entity in ctx.Model.GetEntityTypes())
        {
            var clr = entity.ClrType;
            var isTenantScoped = typeof(ITenantScoped).IsAssignableFrom(clr);
            if (!isTenantScoped)
            {
                continue;
            }

            if (entity.GetQueryFilter() is null)
            {
                unprotected.Add(clr.Name);
            }
        }

        unprotected.Should().BeEmpty(
            "every tenant-scoped entity in {0} must have a global query filter", contextType.Name);
    }

    [Theory]
    [MemberData(nameof(ModuleContexts))]
    public void Every_non_tenant_entity_still_filters_soft_deletes(Type contextType)
    {
        using var ctx = BuildContext(contextType);

        foreach (var entity in ctx.Model.GetEntityTypes())
        {
            var clr = entity.ClrType;
            // Only our domain entities carry the soft-delete contract; skip owned/join types.
            if (!typeof(Entity).IsAssignableFrom(clr) || typeof(ITenantScoped).IsAssignableFrom(clr))
            {
                continue;
            }

            entity.GetQueryFilter().Should().NotBeNull(
                "{0}.{1} is a soft-deletable entity and must filter IsDeleted", contextType.Name, clr.Name);
        }
    }

    private static DbContext BuildContext(Type contextType)
    {
        var builderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(contextType);
        var builder = (DbContextOptionsBuilder)Activator.CreateInstance(builderType)!;
        builder.UseNpgsql(OfflineConnection);

        // builder.Options is, at runtime, DbContextOptions<TContext>, so it binds to the ctor.
        ITenantContext tenant = FakeTenantContext.For(Guid.NewGuid(), Guid.NewGuid());
        return (DbContext)Activator.CreateInstance(contextType, builder.Options, tenant)!;
    }

    private static List<Type> DiscoverContextTypes()
    {
        var baseDir = AppContext.BaseDirectory;
        foreach (var dll in Directory.GetFiles(baseDir, "Accountrack.*.Infrastructure.dll"))
        {
            try
            {
                Assembly.LoadFrom(dll);
            }
            catch
            {
                // Native/satellite assemblies aren't loadable here; ignore.
            }
        }

        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .Where(t => t is { IsAbstract: false } && typeof(BaseDbContext).IsAssignableFrom(t))
            .Distinct()
            .OrderBy(t => t.Name)
            .ToList();
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
