using Accountrack.Application.Abstractions.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Accountrack.ProcessTracker.Infrastructure;

/// <summary>Design-time factory so `dotnet ef migrations` can build the context without the host.</summary>
public sealed class ProcessTrackerDbContextFactory : IDesignTimeDbContextFactory<ProcessTrackerDbContext>
{
    public ProcessTrackerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ProcessTrackerDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=Accountrack_Design;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", ProcessTrackerDbContext.Schema))
            .Options;

        return new ProcessTrackerDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public Guid CompanyId => Guid.Empty;
        public IReadOnlyCollection<Guid> GrantedCompanyIds => Array.Empty<Guid>();
        public bool IsSet => false;
    }
}
