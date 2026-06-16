using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Inventory;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.Sales.Application.Abstractions;
using Accountrack.Sales.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Sales.Application.Features;

public sealed record DeliveryOrderLineInput(Guid SalesOrderLineId, decimal Quantity);

/// <summary>
/// Ships goods against an approved sales order. Atomically (INTEGRATION_EVENTS.md §2): issues stock
/// for each line at moving-average cost, posts Dr COGS / Cr Inventory at the issue cost, advances the
/// SO's delivery status, and records the delivery order.
/// </summary>
public sealed record PostDeliveryOrderCommand(
    Guid SalesOrderId, DateOnly DeliveryDate, string? Notes, IReadOnlyList<DeliveryOrderLineInput> Lines)
    : ICommand<Guid>;

public sealed class PostDeliveryOrderValidator : AbstractValidator<PostDeliveryOrderCommand>
{
    public PostDeliveryOrderValidator()
    {
        RuleFor(x => x.SalesOrderId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("A delivery order requires at least one line.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.SalesOrderLineId).NotEmpty();
            l.RuleFor(x => x.Quantity).GreaterThan(0);
        });
    }
}

public sealed class PostDeliveryOrderHandler : ICommandHandler<PostDeliveryOrderCommand, Guid>
{
    private readonly ISalesOrderRepository _orders;
    private readonly IDeliveryOrderRepository _deliveries;
    private readonly ICrossModuleUnitOfWork _uow;
    private readonly IInventoryPosting _inventory;
    private readonly IGeneralLedgerPoster _ledger;
    private readonly IPostingAccountResolver _accounts;

    public PostDeliveryOrderHandler(
        ISalesOrderRepository orders,
        IDeliveryOrderRepository deliveries,
        ICrossModuleUnitOfWork uow,
        IInventoryPosting inventory,
        IGeneralLedgerPoster ledger,
        IPostingAccountResolver accounts)
    {
        _orders = orders;
        _deliveries = deliveries;
        _uow = uow;
        _inventory = inventory;
        _ledger = ledger;
        _accounts = accounts;
    }

    public Task<Result<Guid>> Handle(PostDeliveryOrderCommand request, CancellationToken ct) =>
        _uow.ExecuteAsync(token => PostAsync(request, token), ct);

    private async Task<Result<Guid>> PostAsync(PostDeliveryOrderCommand request, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(request.SalesOrderId, ct);
        if (order is null)
        {
            return SalesErrors.NotFound;
        }

        if (!order.CanDeliver)
        {
            return SalesErrors.NotDeliverable;
        }

        var sequence = await _deliveries.GetSequenceAsync(ct);
        if (sequence is null)
        {
            sequence = new DeliveryOrderNumberSequence();
            _deliveries.AddSequence(sequence);
        }

        var number = sequence.Take(request.DeliveryDate);
        var delivery = DeliveryOrder.Create(
            number, order.Id, order.CustomerId, order.WarehouseId, order.Currency, request.DeliveryDate, request.Notes);

        decimal totalCost = 0m;
        foreach (var input in request.Lines)
        {
            var line = order.Lines.FirstOrDefault(l => l.Id == input.SalesOrderLineId);
            if (line is null)
            {
                return SalesErrors.SalesOrderLineNotFound(input.SalesOrderLineId);
            }

            if (input.Quantity > line.OutstandingQuantity)
            {
                return SalesErrors.OverDelivery(line.OutstandingQuantity, input.Quantity);
            }

            // Issue stock at moving-average cost; the cost applied becomes COGS.
            var movement = await _inventory.IssueAsync(
                line.ProductId, order.WarehouseId, input.Quantity, request.DeliveryDate, delivery.Id,
                $"Delivery {number}", allowNegative: false, ct);

            if (movement.IsFailure)
            {
                return movement.Error;
            }

            var unitCost = input.Quantity == 0 ? 0m : movement.Value.CostApplied / input.Quantity;
            delivery.AddLine(line.Id, line.ProductId, input.Quantity, unitCost);
            order.DeliverLine(line.Id, input.Quantity);
            totalCost += movement.Value.CostApplied;
        }

        // Resolve accounts (configuration, not hardcoded) and post Dr COGS / Cr Inventory.
        var cogs = await _accounts.ResolveAsync("GoodsShipment", PostingKeys.Cogs, PostingSelector.None, ct);
        if (cogs.IsFailure)
        {
            return cogs.Error;
        }

        var inventoryAccount = await _accounts.ResolveAsync(
            "GoodsShipment", PostingKeys.Inventory, new PostingSelector(WarehouseId: order.WarehouseId), ct);
        if (inventoryAccount.IsFailure)
        {
            return inventoryAccount.Error;
        }

        var posting = new LedgerPostingRequest(
            request.DeliveryDate, LedgerSource.Shipment, delivery.Id,
            $"Delivery {number} for SO {order.Number}",
            new[]
            {
                new LedgerLine(cogs.Value, totalCost, 0m, "Cost of goods sold"),
                new LedgerLine(inventoryAccount.Value, 0m, totalCost, "Inventory shipped"),
            });

        var journal = await _ledger.PostAsync(posting, ct);
        if (journal.IsFailure)
        {
            return journal.Error;
        }

        delivery.SetJournal(journal.Value);
        _deliveries.Add(delivery);

        return delivery.Id;
    }
}
