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
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceLine> PurchaseInvoiceLines => Set<PurchaseInvoiceLine>();
    public DbSet<PurchaseInvoiceNumberSequence> PurchaseInvoiceNumberSequences => Set<PurchaseInvoiceNumberSequence>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<SupplierPaymentAllocation> SupplierPaymentAllocations => Set<SupplierPaymentAllocation>();
    public DbSet<SupplierPaymentNumberSequence> SupplierPaymentNumberSequences => Set<SupplierPaymentNumberSequence>();
    public DbSet<PurchaseReturn> PurchaseReturns => Set<PurchaseReturn>();
    public DbSet<PurchaseReturnLine> PurchaseReturnLines => Set<PurchaseReturnLine>();
    public DbSet<PurchaseReturnNumberSequence> PurchaseReturnNumberSequences => Set<PurchaseReturnNumberSequence>();

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
            b.Property(l => l.InvoicedQuantity).HasColumnType("decimal(19,6)");
            b.Property(l => l.Description).HasMaxLength(512);
            b.Ignore(l => l.OutstandingQuantity);
            b.Ignore(l => l.UninvoicedReceivedQuantity);
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

        modelBuilder.Entity<PurchaseInvoice>(b =>
        {
            b.ToTable("PurchaseInvoices");
            b.Property(i => i.Number).IsRequired().HasMaxLength(32);
            b.Property(i => i.SupplierInvoiceNo).HasMaxLength(64);
            b.Property(i => i.Currency).IsRequired().HasMaxLength(3).IsFixedLength();
            b.Property(i => i.Notes).HasMaxLength(1024);
            b.Property(i => i.SubTotal).HasColumnType("decimal(19,4)");
            b.Property(i => i.TaxTotal).HasColumnType("decimal(19,4)");
            b.Property(i => i.GrandTotal).HasColumnType("decimal(19,4)");
            b.HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.PurchaseInvoiceId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(i => i.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.HasIndex(i => new { i.TenantId, i.CompanyId, i.Number }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(i => new { i.TenantId, i.CompanyId, i.PurchaseOrderId });
        });

        modelBuilder.Entity<PurchaseInvoiceLine>(b =>
        {
            b.ToTable("PurchaseInvoiceLines");
            b.Property(l => l.Quantity).HasColumnType("decimal(19,6)");
            b.Property(l => l.UnitPrice).HasColumnType("decimal(19,4)");
            b.Property(l => l.TaxRate).HasColumnType("decimal(9,6)");
            b.Property(l => l.LineNet).HasColumnType("decimal(19,4)");
            b.Property(l => l.LineTax).HasColumnType("decimal(19,4)");
            b.Property(l => l.LineTotal).HasColumnType("decimal(19,4)");
            b.Property(l => l.ReturnedQuantity).HasColumnType("decimal(19,6)");
            b.HasIndex(l => l.ProductId);
        });

        modelBuilder.Entity<PurchaseInvoiceNumberSequence>(b =>
        {
            b.ToTable("PurchaseInvoiceNumberSequences");
            b.HasIndex(s => new { s.TenantId, s.CompanyId }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<SupplierPayment>(b =>
        {
            b.ToTable("SupplierPayments");
            b.Property(p => p.Number).IsRequired().HasMaxLength(32);
            b.Property(p => p.Currency).IsRequired().HasMaxLength(3).IsFixedLength();
            b.Property(p => p.Reference).HasMaxLength(128);
            b.Property(p => p.Notes).HasMaxLength(1024);
            b.HasMany(p => p.Allocations).WithOne().HasForeignKey(a => a.SupplierPaymentId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(p => p.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Ignore(p => p.TotalAmount);
            b.HasIndex(p => new { p.TenantId, p.CompanyId, p.Number }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(p => new { p.TenantId, p.CompanyId, p.SupplierId });
        });

        modelBuilder.Entity<SupplierPaymentAllocation>(b =>
        {
            b.ToTable("SupplierPaymentAllocations");
            b.Property(a => a.Amount).HasColumnType("decimal(19,4)");
            b.HasIndex(a => a.ApOpenItemId);
        });

        modelBuilder.Entity<SupplierPaymentNumberSequence>(b =>
        {
            b.ToTable("SupplierPaymentNumberSequences");
            b.HasIndex(s => new { s.TenantId, s.CompanyId }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<PurchaseReturn>(b =>
        {
            b.ToTable("PurchaseReturns");
            b.Property(r => r.Number).IsRequired().HasMaxLength(32);
            b.Property(r => r.Currency).IsRequired().HasMaxLength(3).IsFixedLength();
            b.Property(r => r.Notes).HasMaxLength(1024);
            b.Property(r => r.SubTotal).HasColumnType("decimal(19,4)");
            b.Property(r => r.TaxTotal).HasColumnType("decimal(19,4)");
            b.Property(r => r.GrandTotal).HasColumnType("decimal(19,4)");
            b.HasMany(r => r.Lines).WithOne().HasForeignKey(l => l.PurchaseReturnId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(r => r.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Ignore(r => r.TotalCost);
            b.HasIndex(r => new { r.TenantId, r.CompanyId, r.Number }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(r => new { r.TenantId, r.CompanyId, r.PurchaseOrderId });
            b.HasIndex(r => new { r.TenantId, r.CompanyId, r.PurchaseInvoiceId });
        });

        modelBuilder.Entity<PurchaseReturnLine>(b =>
        {
            b.ToTable("PurchaseReturnLines");
            b.Property(l => l.Quantity).HasColumnType("decimal(19,6)");
            b.Property(l => l.UnitPrice).HasColumnType("decimal(19,4)");
            b.Property(l => l.TaxRate).HasColumnType("decimal(9,6)");
            b.Property(l => l.UnitCost).HasColumnType("decimal(19,4)");
            b.Property(l => l.LineNet).HasColumnType("decimal(19,4)");
            b.Property(l => l.LineTax).HasColumnType("decimal(19,4)");
            b.Property(l => l.LineTotal).HasColumnType("decimal(19,4)");
            b.Property(l => l.LineCost).HasColumnType("decimal(19,4)");
            b.HasIndex(l => l.ProductId);
        });

        modelBuilder.Entity<PurchaseReturnNumberSequence>(b =>
        {
            b.ToTable("PurchaseReturnNumberSequences");
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

public sealed class PurchaseInvoiceRepository : IPurchaseInvoiceRepository
{
    private readonly PurchasingDbContext _db;
    public PurchaseInvoiceRepository(PurchasingDbContext db) => _db = db;

    public void Add(PurchaseInvoice invoice) => _db.PurchaseInvoices.Add(invoice);

    public Task<PurchaseInvoice?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.PurchaseInvoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<IReadOnlyList<PurchaseInvoice>> ListByPurchaseOrderAsync(Guid purchaseOrderId, CancellationToken ct) =>
        await _db.PurchaseInvoices.Where(i => i.PurchaseOrderId == purchaseOrderId)
            .OrderByDescending(i => i.Number).ToListAsync(ct);

    public Task<PurchaseInvoiceNumberSequence?> GetSequenceAsync(CancellationToken ct) =>
        _db.PurchaseInvoiceNumberSequences.FirstOrDefaultAsync(ct);

    public void AddSequence(PurchaseInvoiceNumberSequence sequence) => _db.PurchaseInvoiceNumberSequences.Add(sequence);
}

public sealed class SupplierPaymentRepository : ISupplierPaymentRepository
{
    private readonly PurchasingDbContext _db;
    public SupplierPaymentRepository(PurchasingDbContext db) => _db = db;

    public void Add(SupplierPayment payment) => _db.SupplierPayments.Add(payment);

    public Task<SupplierPayment?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.SupplierPayments.Include(p => p.Allocations).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<SupplierPayment>> ListBySupplierAsync(Guid supplierId, CancellationToken ct) =>
        await _db.SupplierPayments.Where(p => p.SupplierId == supplierId)
            .OrderByDescending(p => p.Number).ToListAsync(ct);

    public Task<SupplierPaymentNumberSequence?> GetSequenceAsync(CancellationToken ct) =>
        _db.SupplierPaymentNumberSequences.FirstOrDefaultAsync(ct);

    public void AddSequence(SupplierPaymentNumberSequence sequence) => _db.SupplierPaymentNumberSequences.Add(sequence);
}

public sealed class PurchaseReturnRepository : IPurchaseReturnRepository
{
    private readonly PurchasingDbContext _db;
    public PurchaseReturnRepository(PurchasingDbContext db) => _db = db;

    public void Add(PurchaseReturn purchaseReturn) => _db.PurchaseReturns.Add(purchaseReturn);

    public Task<PurchaseReturn?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.PurchaseReturns.Include(r => r.Lines).FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<PurchaseReturn>> ListByPurchaseOrderAsync(Guid purchaseOrderId, CancellationToken ct) =>
        await _db.PurchaseReturns.Where(r => r.PurchaseOrderId == purchaseOrderId)
            .OrderByDescending(r => r.Number).ToListAsync(ct);

    public async Task<IReadOnlyList<PurchaseReturn>> ListAsync(CancellationToken ct) =>
        await _db.PurchaseReturns.OrderByDescending(r => r.Number).ToListAsync(ct);

    public Task<PurchaseReturnNumberSequence?> GetSequenceAsync(CancellationToken ct) =>
        _db.PurchaseReturnNumberSequences.FirstOrDefaultAsync(ct);

    public void AddSequence(PurchaseReturnNumberSequence sequence) => _db.PurchaseReturnNumberSequences.Add(sequence);
}
