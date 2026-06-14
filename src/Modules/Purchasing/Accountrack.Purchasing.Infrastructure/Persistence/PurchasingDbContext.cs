using Accountrack.Application.Abstractions.Context;
using Accountrack.Infrastructure.Common.Persistence;
using Accountrack.Purchasing.Application.Abstractions;
using Accountrack.Purchasing.Domain;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Purchasing.Infrastructure.Persistence;

/// <summary>EF Core context owning the Purchasing schema ("purchasing"). Module unit of work.</summary>
public sealed class PurchasingDbContext : BaseDbContext, IPurchasingUnitOfWork
{
    public const string Schema = "purchasing";

    public PurchasingDbContext(DbContextOptions<PurchasingDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderNumberSequence> NumberSequences => Set<PurchaseOrderNumberSequence>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptLine> GoodsReceiptLines => Set<GoodsReceiptLine>();
    public DbSet<GoodsReceiptNumberSequence> GoodsReceiptNumberSequences => Set<GoodsReceiptNumberSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<PurchaseOrder>(b =>
        {
            b.ToTable("PurchaseOrders");
            b.Property(o => o.Number).IsRequired().HasMaxLength(32);
            b.Property(o => o.Currency).IsRequired().HasMaxLength(3).IsFixedLength();
            b.Property(o => o.Notes).HasMaxLength(1024);
            b.Property(o => o.Status).HasConversion<int>();
            b.Property(o => o.SubTotal).HasColumnType("decimal(19,4)");
            b.Property(o => o.TaxTotal).HasColumnType("decimal(19,4)");
            b.Property(o => o.GrandTotal).HasColumnType("decimal(19,4)");
            b.HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(o => o.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.HasIndex(o => new { o.TenantId, o.CompanyId, o.Number }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(o => new { o.TenantId, o.CompanyId, o.Status });
        });

        modelBuilder.Entity<PurchaseOrderLine>(b =>
        {
            b.ToTable("PurchaseOrderLines");
            b.Property(l => l.Quantity).HasColumnType("decimal(19,6)");
            b.Property(l => l.UnitPrice).HasColumnType("decimal(19,4)");
            b.Property(l => l.TaxRate).HasColumnType("decimal(9,6)");
            b.Property(l => l.LineSubTotal).HasColumnType("decimal(19,4)");
            b.Property(l => l.LineTaxAmount).HasColumnType("decimal(19,4)");
            b.Property(l => l.LineTotal).HasColumnType("decimal(19,4)");
            b.Property(l => l.ReceivedQuantity).HasColumnType("decimal(19,6)");
            b.Property(l => l.Description).HasMaxLength(512);
            b.Ignore(l => l.OutstandingQuantity);
            b.Ignore(l => l.IsFullyReceived);
            b.HasIndex(l => l.ProductId);
        });

        modelBuilder.Entity<PurchaseOrderNumberSequence>(b =>
        {
            b.ToTable("PurchaseOrderNumberSequences");
            b.HasIndex(s => new { s.TenantId, s.CompanyId }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<GoodsReceipt>(b =>
        {
            b.ToTable("GoodsReceipts");
            b.Property(g => g.Number).IsRequired().HasMaxLength(32);
            b.Property(g => g.Currency).IsRequired().HasMaxLength(3).IsFixedLength();
            b.Property(g => g.Notes).HasMaxLength(1024);
            b.HasMany(g => g.Lines).WithOne().HasForeignKey(l => l.GoodsReceiptId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(g => g.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Ignore(g => g.TotalCost);
            b.HasIndex(g => new { g.TenantId, g.CompanyId, g.Number }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(g => new { g.TenantId, g.CompanyId, g.PurchaseOrderId });
        });

        modelBuilder.Entity<GoodsReceiptLine>(b =>
        {
            b.ToTable("GoodsReceiptLines");
            b.Property(l => l.Quantity).HasColumnType("decimal(19,6)");
            b.Property(l => l.UnitCost).HasColumnType("decimal(19,4)");
            b.Property(l => l.LineCost).HasColumnType("decimal(19,4)");
            b.HasIndex(l => l.ProductId);
        });

        modelBuilder.Entity<GoodsReceiptNumberSequence>(b =>
        {
            b.ToTable("GoodsReceiptNumberSequences");
            b.HasIndex(s => new { s.TenantId, s.CompanyId }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        base.OnModelCreating(modelBuilder);
        ApplyAccountrackConventions(modelBuilder);
    }
}

public sealed class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly PurchasingDbContext _db;
    public PurchaseOrderRepository(PurchasingDbContext db) => _db = db;

    public void Add(PurchaseOrder order) => _db.PurchaseOrders.Add(order);

    public Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.PurchaseOrders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<IReadOnlyList<PurchaseOrder>> ListAsync(CancellationToken ct) =>
        await _db.PurchaseOrders.OrderByDescending(o => o.Number).ToListAsync(ct);

    public Task<PurchaseOrderNumberSequence?> GetSequenceAsync(CancellationToken ct) =>
        _db.NumberSequences.FirstOrDefaultAsync(ct);

    public void AddSequence(PurchaseOrderNumberSequence sequence) => _db.NumberSequences.Add(sequence);
}

public sealed class GoodsReceiptRepository : IGoodsReceiptRepository
{
    private readonly PurchasingDbContext _db;
    public GoodsReceiptRepository(PurchasingDbContext db) => _db = db;

    public void Add(GoodsReceipt receipt) => _db.GoodsReceipts.Add(receipt);

    public Task<GoodsReceipt?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.GoodsReceipts.Include(g => g.Lines).FirstOrDefaultAsync(g => g.Id == id, ct);

    public async Task<IReadOnlyList<GoodsReceipt>> ListByPurchaseOrderAsync(Guid purchaseOrderId, CancellationToken ct) =>
        await _db.GoodsReceipts.Where(g => g.PurchaseOrderId == purchaseOrderId)
            .OrderByDescending(g => g.Number).ToListAsync(ct);

    public Task<GoodsReceiptNumberSequence?> GetSequenceAsync(CancellationToken ct) =>
        _db.GoodsReceiptNumberSequences.FirstOrDefaultAsync(ct);

    public void AddSequence(GoodsReceiptNumberSequence sequence) => _db.GoodsReceiptNumberSequences.Add(sequence);
}
