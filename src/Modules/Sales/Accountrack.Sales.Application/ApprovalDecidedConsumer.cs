using Accountrack.Application.Abstractions.Integration;
using Accountrack.Modules.Contracts.Events;
using Accountrack.Sales.Application.Abstractions;
using Accountrack.Sales.Application.Features;

namespace Accountrack.Sales.Application;

/// <summary>
/// Advances a sales order's status when its approval is decided (event-driven, ADR-0007).
/// Best-effort eventual consumer.
/// </summary>
public sealed class ApprovalDecidedConsumer : IIntegrationEventHandler<ApprovalDecided>
{
    private readonly ISalesOrderRepository _orders;
    private readonly ISalesUnitOfWork _uow;

    public ApprovalDecidedConsumer(ISalesOrderRepository orders, ISalesUnitOfWork uow)
    {
        _orders = orders;
        _uow = uow;
    }

    public async Task HandleAsync(ApprovalDecided e, CancellationToken ct)
    {
        if (e.DocumentType != SalesDocumentTypes.SalesOrder)
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
