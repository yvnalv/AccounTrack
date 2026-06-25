using Accountrack.Application.Abstractions.Integration;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Approval.Application.Abstractions;
using Accountrack.Approval.Application.Contracts;
using Accountrack.Approval.Domain;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Approval.Application.Features;

/// <summary>
/// Lists integration events the outbox dispatcher has given up on (dead-lettered) for the current
/// tenant, so an operator can see what failed and why before requeuing (INTEGRATION_EVENTS.md §5).
/// </summary>
public sealed record GetDeadLetteredEventsQuery : IQuery<IReadOnlyList<DeadLetterEventDto>>;

public sealed class GetDeadLetteredEventsHandler
    : IQueryHandler<GetDeadLetteredEventsQuery, IReadOnlyList<DeadLetterEventDto>>
{
    private readonly IOutboxAdminRepository _outbox;
    public GetDeadLetteredEventsHandler(IOutboxAdminRepository outbox) => _outbox = outbox;

    public async Task<Result<IReadOnlyList<DeadLetterEventDto>>> Handle(
        GetDeadLetteredEventsQuery request, CancellationToken ct)
    {
        var rows = await _outbox.ListDeadLetteredAsync(OutboxDefaults.MaxAttempts, ct);
        var dtos = rows
            .Select(r => new DeadLetterEventDto(r.Id, FriendlyEventName(r.Type), r.OccurredOnUtc, r.Attempts, r.Error))
            .ToList();
        return Result.Success<IReadOnlyList<DeadLetterEventDto>>(dtos);
    }

    /// <summary>Reduces an assembly-qualified name to the bare event type (e.g. "ApprovalDecided").</summary>
    private static string FriendlyEventName(string assemblyQualifiedName)
    {
        var typeName = assemblyQualifiedName.Split(',', 2)[0];
        var lastDot = typeName.LastIndexOf('.');
        return lastDot >= 0 ? typeName[(lastDot + 1)..] : typeName;
    }
}

/// <summary>
/// Requeues a dead-lettered event: resets its attempt count so the dispatcher picks it up again on the
/// next pass. Tenant-scoped; gated by Approval.Manage at the endpoint.
/// </summary>
public sealed record RetryDeadLetteredEventCommand(Guid Id) : ICommand;

public sealed class RetryDeadLetteredEventHandler : ICommandHandler<RetryDeadLetteredEventCommand>
{
    private readonly IOutboxAdminRepository _outbox;
    public RetryDeadLetteredEventHandler(IOutboxAdminRepository outbox) => _outbox = outbox;

    public async Task<Result> Handle(RetryDeadLetteredEventCommand request, CancellationToken ct)
    {
        var requeued = await _outbox.RequeueAsync(request.Id, ct);
        return requeued ? Result.Success() : ApprovalErrors.OutboxMessageNotFound;
    }
}
