using Accountrack.Application.Abstractions.Context;
using Accountrack.Infrastructure.Common.Persistence;
using Accountrack.Sales.Application.Abstractions;
using Accountrack.Sales.Domain;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Sales.Infrastructure.Persistence;

/// <summary>EF Core context owning the Sales schema ("sales"). Module unit of work.</summary>
public sealed class SalesDbContext : BaseDbContext, ISalesUnitOfWork
{
    public const string Schema = "sales";

    public SalesDbContext(DbContextOptions<SalesDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderNumberSequence> NumberSequences => Set<SalesOrderNumberSequence>();
    public DbSet<DeliveryOrder> DeliveryOrders => Set<DeliveryOrder>();
    public DbSet<DeliveryOrderLine> DeliveryOrderLines => Set<DeliveryOrderLine>();
    public DbSet<DeliveryOrderNumberSequence> DeliveryOrderNumberSequences => Set<DeliveryOrderNumberSequence>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceLine> SalesInvoiceLines => Set<SalesInvoiceLine>();
    public DbSet<SalesInvoiceNumberSequence> SalesInvoiceNumberSequences => Set<SalesInvoiceNumberSequence>();
    public DbSet<CustomerPayment> CustomerPayments => Set<CustomerPayment>();
    public DbSet<CustomerPaymentAllocation> CustomerPaymentAllocations => Set<CustomerPaymentAllocation>();
    public DbSet<CustomerPaymentNumberSequence> CustomerPaymentNumberSequences => Set<CustomerPaymentNumberSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<SalesOrder>(b =>
        {
            b.ToTable("SalesOrders");
            b.Property(o => o.Number).IsRequired().HasMaxLength(32);
            b.Property(o => o.Currency).IsRequired().HasMaxLength(3).IsFixedLength();
            b.Property(o => o.Notes).HasMaxLength(1024);
            b.Property(o => o.Status).HasConversion<int>();
            b.Property(o => o.SubTotal).HasColumnType("decimal(19,4)");
            b.Property(o => o.TaxTotal).HasColumnType("decimal(19,4)");
            b.Property(o => o.GrandTotal).HasColumnType("decimal(19,4)");
            b.HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.SalesOrderId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(o => o.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.HasIndex(o => new { o.TenantId, o.CompanyId, o.Number }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(o => new { o.TenantId, o.CompanyId, o.Status });
        });

        modelBuilder.Entity<SalesOrderLine>(b =>
        {
            b.ToTable("SalesOrderLines");
            b.Property(l => l.Quantity).HasColumnType("decimal(19,6)");
            b.Property(l => l.UnitPrice).HasColumnType("decimal(19,4)");
            b.Property(l => l.TaxRate).HasColumnType("decimal(9,6)");
            b.Property(l => l.LineSubTotal).HasColumnType("decimal(19,4)");
            b.Property(l => l.LineTaxAmount).HasColumnType("decimal(19,4)");
            b.Property(l => l.LineTotal).HasColumnType("decimal(19,4)");
            b.Property(l => l.DeliveredQuantity).HasColumnType("decimal(19,6)");
            b.Property(l => l.InvoicedQuantity).HasColumnType("decimal(19,6)");
            b.Property(l => l.Description).HasMaxLength(512);
            b.HasIndex(l => l.ProductId);
        });

        modelBuilder.Entity<SalesOrderNumberSequence>(b =>
        {
            b.ToTable("SalesOrderNumberSequences");
            b.HasIndex(s => new { s.TenantId, s.CompanyId }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<DeliveryOrder>(b =>
        {
            b.ToTable("DeliveryOrders");
            b.Property(d => d.Number).IsRequired().HasMaxLength(32);
            b.Property(d => d.Currency).IsRequired().HasMaxLength(3).IsFixedLength();
            b.Property(d => d.Notes).HasMaxLength(1024);
            b.HasMany(d => d.Lines).WithOne().HasForeignKey(l => l.DeliveryOrderId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(d => d.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Ignore(d => d.TotalCost);
            b.HasIndex(d => new { d.TenantId, d.CompanyId, d.Number }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(d => new { d.TenantId, d.CompanyId, d.SalesOrderId });
        });

        modelBuilder.Entity<DeliveryOrderLine>(b =>
        {
            b.ToTable("DeliveryOrderLines");
            b.Property(l => l.Quantity).HasColumnType("decimal(19,6)");
            b.Property(l => l.UnitCost).HasColumnType("decimal(19,4)");
            b.Property(l => l.LineCost).HasColumnType("decimal(19,4)");
            b.HasIndex(l => l.ProductId);
        });

        modelBuilder.Entity<DeliveryOrderNumberSequence>(b =>
        {
            b.ToTable("DeliveryOrderNumberSequences");
            b.HasIndex(s => new { s.TenantId, s.CompanyId }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<SalesInvoice>(b =>
        {
            b.ToTable("SalesInvoices");
            b.Property(i => i.Number).IsRequired().HasMaxLength(32);
            b.Property(i => i.Currency).IsRequired().HasMaxLength(3).IsFixedLength();
            b.Property(i => i.Notes).HasMaxLength(1024);
            b.Property(i => i.SubTotal).HasColumnType("decimal(19,4)");
            b.Property(i => i.TaxTotal).HasColumnType("decimal(19,4)");
            b.Property(i => i.GrandTotal).HasColumnType("decimal(19,4)");
            b.HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.SalesInvoiceId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(i => i.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.HasIndex(i => new { i.TenantId, i.CompanyId, i.Number }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(i => new { i.TenantId, i.CompanyId, i.SalesOrderId });
        });

        modelBuilder.Entity<SalesInvoiceLine>(b =>
        {
            b.ToTable("SalesInvoiceLines");
            b.Property(l => l.Quantity).HasColumnType("decimal(19,6)");
            b.Property(l => l.UnitPrice).HasColumnType("decimal(19,4)");
            b.Property(l => l.TaxRate).HasColumnType("decimal(9,6)");
            b.Property(l => l.LineNet).HasColumnType("decimal(19,4)");
            b.Property(l => l.LineTax).HasColumnType("decimal(19,4)");
            b.Property(l => l.LineTotal).HasColumnType("decimal(19,4)");
            b.HasIndex(l => l.ProductId);
        });

        modelBuilder.Entity<SalesInvoiceNumberSequence>(b =>
        {
            b.ToTable("SalesInvoiceNumberSequences");
            b.HasIndex(s => new { s.TenantId, s.CompanyId }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<CustomerPayment>(b =>
        {
            b.ToTable("CustomerPayments");
            b.Property(p => p.Number).IsRequired().HasMaxLength(32);
            b.Property(p => p.Currency).IsRequired().HasMaxLength(3).IsFixedLength();
            b.Property(p => p.Reference).HasMaxLength(128);
            b.Property(p => p.Notes).HasMaxLength(1024);
            b.HasMany(p => p.Allocations).WithOne().HasForeignKey(a => a.CustomerPaymentId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(p => p.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Ignore(p => p.TotalAmount);
            b.HasIndex(p => new { p.TenantId, p.CompanyId, p.Number }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(p => new { p.TenantId, p.CompanyId, p.CustomerId });
        });

        modelBuilder.Entity<CustomerPaymentAllocation>(b =>
        {
            b.ToTable("CustomerPaymentAllocations");
            b.Property(a => a.Amount).HasColumnType("decimal(19,4)");
            b.HasIndex(a => a.ArOpenItemId);
        });

        modelBuilder.Entity<CustomerPaymentNumberSequence>(b =>
        {
            b.ToTable("CustomerPaymentNumberSequences");
            b.HasIndex(s => new { s.TenantId, s.CompanyId }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        base.OnModelCreating(modelBuilder);
        ApplyAccountrackConventions(modelBuilder);
    }
}

public sealed class SalesOrderRepository : ISalesOrderRepository
{
    private readonly SalesDbContext _db;
    public SalesOrderRepository(SalesDbContext db) => _db = db;

    public void Add(SalesOrder order) => _db.SalesOrders.Add(order);

    public Task<SalesOrder?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.SalesOrders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<IReadOnlyList<SalesOrder>> ListAsync(CancellationToken ct) =>
        await _db.SalesOrders.OrderByDescending(o => o.Number).ToListAsync(ct);

    public Task<SalesOrderNumberSequence?> GetSequenceAsync(CancellationToken ct) =>
        _db.NumberSequences.FirstOrDefaultAsync(ct);

    public void AddSequence(SalesOrderNumberSequence sequence) => _db.NumberSequences.Add(sequence);
}

public sealed class DeliveryOrderRepository : IDeliveryOrderRepository
{
    private readonly SalesDbContext _db;
    public DeliveryOrderRepository(SalesDbContext db) => _db = db;

    public void Add(DeliveryOrder delivery) => _db.DeliveryOrders.Add(delivery);

    public Task<DeliveryOrder?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.DeliveryOrders.Include(d => d.Lines).FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<DeliveryOrder>> ListBySalesOrderAsync(Guid salesOrderId, CancellationToken ct) =>
        await _db.DeliveryOrders.Where(d => d.SalesOrderId == salesOrderId)
            .OrderByDescending(d => d.Number).ToListAsync(ct);

    public Task<DeliveryOrderNumberSequence?> GetSequenceAsync(CancellationToken ct) =>
        _db.DeliveryOrderNumberSequences.FirstOrDefaultAsync(ct);

    public void AddSequence(DeliveryOrderNumberSequence sequence) => _db.DeliveryOrderNumberSequences.Add(sequence);
}

public sealed class SalesInvoiceRepository : ISalesInvoiceRepository
{
    private readonly SalesDbContext _db;
    public SalesInvoiceRepository(SalesDbContext db) => _db = db;

    public void Add(SalesInvoice invoice) => _db.SalesInvoices.Add(invoice);

    public Task<SalesInvoice?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.SalesInvoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<IReadOnlyList<SalesInvoice>> ListBySalesOrderAsync(Guid salesOrderId, CancellationToken ct) =>
        await _db.SalesInvoices.Where(i => i.SalesOrderId == salesOrderId)
            .OrderByDescending(i => i.Number).ToListAsync(ct);

    public Task<SalesInvoiceNumberSequence?> GetSequenceAsync(CancellationToken ct) =>
        _db.SalesInvoiceNumberSequences.FirstOrDefaultAsync(ct);

    public void AddSequence(SalesInvoiceNumberSequence sequence) => _db.SalesInvoiceNumberSequences.Add(sequence);
}

public sealed class CustomerPaymentRepository : ICustomerPaymentRepository
{
    private readonly SalesDbContext _db;
    public CustomerPaymentRepository(SalesDbContext db) => _db = db;

    public void Add(CustomerPayment payment) => _db.CustomerPayments.Add(payment);

    public Task<CustomerPayment?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.CustomerPayments.Include(p => p.Allocations).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<CustomerPayment>> ListByCustomerAsync(Guid customerId, CancellationToken ct) =>
        await _db.CustomerPayments.Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.Number).ToListAsync(ct);

    public Task<CustomerPaymentNumberSequence?> GetSequenceAsync(CancellationToken ct) =>
        _db.CustomerPaymentNumberSequences.FirstOrDefaultAsync(ct);

    public void AddSequence(CustomerPaymentNumberSequence sequence) => _db.CustomerPaymentNumberSequences.Add(sequence);
}
