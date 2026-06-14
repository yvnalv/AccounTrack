using Accountrack.SharedKernel.Domain;

namespace Accountrack.Notification.Domain;

/// <summary>
/// An in-app notification targeted at a user (Notification module). Email delivery is a future
/// channel; this slice is in-app only.
/// </summary>
public sealed class Notification : TenantOwnedEntity, IAggregateRoot
{
    private Notification() { }

    private Notification(Guid userId, string title, string body)
    {
        UserId = userId;
        Title = title;
        Body = body;
    }

    public Guid UserId { get; private set; }
    public string Title { get; private set; } = default!;
    public string Body { get; private set; } = default!;
    public DateTime? ReadAtUtc { get; private set; }

    public bool IsRead => ReadAtUtc is not null;

    public static Notification Create(Guid userId, string title, string body) =>
        new(userId, title.Trim(), body.Trim());

    public void MarkRead(DateTime nowUtc) => ReadAtUtc ??= nowUtc;
}
