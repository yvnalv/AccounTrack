using Accountrack.Application.Abstractions.Context;
using Accountrack.CompanyManagement.Application.Abstractions;
using Accountrack.CompanyManagement.Domain;
using Accountrack.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.CompanyManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core context owning the Company Management schema ("company"). Inherits platform
/// conventions from <see cref="BaseDbContext"/> and acts as the module's unit of work.
/// </summary>
public sealed class CompanyDbContext : BaseDbContext, ICompanyUnitOfWork
{
    public const string Schema = "company";

    public CompanyDbContext(DbContextOptions<CompanyDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanySetting> CompanySettings => Set<CompanySetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Tenant>(b =>
        {
            b.ToTable("Tenants");
            b.Property(t => t.Name).IsRequired().HasMaxLength(200);
            b.Property(t => t.Status).HasConversion<int>();
        });

        modelBuilder.Entity<Company>(b =>
        {
            b.ToTable("Companies");
            b.Property(c => c.Code).IsRequired().HasMaxLength(32);
            b.Property(c => c.Name).IsRequired().HasMaxLength(200);
            b.Property(c => c.LegalName).HasMaxLength(256);
            b.Property(c => c.FunctionalCurrency).IsRequired().HasMaxLength(3).IsFixedLength();
            b.Property(c => c.TimeZone).IsRequired().HasMaxLength(64);
            b.Property(c => c.TaxId).HasMaxLength(64);
            b.HasIndex(c => new { c.TenantId, c.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<CompanySetting>(b =>
        {
            b.ToTable("CompanySettings");
            b.Property(s => s.Key).IsRequired().HasMaxLength(128);
            b.Property(s => s.Value).IsRequired().HasMaxLength(2048);
            b.HasIndex(s => new { s.CompanyId, s.Key }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        base.OnModelCreating(modelBuilder);
        ApplyAccountrackConventions(modelBuilder);
    }
}
