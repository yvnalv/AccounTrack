using Accountrack.Notification.Domain;

namespace Accountrack.Notification.Application.Abstractions;

public interface INotificationRepository
{
    void Add(Domain.Notification notification);
    Task<Domain.Notification?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Domain.Notification>> ListForUserAsync(Guid userId, bool unreadOnly, CancellationToken ct);
}

public interface INotificationUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
