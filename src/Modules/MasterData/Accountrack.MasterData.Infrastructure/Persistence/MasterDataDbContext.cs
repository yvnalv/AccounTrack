using Accountrack.Application.Abstractions.Context;
using Accountrack.Infrastructure.Common.Persistence;
using Accountrack.MasterData.Application.Abstractions;
using Accountrack.MasterData.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accountrack.MasterData.Infrastructure.Persistence;

/// <summary>EF Core context owning the Master Data schema ("masterdata"). Module unit of work.</summary>
public sealed class MasterDataDbContext : BaseDbContext, IMasterDataUnitOfWork
{
    public const string Schema = "masterdata";

    public MasterDataDbContext(DbContextOptions<MasterDataDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<TaxCode> TaxCodes => Set<TaxCode>();
    public DbSet<PriceList> PriceLists => Set<PriceList>();
    public DbSet<PriceListItem> PriceListItems => Set<PriceListItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        CodedTable<UnitOfMeasure>(modelBuilder, "UnitsOfMeasure", 16, 100);
        CodedTable<ProductCategory>(modelBuilder, "ProductCategories", 32, 100);

        modelBuilder.Entity<Product>(b =>
        {
            CodedConfig(b, codeLen: 64, nameLen: 200);
            b.ToTable("Products");
            b.HasIndex(p => p.BaseUomId);
            b.Property(p => p.CostingMethod).HasConversion<int>();
        });

        modelBuilder.Entity<Customer>(b =>
        {
            CodedConfig(b, 32, 200);
            b.ToTable("Customers");
            b.Property(c => c.TaxId).HasMaxLength(64);
            b.Property(c => c.CreditLimit).HasColumnType("decimal(19,4)");
        });

        modelBuilder.Entity<Supplier>(b =>
        {
            CodedConfig(b, 32, 200);
            b.ToTable("Suppliers");
            b.Property(s => s.TaxId).HasMaxLength(64);
        });

        modelBuilder.Entity<Warehouse>(b =>
        {
            CodedConfig(b, 32, 200);
            b.ToTable("Warehouses");
            b.Property(w => w.Address).HasMaxLength(512);
        });

        modelBuilder.Entity<TaxCode>(b =>
        {
            CodedConfig(b, 16, 100);
            b.ToTable("TaxCodes");
            b.Property(t => t.Rate).HasColumnType("decimal(9,6)");
        });

        modelBuilder.Entity<PriceList>(b =>
        {
            b.ToTable("PriceLists");
            b.Property(p => p.Name).IsRequired().HasMaxLength(200);
            b.Property(p => p.Type).HasConversion<int>();
            b.HasIndex(p => new { p.TenantId, p.CompanyId, p.Type, p.IsDefault });
        });

        modelBuilder.Entity<PriceListItem>(b =>
        {
            b.ToTable("PriceListItems");
            b.Property(p => p.UnitPrice).HasColumnType("decimal(19,4)");
            b.HasIndex(p => new { p.PriceListId, p.ProductId }).IsUnique().HasFilter("\"IsDeleted\" = false");
        });

        base.OnModelCreating(modelBuilder);
        ApplyAccountrackConventions(modelBuilder);
    }

    private static void CodedTable<T>(ModelBuilder mb, string table, int codeLen, int nameLen)
        where T : class
    {
        mb.Entity<T>(b =>
        {
            b.ToTable(table);
            b.Property("Code").IsRequired().HasMaxLength(codeLen);
            b.Property("Name").IsRequired().HasMaxLength(nameLen);
            b.HasIndex("TenantId", "CompanyId", "Code").IsUnique().HasFilter("\"IsDeleted\" = false");
        });
    }

    private static void CodedConfig<T>(EntityTypeBuilder<T> b, int codeLen, int nameLen)
        where T : class
    {
        b.Property("Code").IsRequired().HasMaxLength(codeLen);
        b.Property("Name").IsRequired().HasMaxLength(nameLen);
        b.HasIndex("TenantId", "CompanyId", "Code").IsUnique().HasFilter("\"IsDeleted\" = false");
    }
}
