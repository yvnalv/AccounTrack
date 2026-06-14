using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Notification.Application.Abstractions;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Notification.Application;

public sealed record NotificationDto(Guid Id, string Title, string Body, bool IsRead, DateTime? ReadAtUtc, DateTime CreatedAt);

/// <summary>The current user's notifications, newest first.</summary>
public sealed record GetMyNotificationsQuery(bool UnreadOnly = false) : IQuery<IReadOnlyList<NotificationDto>>;

public sealed class GetMyNotificationsHandler : IQueryHandler<GetMyNotificationsQuery, IReadOnlyList<NotificationDto>>
{
    private readonly INotificationRepository _notifications;
    private readonly ICurrentUser _user;

    public GetMyNotificationsHandler(INotificationRepository notifications, ICurrentUser user)
    {
        _notifications = notifications;
        _user = user;
    }

    public async Task<Result<IReadOnlyList<NotificationDto>>> Handle(GetMyNotificationsQuery request, CancellationToken ct)
    {
        var items = await _notifications.ListForUserAsync(_user.UserId, request.UnreadOnly, ct);
        return Result.Success<IReadOnlyList<NotificationDto>>(items
            .Select(n => new NotificationDto(n.Id, n.Title, n.Body, n.IsRead, n.ReadAtUtc, n.CreatedAt))
            .ToList());
    }
}

public sealed record MarkNotificationReadCommand(Guid Id) : ICommand;

public sealed class MarkNotificationReadHandler : ICommandHandler<MarkNotificationReadCommand>
{
    private readonly INotificationRepository _notifications;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly INotificationUnitOfWork _uow;

    public MarkNotificationReadHandler(
        INotificationRepository notifications, ICurrentUser user, IClock clock, INotificationUnitOfWork uow)
    {
        _notifications = notifications;
        _user = user;
        _clock = clock;
        _uow = uow;
    }

    public async Task<Result> Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        var notification = await _notifications.GetByIdAsync(request.Id, ct);
        // Not found, or belongs to another user — same response (no enumeration).
        if (notification is null || notification.UserId != _user.UserId)
        {
            return Error.NotFound("NOTIFICATION.NOT_FOUND", "Notification not found.");
        }

        notification.MarkRead(_clock.UtcNow);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
