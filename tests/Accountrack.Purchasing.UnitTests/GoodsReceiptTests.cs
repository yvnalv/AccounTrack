using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Inventory;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.Purchasing.Application.Abstractions;
using Accountrack.Purchasing.Application.Features;
using Accountrack.Purchasing.Domain;
using Accountrack.SharedKernel.Results;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Purchasing.UnitTests;

public class GoodsReceiptTests
{
    private static readonly DateOnly Date = new(2026, 6, 14);

    // A unit of work that simply runs the work (success path) so the orchestration can be tested
    // without a database; real atomic commit/rollback is covered end-to-end.
    private sealed class DirectUnitOfWork : ICrossModuleUnitOfWork
    {
        public Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken ct) =>
            work(ct);
    }

    private readonly IPurchaseOrderRepository _orders = Substitute.For<IPurchaseOrderRepository>();
    private readonly IGoodsReceiptRepository _receipts = Substitute.For<IGoodsReceiptRepository>();
    private readonly IInventoryPosting _inventory = Substitute.For<IInventoryPosting>();
    private readonly IGeneralLedgerPoster _ledger = Substitute.For<IGeneralLedgerPoster>();
    private readonly IPostingAccountResolver _accounts = Substitute.For<IPostingAccountResolver>();

    private static PurchaseOrder ApprovedOrder(decimal qty = 10m, decimal unitPrice = 100m)
    {
        var po = PurchaseOrder.CreateDraft("PO/202606/00001", Guid.NewGuid(), Guid.NewGuid(), "IDR", Date, null);
        po.AddLine(Guid.NewGuid(), qty, unitPrice, 0.11m, null);
        po.MarkAutoApproved(Guid.NewGuid());
        return po;
    }

    private PostGoodsReceiptHandler Handler() =>
        new(_orders, _receipts, new DirectUnitOfWork(), _inventory, _ledger, _accounts);

    // --- Domain ---

    [Fact]
    public void Receiving_part_of_a_line_marks_the_order_partially_received()
    {
        var po = ApprovedOrder(qty: 10m);
        po.ReceiveLine(po.Lines[0].Id, 4m);

        po.Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);
        po.Lines[0].ReceivedQuantity.Should().Be(4m);
        po.Lines[0].OutstandingQuantity.Should().Be(6m);
    }

    [Fact]
    public void Receiving_the_full_quantity_marks_the_order_received()
    {
        var po = ApprovedOrder(qty: 10m);
        po.ReceiveLine(po.Lines[0].Id, 10m);

        po.Status.Should().Be(PurchaseOrderStatus.Received);
        po.Lines[0].IsFullyReceived.Should().BeTrue();
    }

    [Fact]
    public void Over_receiving_a_line_is_rejected()
    {
        var po = ApprovedOrder(qty: 10m);
        var act = () => po.ReceiveLine(po.Lines[0].Id, 11m);
        act.Should().Throw<InvalidOperationException>().WithMessage("*exceeds*");
    }

    // --- Handler orchestration ---

    [Fact]
    public async Task Posting_a_receipt_moves_stock_posts_the_journal_and_advances_the_po()
    {
        var po = ApprovedOrder(qty: 10m, unitPrice: 100m);
        var lineId = po.Lines[0].Id;
        _orders.GetByIdAsync(po.Id, Arg.Any<CancellationToken>()).Returns(po);
        _inventory.ReceiveAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), "IDR", 4m, 100m, Date, Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new StockMovementResult(Guid.NewGuid(), 400m, 4m, 100m));
        _accounts.ResolveAsync("GoodsReceipt", PostingKeys.Inventory, Arg.Any<PostingSelector>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());
        _accounts.ResolveAsync("GoodsReceipt", PostingKeys.GrIrClearing, Arg.Any<PostingSelector>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());
        var journalId = Guid.NewGuid();
        _ledger.PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>()).Returns(journalId);

        GoodsReceipt? captured = null;
        _receipts.When(r => r.Add(Arg.Any<GoodsReceipt>())).Do(ci => captured = ci.Arg<GoodsReceipt>());

        var result = await Handler().Handle(
            new PostGoodsReceiptCommand(po.Id, Date, "first delivery",
                new[] { new GoodsReceiptLineInput(lineId, 4m) }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        po.Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);
        captured.Should().NotBeNull();
        captured!.TotalCost.Should().Be(400m);
        captured.JournalEntryId.Should().Be(journalId);

        // The journal must be a balanced Dr Inventory / Cr GR-IR for the received cost.
        await _ledger.Received(1).PostAsync(
            Arg.Is<LedgerPostingRequest>(r =>
                r.Lines.Count == 2 &&
                r.Lines.Sum(l => l.Debit) == 400m &&
                r.Lines.Sum(l => l.Credit) == 400m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Over_receipt_fails_before_touching_inventory_or_the_ledger()
    {
        var po = ApprovedOrder(qty: 10m);
        var lineId = po.Lines[0].Id;
        _orders.GetByIdAsync(po.Id, Arg.Any<CancellationToken>()).Returns(po);

        var result = await Handler().Handle(
            new PostGoodsReceiptCommand(po.Id, Date, null,
                new[] { new GoodsReceiptLineInput(lineId, 20m) }), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PURCHASING.OVER_RECEIPT");
        await _inventory.DidNotReceive().ReceiveAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<decimal>(),
            Arg.Any<DateOnly>(), Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _ledger.DidNotReceive().PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_non_approved_order_cannot_be_received()
    {
        var po = PurchaseOrder.CreateDraft("PO/202606/00002", Guid.NewGuid(), Guid.NewGuid(), "IDR", Date, null);
        po.AddLine(Guid.NewGuid(), 5m, 50m, 0m, null); // still Draft
        _orders.GetByIdAsync(po.Id, Arg.Any<CancellationToken>()).Returns(po);

        var result = await Handler().Handle(
            new PostGoodsReceiptCommand(po.Id, Date, null,
                new[] { new GoodsReceiptLineInput(po.Lines[0].Id, 1m) }), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PurchasingErrors.NotReceivable);
    }
}
