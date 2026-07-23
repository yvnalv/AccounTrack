using Accountrack.Application.Abstractions.Context;
using Accountrack.Billing.Application.Abstractions;
using Accountrack.Billing.Domain;
using Accountrack.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Billing.Infrastructure.Persistence;

/// <summary>
/// EF Core context owning the Billing schema ("billing"). This is Accountrack's commercial ledger
/// (SUBSCRIPTION_BILLING.md, ADR-0039) — a standalone context on its own connection that MUST NEVER
/// enlist in the cross-module GL transaction or write to a tenant's business schema (§5).
/// <see cref="Plan"/> is a global catalog (soft-delete filter only); Subscriptions/Invoices are
/// tenant-scoped (tenant query filter applied by the base context).
/// </summary>
public sealed class BillingDbContext : BaseDbContext, IBillingUnitOfWork
{
    public const string Schema = "billing";

    public BillingDbContext(DbContextOptions<BillingDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<BillingInvoice> BillingInvoices => Set<BillingInvoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Plan>(b =>
        {
            b.ToTable("Plans");
            b.Property(p => p.Code).IsRequired().HasMaxLength(64);
            b.Property(p => p.Name).IsRequired().HasMaxLength(100);
            b.Property(p => p.Interval).HasConversion<int>();
            b.Property(p => p.Currency).IsRequired().HasMaxLength(3).IsFixedLength();
            b.Property(p => p.FeaturesJson).IsRequired().HasColumnType("jsonb");
            b.HasIndex(p => p.Code).IsUnique().HasFilter("\"IsDeleted\" = false");
        });

        modelBuilder.Entity<Subscription>(b =>
        {
            b.ToTable("Subscriptions");
            b.Property(s => s.Status).HasConversion<int>();
            b.Property(s => s.Interval).HasConversion<int>();
            b.Property(s => s.PaymentMode).HasConversion<int>();
            b.Property(s => s.GatewayCustomerId).HasMaxLength(128);
            b.Property(s => s.GatewaySubscriptionId).HasMaxLength(128);
            b.HasIndex(s => s.TenantId).IsUnique().HasFilter("\"IsDeleted\" = false");
            b.HasIndex(s => s.PlanId);
        });

        modelBuilder.Entity<BillingInvoice>(b =>
        {
            b.ToTable("BillingInvoices");
            b.Property(i => i.Number).IsRequired().HasMaxLength(32);
            b.Property(i => i.Currency).IsRequired().HasMaxLength(3).IsFixedLength();
            b.Property(i => i.Status).HasConversion<int>();
            b.Property(i => i.GatewayInvoiceId).HasMaxLength(128);
            b.Property(i => i.PdfRef).HasMaxLength(256);
            b.HasIndex(i => new { i.TenantId, i.Number }).IsUnique().HasFilter("\"IsDeleted\" = false");
            b.HasIndex(i => i.SubscriptionId);
        });

        base.OnModelCreating(modelBuilder);
        ApplyAccountrackConventions(modelBuilder);
    }
}

public sealed class PlanRepository : IPlanRepository
{
    private readonly BillingDbContext _db;
    public PlanRepository(BillingDbContext db) => _db = db;

    public void Add(Plan plan) => _db.Plans.Add(plan);

    public Task<Plan?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.Plans.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Plan?> GetByCodeAsync(string code, CancellationToken ct) =>
        _db.Plans.FirstOrDefaultAsync(p => p.Code == code, ct);

    public async Task<IReadOnlyList<Plan>> ListPublicAsync(CancellationToken ct) =>
        await _db.Plans
            .Where(p => p.IsActive && p.IsPublic)
            .OrderBy(p => p.BasePriceMinor).ThenBy(p => p.Interval)
            .ToListAsync(ct);
}

public sealed class SubscriptionRepository : ISubscriptionRepository
{
    private readonly BillingDbContext _db;
    public SubscriptionRepository(BillingDbContext db) => _db = db;

    public void Add(Subscription subscription) => _db.Subscriptions.Add(subscription);

    public Task<Subscription?> GetForCurrentTenantAsync(CancellationToken ct) =>
        _db.Subscriptions.FirstOrDefaultAsync(ct);

    public Task<Subscription?> GetByIdIgnoringFiltersAsync(Guid id, CancellationToken ct) =>
        _db.Subscriptions.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == id, ct);
}

public sealed class BillingInvoiceRepository : IBillingInvoiceRepository
{
    private readonly BillingDbContext _db;
    public BillingInvoiceRepository(BillingDbContext db) => _db = db;

    public void Add(BillingInvoice invoice) => _db.BillingInvoices.Add(invoice);

    public Task<int> CountForCurrentTenantAsync(CancellationToken ct) =>
        _db.BillingInvoices.CountAsync(ct);

    public async Task<IReadOnlyList<BillingInvoice>> ListForCurrentTenantAsync(CancellationToken ct) =>
        await _db.BillingInvoices.OrderByDescending(i => i.PeriodStart).ThenByDescending(i => i.Number)
            .ToListAsync(ct);

    public Task<BillingInvoice?> GetByGatewayInvoiceIdIgnoringFiltersAsync(string gatewayInvoiceId, CancellationToken ct) =>
        _db.BillingInvoices.IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.GatewayInvoiceId == gatewayInvoiceId, ct);
}
