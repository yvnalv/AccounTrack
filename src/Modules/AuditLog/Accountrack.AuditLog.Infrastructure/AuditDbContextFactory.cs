using Accountrack.Application.Abstractions.Context;
using Accountrack.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Accountrack.AuditLog.Infrastructure;

/// <summary>Design-time factory so `dotnet ef migrations` can build the context without the host.</summary>
public sealed class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=Accountrack_Design;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", BaseDbContext.AuditSchema))
            .Options;

        return new AuditDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public Guid CompanyId => Guid.Empty;
        public IReadOnlyCollection<Guid> GrantedCompanyIds => Array.Empty<Guid>();
        public bool IsSet => false;
    }
}
