using Accountrack.Application.Abstractions.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Accountrack.MasterData.Infrastructure.Persistence;

/// <summary>Design-time factory so `dotnet ef migrations` can build the context without the host.</summary>
public sealed class MasterDataDbContextFactory : IDesignTimeDbContextFactory<MasterDataDbContext>
{
    public MasterDataDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MasterDataDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=Accountrack_Design;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", MasterDataDbContext.Schema))
            .Options;

        return new MasterDataDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public Guid CompanyId => Guid.Empty;
        public IReadOnlyCollection<Guid> GrantedCompanyIds => Array.Empty<Guid>();
        public bool IsSet => false;
    }
}
