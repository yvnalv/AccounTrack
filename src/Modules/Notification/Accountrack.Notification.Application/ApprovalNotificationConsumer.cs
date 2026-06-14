using Accountrack.Application.Abstractions.Integration;
using Accountrack.Modules.Contracts.Events;
using Accountrack.Notification.Application.Abstractions;

namespace Accountrack.Notification.Application;

/// <summary>
/// Notifies the document's submitter when their approval is submitted/decided. Best-effort,
/// eventual consumer of the approval integration events (ADR-0007).
/// </summary>
public sealed class ApprovalNotificationConsumer
    : IIntegrationEventHandler<ApprovalSubmitted>, IIntegrationEventHandler<ApprovalDecided>
{
    private readonly INotificationRepository _notifications;
    private readonly INotificationUnitOfWork _uow;

    public ApprovalNotificationConsumer(INotificationRepository notifications, INotificationUnitOfWork uow)
    {
        _notifications = notifications;
        _uow = uow;
    }

    public Task HandleAsync(ApprovalSubmitted e, CancellationToken ct)
    {
        var (title, body) = e.Status == "AutoApproved"
            ? ($"{e.DocumentType} auto-approved", "No approval was required; the document was auto-approved.")
            : ($"{e.DocumentType} submitted for approval", "Your document is pending approval.");
        return NotifyAsync(e.SubmittedByUserId, title, body, ct);
    }

    public Task HandleAsync(ApprovalDecided e, CancellationToken ct)
    {
        var (title, body) = e.Approved
            ? ($"{e.DocumentType} {e.Status.ToLowerInvariant()}",
                e.Status == "Approved" ? "Your document was fully approved." : "Your document advanced to the next approval level.")
            : ($"{e.DocumentType} rejected", "Your document was rejected.");
        return NotifyAsync(e.SubmittedByUserId, title, body, ct);
    }

    private async Task NotifyAsync(Guid userId, string title, string body, CancellationToken ct)
    {
        _notifications.Add(Domain.Notification.Create(userId, title, body));
        await _uow.SaveChangesAsync(ct);
    }
}
