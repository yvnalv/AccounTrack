using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Application.Features;
using Accountrack.Application.Abstractions.Integration;
using Accountrack.Modules.Contracts.Events;

namespace Accountrack.Accounting.Application;

/// <summary>
/// Posts (or rejects) a manual journal / guided Cash & Bank flow when its approval is decided
/// (event-driven, ADR-0007/0040). On approval it posts the held journal to the GL; on rejection it
/// marks the journal Rejected (it never reaches the ledger). Best-effort eventual consumer — a posting
/// failure leaves the journal awaiting approval for retry (no durable outbox yet).
/// </summary>
public sealed class ApprovalDecidedConsumer : IIntegrationEventHandler<ApprovalDecided>
{
    private readonly IJournalRepository _journals;
    private readonly IJournalPoster _poster;
    private readonly IAccountingUnitOfWork _uow;

    public ApprovalDecidedConsumer(IJournalRepository journals, IJournalPoster poster, IAccountingUnitOfWork uow)
    {
        _journals = journals;
        _poster = poster;
        _uow = uow;
    }

    public async Task HandleAsync(ApprovalDecided e, CancellationToken ct)
    {
        if (e.DocumentType != AccountingDocumentTypes.ManualJournal)
        {
            return;
        }

        var entry = await _journals.GetByIdAsync(e.DocumentId, ct);
        if (entry is null || !entry.IsAwaitingApproval)
        {
            return; // unknown, already posted, or already rejected — idempotent no-op
        }

        if (e.Approved && e.Status == "Approved")
        {
            var posted = await _poster.PostHeldAsync(entry, ct);
            if (posted.IsSuccess)
            {
                await _uow.SaveChangesAsync(ct);
            }
            // On failure (e.g. the period has since closed) leave it awaiting approval for retry.
        }
        else if (!e.Approved)
        {
            entry.MarkRejected();
            await _uow.SaveChangesAsync(ct);
        }
        // else: a multi-level approval advanced but isn't final — nothing to do yet.
    }
}
