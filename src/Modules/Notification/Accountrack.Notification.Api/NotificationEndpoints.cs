using Accountrack.Notification.Application;
using Accountrack.SharedKernel.Results;
using Accountrack.Web.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Accountrack.Notification.Api;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/notifications").WithTags("Notifications").RequireAuthorization();

        group.MapGet("/", (bool? unreadOnly, ISender s, CancellationToken ct) =>
                Send(s.Send(new GetMyNotificationsQuery(unreadOnly ?? false), ct)))
            .WithName("GetMyNotifications");

        group.MapPost("/{id:guid}/read", (Guid id, ISender s, CancellationToken ct) =>
                SendUnit(s.Send(new MarkNotificationReadCommand(id), ct)))
            .WithName("MarkNotificationRead");

        return app;
    }

    private static async Task<IResult> Send<T>(Task<Result<T>> task) => (await task).ToHttpResult();

    private static async Task<IResult> SendUnit(Task<Result> task) => (await task).ToHttpResult();
}
