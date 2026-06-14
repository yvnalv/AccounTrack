using Accountrack.Application.Abstractions.Context;
using Accountrack.Infrastructure.Common.Persistence;
using Accountrack.ProcessTracker.Application.Abstractions;
using Accountrack.ProcessTracker.Domain;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.ProcessTracker.Infrastructure;

/// <summary>EF Core context owning the Process Tracker schema ("process"). Module unit of work.</summary>
public sealed class ProcessTrackerDbContext : BaseDbContext, IProcessTrackerUnitOfWork
{
    public const string Schema = "process";

    public ProcessTrackerDbContext(DbContextOptions<ProcessTrackerDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<ProcessEvent> ProcessEvents => Set<ProcessEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<ProcessEvent>(b =>
        {
            b.ToTable("ProcessEvents");
            b.Property(e => e.DocumentType).IsRequired().HasMaxLength(64);
            b.Property(e => e.Milestone).IsRequired().HasMaxLength(128);
            b.Property(e => e.Note).HasMaxLength(512);
            b.HasIndex(e => new { e.TenantId, e.CompanyId, e.DocumentType, e.DocumentId });
        });

        base.OnModelCreating(modelBuilder);
        ApplyAccountrackConventions(modelBuilder);
    }
}

public sealed class ProcessTimelineRepository : IProcessTimelineRepository
{
    private readonly ProcessTrackerDbContext _db;
    public ProcessTimelineRepository(ProcessTrackerDbContext db) => _db = db;

    public void Add(ProcessEvent processEvent) => _db.ProcessEvents.Add(processEvent);

    public async Task<IReadOnlyList<ProcessEvent>> GetTimelineAsync(string documentType, Guid documentId, CancellationToken ct) =>
        await _db.ProcessEvents
            .Where(e => e.DocumentType == documentType && e.DocumentId == documentId)
            .OrderBy(e => e.OccurredAtUtc)
            .ToListAsync(ct);
}
