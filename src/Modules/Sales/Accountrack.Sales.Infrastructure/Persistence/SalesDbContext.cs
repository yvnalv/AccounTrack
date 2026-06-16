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
            b.Property(l => l.Description).HasMaxLength(512);
            b.HasIndex(l => l.ProductId);
        });

        modelBuilder.Entity<SalesOrderNumberSequence>(b =>
        {
            b.ToTable("SalesOrderNumberSequences");
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
