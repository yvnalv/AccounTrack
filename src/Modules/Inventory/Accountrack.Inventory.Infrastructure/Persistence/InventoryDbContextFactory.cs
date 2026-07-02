using Accountrack.Application.Abstractions.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Accountrack.Inventory.Infrastructure.Persistence;

/// <summary>Design-time factory so `dotnet ef migrations` can build the context without the host.</summary>
public sealed class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=Accountrack_Design;Username=postgres;Password=postgres",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", InventoryDbContext.Schema))
            .Options;

        return new InventoryDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public Guid CompanyId => Guid.Empty;
        public IReadOnlyCollection<Guid> GrantedCompanyIds => Array.Empty<Guid>();
        public bool IsSet => false;
    }
}
