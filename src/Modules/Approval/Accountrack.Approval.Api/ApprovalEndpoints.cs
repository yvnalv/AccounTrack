using Accountrack.Approval.Application.Features;
using Accountrack.SharedKernel.Results;
using Accountrack.Web.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Accountrack.Approval.Api;

public static class ApprovalEndpoints
{
    public sealed record DecisionRequest(string? Comment);

    public static IEndpointRouteBuilder MapApprovalEndpoints(this IEndpointRouteBuilder app)
    {
        var defs = app.MapGroup("/api/v1/approval-definitions").WithTags("Approval Workflow").RequireAuthorization();

        defs.MapGet("/", (ISender s, CancellationToken ct) => Send(s.Send(new GetApprovalDefinitionsQuery(), ct)))
            .RequireAuthorization("Approval.Manage").WithName("GetApprovalDefinitions");

        defs.MapPost("/", (CreateApprovalDefinitionCommand c, ISender s, CancellationToken ct) =>
                Created(s.Send(c, ct), "/api/v1/approval-definitions"))
            .RequireAuthorization("Approval.Manage").WithName("CreateApprovalDefinition");

        var reqs = app.MapGroup("/api/v1/approval-requests").WithTags("Approval Workflow").RequireAuthorization();

        // Submit is typically called by the owning document module; exposed for testing/integration.
        reqs.MapPost("/", (SubmitForApprovalCommand c, ISender s, CancellationToken ct) =>
                Created(s.Send(c, ct), "/api/v1/approval-requests"))
            .WithName("SubmitForApproval");

        reqs.MapGet("/mine", (ISender s, CancellationToken ct) => Send(s.Send(new GetMyPendingApprovalsQuery(), ct)))
            .WithName("MyPendingApprovals");

        reqs.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => Send(s.Send(new GetApprovalRequestQuery(id), ct)))
            .WithName("GetApprovalRequest");

        reqs.MapPost("/{id:guid}/approve", (Guid id, DecisionRequest body, ISender s, CancellationToken ct) =>
                Send(s.Send(new DecideApprovalCommand(id, true, body.Comment), ct)))
            .WithName("ApproveRequest");

        reqs.MapPost("/{id:guid}/reject", (Guid id, DecisionRequest body, ISender s, CancellationToken ct) =>
                Send(s.Send(new DecideApprovalCommand(id, false, body.Comment), ct)))
            .WithName("RejectRequest");

        // Outbox operations: surface integration events the dispatcher gave up on, and requeue them.
        var outbox = app.MapGroup("/api/v1/approval/outbox").WithTags("Approval Workflow").RequireAuthorization();

        outbox.MapGet("/dead-letter", (ISender s, CancellationToken ct) =>
                Send(s.Send(new GetDeadLetteredEventsQuery(), ct)))
            .RequireAuthorization("Approval.Manage").WithName("GetDeadLetteredEvents");

        outbox.MapPost("/dead-letter/{id:guid}/retry", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new RetryDeadLetteredEventCommand(id), ct)).ToHttpResult())
            .RequireAuthorization("Approval.Manage").WithName("RetryDeadLetteredEvent");

        return app;
    }

    private static async Task<IResult> Send<T>(Task<Result<T>> task) => (await task).ToHttpResult();

    private static async Task<IResult> Created<T>(Task<Result<T>> task, string location) =>
        (await task).ToCreatedResult(location);
}
