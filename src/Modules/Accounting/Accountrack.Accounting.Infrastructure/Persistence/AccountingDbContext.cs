using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Domain;
using Accountrack.Application.Abstractions.Context;
using Accountrack.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Accounting.Infrastructure.Persistence;

/// <summary>EF Core context owning the Accounting schema ("accounting"). Module unit of work.</summary>
public sealed class AccountingDbContext : BaseDbContext, IAccountingUnitOfWork
{
    public const string Schema = "accounting";

    public AccountingDbContext(DbContextOptions<AccountingDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<FiscalYear> FiscalYears => Set<FiscalYear>();
    public DbSet<FiscalPeriod> FiscalPeriods => Set<FiscalPeriod>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<JournalNumberSequence> JournalNumberSequences => Set<JournalNumberSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Account>(b =>
        {
            b.ToTable("Accounts");
            b.Property(a => a.Code).IsRequired().HasMaxLength(32);
            b.Property(a => a.Name).IsRequired().HasMaxLength(200);
            b.Property(a => a.Type).HasConversion<int>();
            b.Property(a => a.NormalBalance).HasConversion<int>();
            b.Property(a => a.ControlType).HasConversion<int>();
            b.HasIndex(a => new { a.TenantId, a.CompanyId, a.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<FiscalYear>(b =>
        {
            b.ToTable("FiscalYears");
            b.HasMany(fy => fy.Periods).WithOne().HasForeignKey(p => p.FiscalYearId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(fy => fy.Periods).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.HasIndex(fy => new { fy.TenantId, fy.CompanyId, fy.Year }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<FiscalPeriod>(b =>
        {
            b.ToTable("FiscalPeriods");
            b.Property(p => p.Status).HasConversion<int>();
            b.HasIndex(p => new { p.TenantId, p.CompanyId, p.StartDate, p.EndDate });
        });

        modelBuilder.Entity<JournalEntry>(b =>
        {
            b.ToTable("JournalEntries");
            b.Property(j => j.EntryNo).HasMaxLength(32);
            b.Property(j => j.Currency).IsRequired().HasMaxLength(3).IsFixedLength();
            b.Property(j => j.Description).IsRequired().HasMaxLength(512);
            b.Property(j => j.Status).HasConversion<int>();
            b.Property(j => j.Source).HasConversion<int>();
            b.HasMany(j => j.Lines).WithOne().HasForeignKey(l => l.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(j => j.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.HasIndex(j => new { j.TenantId, j.CompanyId, j.EntryNo }).IsUnique().HasFilter("[EntryNo] IS NOT NULL AND [IsDeleted] = 0");
            b.HasIndex(j => new { j.TenantId, j.CompanyId, j.Date });
        });

        modelBuilder.Entity<JournalLine>(b =>
        {
            b.ToTable("JournalLines");
            b.Property(l => l.Description).HasMaxLength(512);
            b.OwnsOne(l => l.Debit, m =>
            {
                m.Property(x => x.Amount).HasColumnName("DebitAmount").HasColumnType("decimal(19,4)");
                m.Property(x => x.Currency).HasColumnName("DebitCurrency").HasMaxLength(3).IsFixedLength();
            });
            b.OwnsOne(l => l.Credit, m =>
            {
                m.Property(x => x.Amount).HasColumnName("CreditAmount").HasColumnType("decimal(19,4)");
                m.Property(x => x.Currency).HasColumnName("CreditCurrency").HasMaxLength(3).IsFixedLength();
            });
            b.Navigation(l => l.Debit).IsRequired();
            b.Navigation(l => l.Credit).IsRequired();
            b.HasIndex(l => l.AccountId);
        });

        modelBuilder.Entity<JournalNumberSequence>(b =>
        {
            b.ToTable("JournalNumberSequences");
            b.HasIndex(s => new { s.TenantId, s.CompanyId }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        base.OnModelCreating(modelBuilder);
        ApplyAccountrackConventions(modelBuilder);
    }
}
