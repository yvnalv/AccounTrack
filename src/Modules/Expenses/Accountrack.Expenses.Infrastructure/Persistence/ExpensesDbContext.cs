using Accountrack.Application.Abstractions.Context;
using Accountrack.Expenses.Application.Abstractions;
using Accountrack.Expenses.Domain;
using Accountrack.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Expenses.Infrastructure.Persistence;

/// <summary>EF Core context owning the Expenses schema ("expenses"). Module unit of work.</summary>
public sealed class ExpensesDbContext : BaseDbContext, IExpensesUnitOfWork
{
    public const string Schema = "expenses";

    public ExpensesDbContext(DbContextOptions<ExpensesDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<ExpenseVoucher> ExpenseVouchers => Set<ExpenseVoucher>();
    public DbSet<ExpenseVoucherLine> ExpenseVoucherLines => Set<ExpenseVoucherLine>();
    public DbSet<ExpenseVoucherNumberSequence> ExpenseVoucherNumberSequences => Set<ExpenseVoucherNumberSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<ExpenseCategory>(b =>
        {
            b.ToTable("ExpenseCategories");
            b.Property(c => c.Code).IsRequired().HasMaxLength(32);
            b.Property(c => c.Name).IsRequired().HasMaxLength(100);
            b.Property(c => c.PostingRuleKey).IsRequired().HasMaxLength(64);
            b.HasIndex(c => new { c.TenantId, c.CompanyId, c.Code }).IsUnique().HasFilter("\"IsDeleted\" = false");
        });

        modelBuilder.Entity<ExpenseVoucher>(b =>
        {
            b.ToTable("ExpenseVouchers");
            b.Property(v => v.Number).IsRequired().HasMaxLength(32);
            b.Property(v => v.PayeeName).HasMaxLength(200);
            b.Property(v => v.Currency).IsRequired().HasMaxLength(3).IsFixedLength();
            b.Property(v => v.Reference).HasMaxLength(128);
            b.Property(v => v.Notes).HasMaxLength(1024);
            b.Property(v => v.SubTotal).HasColumnType("decimal(19,4)");
            b.Property(v => v.TaxTotal).HasColumnType("decimal(19,4)");
            b.Property(v => v.GrandTotal).HasColumnType("decimal(19,4)");
            b.Property(v => v.Status).HasConversion<int>();
            b.HasMany(v => v.Lines).WithOne().HasForeignKey(l => l.ExpenseVoucherId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(v => v.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.HasIndex(v => new { v.TenantId, v.CompanyId, v.Number }).IsUnique().HasFilter("\"IsDeleted\" = false");
            b.HasIndex(v => new { v.TenantId, v.CompanyId, v.ExpenseDate });
        });

        modelBuilder.Entity<ExpenseVoucherLine>(b =>
        {
            b.ToTable("ExpenseVoucherLines");
            b.Property(l => l.ExpenseRuleKey).IsRequired().HasMaxLength(64);
            b.Property(l => l.Description).HasMaxLength(512);
            b.Property(l => l.Amount).HasColumnType("decimal(19,4)");
            b.Property(l => l.TaxRate).HasColumnType("decimal(9,6)");
            b.Property(l => l.LineTax).HasColumnType("decimal(19,4)");
            b.Property(l => l.LineTotal).HasColumnType("decimal(19,4)");
            b.HasIndex(l => l.ExpenseCategoryId);
        });

        modelBuilder.Entity<ExpenseVoucherNumberSequence>(b =>
        {
            b.ToTable("ExpenseVoucherNumberSequences");
            b.HasIndex(s => new { s.TenantId, s.CompanyId }).IsUnique().HasFilter("\"IsDeleted\" = false");
        });

        base.OnModelCreating(modelBuilder);
        ApplyAccountrackConventions(modelBuilder);
    }
}

public sealed class ExpenseCategoryRepository : IExpenseCategoryRepository
{
    private readonly ExpensesDbContext _db;
    public ExpenseCategoryRepository(ExpensesDbContext db) => _db = db;

    public void Add(ExpenseCategory category) => _db.ExpenseCategories.Add(category);

    public Task<ExpenseCategory?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct) =>
        _db.ExpenseCategories.AnyAsync(c => c.Code == code, ct);

    public async Task<IReadOnlyList<ExpenseCategory>> ListAsync(CancellationToken ct) =>
        await _db.ExpenseCategories.OrderBy(c => c.Code).ToListAsync(ct);
}

public sealed class ExpenseVoucherRepository : IExpenseVoucherRepository
{
    private readonly ExpensesDbContext _db;
    public ExpenseVoucherRepository(ExpensesDbContext db) => _db = db;

    public void Add(ExpenseVoucher voucher) => _db.ExpenseVouchers.Add(voucher);

    public Task<ExpenseVoucher?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.ExpenseVouchers.Include(v => v.Lines).FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task<IReadOnlyList<ExpenseVoucher>> ListAsync(CancellationToken ct) =>
        await _db.ExpenseVouchers.Include(v => v.Lines).OrderByDescending(v => v.Number).ToListAsync(ct);

    public Task<ExpenseVoucherNumberSequence?> GetSequenceAsync(CancellationToken ct) =>
        _db.ExpenseVoucherNumberSequences.FirstOrDefaultAsync(ct);

    public void AddSequence(ExpenseVoucherNumberSequence sequence) => _db.ExpenseVoucherNumberSequences.Add(sequence);
}
