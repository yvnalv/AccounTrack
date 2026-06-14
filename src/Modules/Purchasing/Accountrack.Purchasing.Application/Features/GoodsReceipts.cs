using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Inventory;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.Purchasing.Application.Abstractions;
using Accountrack.Purchasing.Application.Contracts;
using Accountrack.Purchasing.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Purchasing.Application.Features;

public sealed record GoodsReceiptLineInput(Guid PurchaseOrderLineId, decimal Quantity);

/// <summary>
/// Receives goods against an approved purchase order. Atomically (INTEGRATION_EVENTS.md §2): writes
/// the inventory ledger for each line, posts Dr Inventory / Cr GR-IR via posting rules, advances the
/// PO's receipt status, and records the goods receipt — all in one transaction.
/// </summary>
public sealed record PostGoodsReceiptCommand(
    Guid PurchaseOrderId, DateOnly ReceiptDate, string? Notes, IReadOnlyList<GoodsReceiptLineInput> Lines)
    : ICommand<Guid>;

public sealed class PostGoodsReceiptValidator : AbstractValidator<PostGoodsReceiptCommand>
{
    public PostGoodsReceiptValidator()
    {
        RuleFor(x => x.PurchaseOrderId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("A goods receipt requires at least one line.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.PurchaseOrderLineId).NotEmpty();
            l.RuleFor(x => x.Quantity).GreaterThan(0);
        });
    }
}

public sealed class PostGoodsReceiptHandler : ICommandHandler<PostGoodsReceiptCommand, Guid>
{
    private readonly IPurchaseOrderRepository _orders;
    private readonly IGoodsReceiptRepository _receipts;
    private readonly ICrossModuleUnitOfWork _uow;
    private readonly IInventoryPosting _inventory;
    private readonly IGeneralLedgerPoster _ledger;
    private readonly IPostingAccountResolver _accounts;

    public PostGoodsReceiptHandler(
        IPurchaseOrderRepository orders,
        IGoodsReceiptRepository receipts,
        ICrossModuleUnitOfWork uow,
        IInventoryPosting inventory,
        IGeneralLedgerPoster ledger,
        IPostingAccountResolver accounts)
    {
        _orders = orders;
        _receipts = receipts;
        _uow = uow;
        _inventory = inventory;
        _ledger = ledger;
        _accounts = accounts;
    }

    public Task<Result<Guid>> Handle(PostGoodsReceiptCommand request, CancellationToken ct) =>
        _uow.ExecuteAsync(token => PostAsync(request, token), ct);

    private async Task<Result<Guid>> PostAsync(PostGoodsReceiptCommand request, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(request.PurchaseOrderId, ct);
        if (order is null)
        {
            return PurchasingErrors.NotFound;
        }

        if (!order.CanReceive)
        {
            return PurchasingErrors.NotReceivable;
        }

        var sequence = await _receipts.GetSequenceAsync(ct);
        if (sequence is null)
        {
            sequence = new GoodsReceiptNumberSequence();
            _receipts.AddSequence(sequence);
        }

        var number = sequence.Take(request.ReceiptDate);
        var receipt = GoodsReceipt.Create(
            number, order.Id, order.SupplierId, order.WarehouseId, order.Currency, request.ReceiptDate, request.Notes);

        decimal totalCost = 0m;
        foreach (var input in request.Lines)
        {
            var line = order.Lines.FirstOrDefault(l => l.Id == input.PurchaseOrderLineId);
            if (line is null)
            {
                return PurchasingErrors.PurchaseOrderLineNotFound(input.PurchaseOrderLineId);
            }

            if (input.Quantity > line.OutstandingQuantity)
            {
                return PurchasingErrors.OverReceipt(line.OutstandingQuantity, input.Quantity);
            }

            // Value the receipt at the PO (net) unit price and push it through the inventory ledger.
            var movement = await _inventory.ReceiveAsync(
                line.ProductId, order.WarehouseId, order.Currency, input.Quantity, line.UnitPrice,
                request.ReceiptDate, receipt.Id, $"Goods receipt {number}", ct);

            if (movement.IsFailure)
            {
                return movement.Error;
            }

            receipt.AddLine(line.Id, line.ProductId, input.Quantity, line.UnitPrice);
            order.ReceiveLine(line.Id, input.Quantity);
            totalCost += movement.Value.CostApplied;
        }

        // Resolve accounts (configuration, not hardcoded) and post Dr Inventory / Cr GR-IR.
        var inventoryAccount = await _accounts.ResolveAsync(
            "GoodsReceipt", PostingKeys.Inventory, new PostingSelector(WarehouseId: order.WarehouseId), ct);
        if (inventoryAccount.IsFailure)
        {
            return inventoryAccount.Error;
        }

        var clearingAccount = await _accounts.ResolveAsync(
            "GoodsReceipt", PostingKeys.GrIrClearing, PostingSelector.None, ct);
        if (clearingAccount.IsFailure)
        {
            return clearingAccount.Error;
        }

        var posting = new LedgerPostingRequest(
            request.ReceiptDate, LedgerSource.GoodsReceipt, receipt.Id,
            $"Goods receipt {number} for PO {order.Number}",
            new[]
            {
                new LedgerLine(inventoryAccount.Value, totalCost, 0m, "Inventory received"),
                new LedgerLine(clearingAccount.Value, 0m, totalCost, "GR/IR clearing"),
            });

        var journal = await _ledger.PostAsync(posting, ct);
        if (journal.IsFailure)
        {
            return journal.Error;
        }

        receipt.SetJournal(journal.Value);
        _receipts.Add(receipt);

        return receipt.Id;
    }
}
