using Accountrack.Application.Abstractions.Integration;
using Accountrack.Modules.Contracts.Events;
using Accountrack.Purchasing.Application.Abstractions;
using Accountrack.Purchasing.Application.Features;

namespace Accountrack.Purchasing.Application;

/// <summary>
/// Advances a purchase order's status when its approval is decided (event-driven, ADR-0007).
/// Best-effort eventual consumer.
/// </summary>
public sealed class ApprovalDecidedConsumer : IIntegrationEventHandler<ApprovalDecided>
{
    private readonly IPurchaseOrderRepository _orders;
    private readonly IPurchasingUnitOfWork _uow;

    public ApprovalDecidedConsumer(IPurchaseOrderRepository orders, IPurchasingUnitOfWork uow)
    {
        _orders = orders;
        _uow = uow;
    }

    public async Task HandleAsync(ApprovalDecided e, CancellationToken ct)
    {
        if (e.DocumentType != PurchasingDocumentTypes.PurchaseOrder)
        {
            return;
        }

        var order = await _orders.GetByIdAsync(e.DocumentId, ct);
        if (order is null)
        {
            return;
        }

        if (e.Approved && e.Status == "Approved")
        {
            order.MarkApproved();
        }
        else if (!e.Approved)
        {
            order.MarkRejected();
        }
        else
        {
            return; // multi-level advance — no status change yet
        }

        await _uow.SaveChangesAsync(ct);
    }
}
