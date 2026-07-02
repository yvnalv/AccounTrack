using Accountrack.Application.Abstractions.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Accountrack.Accounting.Infrastructure.Persistence;

/// <summary>Design-time factory so `dotnet ef migrations` can build the context without the host.</summary>
public sealed class AccountingDbContextFactory : IDesignTimeDbContextFactory<AccountingDbContext>
{
    public AccountingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=Accountrack_Design;Username=postgres;Password=postgres",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", AccountingDbContext.Schema))
            .Options;

        return new AccountingDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public Guid CompanyId => Guid.Empty;
        public IReadOnlyCollection<Guid> GrantedCompanyIds => Array.Empty<Guid>();
        public bool IsSet => false;
    }
}
