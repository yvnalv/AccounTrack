using Accountrack.Application.Abstractions.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Accountrack.Sales.Infrastructure.Persistence;

/// <summary>Design-time factory so `dotnet ef migrations` can build the context without the host.</summary>
public sealed class SalesDbContextFactory : IDesignTimeDbContextFactory<SalesDbContext>
{
    public SalesDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=Accountrack_Design;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", SalesDbContext.Schema))
            .Options;

        return new SalesDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public Guid CompanyId => Guid.Empty;
        public IReadOnlyCollection<Guid> GrantedCompanyIds => Array.Empty<Guid>();
        public bool IsSet => false;
    }
}
