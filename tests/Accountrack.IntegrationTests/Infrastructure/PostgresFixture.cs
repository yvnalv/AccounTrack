using System.Net.Sockets;
using Accountrack.Application.Abstractions.Context;
using Accountrack.Infrastructure.Common.Persistence.Interceptors;
using Accountrack.MasterData.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Accountrack.IntegrationTests.Infrastructure;

/// <summary>
/// Spins up a throwaway PostgreSQL database for the cross-tenant isolation suite
/// (MULTI_TENANCY.md §9, TESTING.md). Uses the real Npgsql provider so global query filters,
/// the tenancy-stamping interceptor, and RowVersion behave exactly as in production.
///
/// TESTING.md prescribes Testcontainers; this fixture instead targets a local/CI PostgreSQL (env
/// var <c>ACCOUNTRACK_TEST_PG</c>, default localhost) and <b>skips</b> the suite when none is
/// reachable, so the tests run wherever a server exists without requiring Docker.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string DefaultServer = "Host=localhost;Port=5432;Username=postgres;Password=postgres";
    private const string DatabaseName = "Accountrack_IsolationTests";

    public bool Available { get; private set; }
    public string? SkipReason { get; private set; }

    private string ConnectionString =>
        $"{(Environment.GetEnvironmentVariable("ACCOUNTRACK_TEST_PG") ?? DefaultServer)};Database={DatabaseName}";

    public async Task InitializeAsync()
    {
        try
        {
            // Build the MasterData schema fresh so it matches the current model.
            await using var db = NewContext(FakeTenantContext.None());
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
            Available = true;
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException or SocketException)
        {
            Available = false;
            SkipReason = $"PostgreSQL not reachable for isolation tests: {ex.Message}";
        }
    }

    public async Task DisposeAsync()
    {
        if (!Available)
        {
            return;
        }

        await using var db = NewContext(FakeTenantContext.None());
        await db.Database.EnsureDeletedAsync();
    }

    /// <summary>Creates a context bound to the given tenant, wired with the production interceptor.</summary>
    public MasterDataDbContext NewContext(ITenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<MasterDataDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(new AuditingSaveChangesInterceptor(tenant, new FakeCurrentUser(), new FixedClock()))
            .Options;

        return new MasterDataDbContext(options, tenant);
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
