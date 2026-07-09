using Accountrack.Application.Abstractions.Context;
using Accountrack.Infrastructure.Common.Persistence;
using Accountrack.Inventory.Application.Abstractions;
using Accountrack.Inventory.Domain;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Inventory.Infrastructure.Persistence;

/// <summary>EF Core context owning the Inventory schema ("inventory"). Module unit of work.</summary>
public sealed class InventoryDbContext : BaseDbContext, IInventoryUnitOfWork
{
    public const string Schema = "inventory";

    public InventoryDbContext(DbContextOptions<InventoryDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<StockCostBucket> StockCostBuckets => Set<StockCostBucket>();
    public DbSet<StockCostLayer> StockCostLayers => Set<StockCostLayer>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<StockCostBucket>(b =>
        {
            b.ToTable("StockCostBuckets");
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3).IsFixedLength();
            b.Property(x => x.CostingMethod).HasConversion<int>();
            b.Property(x => x.OnHandQty).HasColumnType("decimal(19,6)");
            b.Property(x => x.AvgUnitCost).HasColumnType("decimal(19,4)");
            // One bucket per (company, warehouse, product) â€” the cost-bucket granularity (ADR-0015).
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.WarehouseId, x.ProductId })
                .IsUnique().HasFilter("\"IsDeleted\" = false");
        });

        modelBuilder.Entity<StockCostLayer>(b =>
        {
            b.ToTable("StockCostLayers");
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3).IsFixedLength();
            b.Property(x => x.UnitCost).HasColumnType("decimal(19,4)");
            b.Property(x => x.OriginalQty).HasColumnType("decimal(19,6)");
            b.Property(x => x.RemainingQty).HasColumnType("decimal(19,6)");
            // FIFO consumption order per bucket, and open-layer scans for valuation.
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ProductId, x.WarehouseId, x.MovementDate });
        });

        modelBuilder.Entity<InventoryTransaction>(b =>
        {
            b.ToTable("InventoryTransactions");
            b.Property(x => x.Type).HasConversion<int>();
            b.Property(x => x.Source).HasConversion<int>();
            b.Property(x => x.Quantity).HasColumnType("decimal(19,6)");
            b.Property(x => x.UnitCost).HasColumnType("decimal(19,4)");
            b.Property(x => x.TotalCost).HasColumnType("decimal(19,4)");
            b.Property(x => x.RunningQtyAfter).HasColumnType("decimal(19,6)");
            b.Property(x => x.RunningAvgCostAfter).HasColumnType("decimal(19,4)");
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3).IsFixedLength();
            b.Property(x => x.Description).HasMaxLength(512);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ProductId, x.WarehouseId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.MovementDate });
            // Pairs a transfer's two legs for cross-bucket back-dated recompute (ADR-0038); nullable.
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.TransferGroupId });
        });

        base.OnModelCreating(modelBuilder);
        ApplyAccountrackConventions(modelBuilder);
    }
}
