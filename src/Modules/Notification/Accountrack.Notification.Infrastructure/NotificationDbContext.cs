using Accountrack.Application.Abstractions.Context;
using Accountrack.Infrastructure.Common.Persistence;
using Accountrack.Notification.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Notification.Infrastructure;

/// <summary>EF Core context owning the Notification schema ("notification"). Module unit of work.</summary>
public sealed class NotificationDbContext : BaseDbContext, INotificationUnitOfWork
{
    public const string Schema = "notification";

    public NotificationDbContext(DbContextOptions<NotificationDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<Domain.Notification> Notifications => Set<Domain.Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Domain.Notification>(b =>
        {
            b.ToTable("Notifications");
            b.Property(n => n.Title).IsRequired().HasMaxLength(200);
            b.Property(n => n.Body).IsRequired().HasMaxLength(1024);
            b.HasIndex(n => new { n.TenantId, n.CompanyId, n.UserId });
        });

        base.OnModelCreating(modelBuilder);
        ApplyAccountrackConventions(modelBuilder);
    }
}

public sealed class NotificationRepository : INotificationRepository
{
    private readonly NotificationDbContext _db;
    public NotificationRepository(NotificationDbContext db) => _db = db;

    public void Add(Domain.Notification notification) => _db.Notifications.Add(notification);

    public Task<Domain.Notification?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<IReadOnlyList<Domain.Notification>> ListForUserAsync(Guid userId, bool unreadOnly, CancellationToken ct)
    {
        var query = _db.Notifications.Where(n => n.UserId == userId);
        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAtUtc == null);
        }

        return await query.OrderByDescending(n => n.CreatedAt).ToListAsync(ct);
    }
}
