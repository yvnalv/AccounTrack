using Accountrack.Application.Abstractions.Messaging;
using Accountrack.ProcessTracker.Application.Abstractions;
using Accountrack.SharedKernel.Results;

namespace Accountrack.ProcessTracker.Application;

public sealed record ProcessEventDto(
    Guid Id, string Milestone, string? Note, Guid? ActorUserId, DateTime OccurredAtUtc);

/// <summary>The lifecycle timeline for a document, oldest first.</summary>
public sealed record GetTimelineQuery(string DocumentType, Guid DocumentId) : IQuery<IReadOnlyList<ProcessEventDto>>;

public sealed class GetTimelineQueryHandler : IQueryHandler<GetTimelineQuery, IReadOnlyList<ProcessEventDto>>
{
    private readonly IProcessTimelineRepository _timeline;

    public GetTimelineQueryHandler(IProcessTimelineRepository timeline) => _timeline = timeline;

    public async Task<Result<IReadOnlyList<ProcessEventDto>>> Handle(GetTimelineQuery request, CancellationToken ct)
    {
        var events = await _timeline.GetTimelineAsync(request.DocumentType, request.DocumentId, ct);
        return Result.Success<IReadOnlyList<ProcessEventDto>>(events
            .Select(e => new ProcessEventDto(e.Id, e.Milestone, e.Note, e.ActorUserId, e.OccurredAtUtc))
            .ToList());
    }
}
