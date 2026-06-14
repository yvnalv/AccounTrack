using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Integration;
using Accountrack.Modules.Contracts.Events;
using Accountrack.ProcessTracker.Application.Abstractions;
using Accountrack.ProcessTracker.Domain;

namespace Accountrack.ProcessTracker.Application;

/// <summary>
/// Appends lifecycle milestones to a document's timeline when approval events occur
/// (WORKFLOW_APPROVAL.md §6). A best-effort, eventual consumer (ADR-0007).
/// </summary>
public sealed class ApprovalProcessConsumer
    : IIntegrationEventHandler<ApprovalSubmitted>, IIntegrationEventHandler<ApprovalDecided>
{
    private readonly IProcessTimelineRepository _timeline;
    private readonly IProcessTrackerUnitOfWork _uow;
    private readonly IClock _clock;

    public ApprovalProcessConsumer(IProcessTimelineRepository timeline, IProcessTrackerUnitOfWork uow, IClock clock)
    {
        _timeline = timeline;
        _uow = uow;
        _clock = clock;
    }

    public Task HandleAsync(ApprovalSubmitted e, CancellationToken ct)
    {
        var milestone = e.Status == "AutoApproved" ? "Auto-approved" : "Submitted for approval";
        return RecordAsync(e.DocumentType, e.DocumentId, milestone, e.SubmittedByUserId, ct);
    }

    public Task HandleAsync(ApprovalDecided e, CancellationToken ct)
    {
        var milestone = !e.Approved ? "Rejected"
            : e.Status == "Approved" ? "Approved"
            : "Approval advanced";
        return RecordAsync(e.DocumentType, e.DocumentId, milestone, e.DecidedByUserId, ct);
    }

    private async Task RecordAsync(string documentType, Guid documentId, string milestone, Guid actor, CancellationToken ct)
    {
        _timeline.Add(ProcessEvent.Record(documentType, documentId, milestone, note: null, actorUserId: actor, _clock.UtcNow));
        await _uow.SaveChangesAsync(ct);
    }
}
