using Accountrack.Modules.Contracts.Approval;
using Accountrack.Modules.Contracts.Events;
using Accountrack.Modules.Contracts.MasterData;
using Accountrack.Purchasing.Application;
using Accountrack.Purchasing.Application.Abstractions;
using Accountrack.Purchasing.Application.Features;
using Accountrack.Purchasing.Domain;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Purchasing.UnitTests;

public class PurchasingHandlerTests
{
    private static readonly DateOnly Date = new(2026, 6, 14);
    private readonly IPurchaseOrderRepository _orders = Substitute.For<IPurchaseOrderRepository>();
    private readonly IApprovalService _approval = Substitute.For<IApprovalService>();
    private readonly IMasterDataLookup _masterData = Substitute.For<IMasterDataLookup>();
    private readonly IPurchasingUnitOfWork _uow = Substitute.For<IPurchasingUnitOfWork>();

    private static PurchaseOrder DraftWithLine()
    {
        var po = PurchaseOrder.CreateDraft("PO/202606/00001", Guid.NewGuid(), Guid.NewGuid(), "IDR", Date, null);
        po.AddLine(Guid.NewGuid(), 1, 100, 0m, null);
        return po;
    }

    [Fact]
    public async Task Update_sets_expected_version_when_row_version_supplied()
    {
        var po = DraftWithLine();
        _orders.GetByIdAsync(po.Id, Arg.Any<CancellationToken>()).Returns(po);
        _masterData.SupplierExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _masterData.WarehouseExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _masterData.ProductExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        var version = new byte[] { 9, 8, 7 };

        var handler = new UpdatePurchaseOrderHandler(_orders, _masterData, _uow);
        var result = await handler.Handle(
            new UpdatePurchaseOrderCommand(po.Id, Guid.NewGuid(), Guid.NewGuid(), Date, null,
                new[] { new CreatePoLine(Guid.NewGuid(), 1m, 50m, 0m, null) }, version), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _orders.Received(1).SetExpectedVersion(po, version);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_skips_version_check_when_none_supplied()
    {
        var po = DraftWithLine();
        _orders.GetByIdAsync(po.Id, Arg.Any<CancellationToken>()).Returns(po);
        _masterData.SupplierExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _masterData.WarehouseExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _masterData.ProductExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);

        var handler = new UpdatePurchaseOrderHandler(_orders, _masterData, _uow);
        var result = await handler.Handle(
            new UpdatePurchaseOrderCommand(po.Id, Guid.NewGuid(), Guid.NewGuid(), Date, null,
                new[] { new CreatePoLine(Guid.NewGuid(), 1m, 50m, 0m, null) }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _orders.DidNotReceive().SetExpectedVersion(Arg.Any<PurchaseOrder>(), Arg.Any<byte[]>());
    }

    [Fact]
    public async Task Submit_with_matching_rule_goes_pending()
    {
        var po = DraftWithLine();
        _orders.GetByIdAsync(po.Id, Arg.Any<CancellationToken>()).Returns(po);
        _approval.SubmitAsync("PurchaseOrder", po.Id, Arg.Any<IReadOnlyDictionary<string, decimal>>(), Arg.Any<CancellationToken>())
            .Returns(new ApprovalSubmissionResult(Guid.NewGuid(), "Pending"));

        var handler = new SubmitPurchaseOrderHandler(_orders, _approval, _uow);
        var result = await handler.Handle(new SubmitPurchaseOrderCommand(po.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("PendingApproval");
        po.Status.Should().Be(PurchaseOrderStatus.PendingApproval);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_with_no_rule_auto_approves()
    {
        var po = DraftWithLine();
        _orders.GetByIdAsync(po.Id, Arg.Any<CancellationToken>()).Returns(po);
        _approval.SubmitAsync(Arg.Any<string>(), po.Id, Arg.Any<IReadOnlyDictionary<string, decimal>>(), Arg.Any<CancellationToken>())
            .Returns(new ApprovalSubmissionResult(Guid.NewGuid(), "AutoApproved"));

        var handler = new SubmitPurchaseOrderHandler(_orders, _approval, _uow);
        var result = await handler.Handle(new SubmitPurchaseOrderCommand(po.Id), CancellationToken.None);

        result.Value.Should().Be("Approved");
        po.Status.Should().Be(PurchaseOrderStatus.Approved);
    }

    [Fact]
    public async Task Submit_rejects_a_non_draft_order()
    {
        var po = DraftWithLine();
        po.MarkAutoApproved(Guid.NewGuid()); // now Approved, not Draft
        _orders.GetByIdAsync(po.Id, Arg.Any<CancellationToken>()).Returns(po);

        var handler = new SubmitPurchaseOrderHandler(_orders, _approval, _uow);
        var result = await handler.Handle(new SubmitPurchaseOrderCommand(po.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PurchasingErrors.NotDraft);
    }

    [Fact]
    public async Task Approval_decided_event_advances_po_to_approved()
    {
        var po = DraftWithLine();
        po.MarkPendingApproval(Guid.NewGuid());
        _orders.GetByIdAsync(po.Id, Arg.Any<CancellationToken>()).Returns(po);

        var consumer = new ApprovalDecidedConsumer(_orders, _uow);
        await consumer.HandleAsync(
            new ApprovalDecided("PurchaseOrder", po.Id, Guid.NewGuid(), "Approved", Guid.NewGuid(), Guid.NewGuid(), true),
            CancellationToken.None);

        po.Status.Should().Be(PurchaseOrderStatus.Approved);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approval_decided_event_for_other_document_type_is_ignored()
    {
        var consumer = new ApprovalDecidedConsumer(_orders, _uow);
        await consumer.HandleAsync(
            new ApprovalDecided("Expense", Guid.NewGuid(), Guid.NewGuid(), "Approved", Guid.NewGuid(), Guid.NewGuid(), true),
            CancellationToken.None);

        await _orders.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_marks_a_draft_order_cancelled()
    {
        var po = DraftWithLine();
        _orders.GetByIdAsync(po.Id, Arg.Any<CancellationToken>()).Returns(po);

        var result = await new CancelPurchaseOrderHandler(_orders, _uow).Handle(new CancelPurchaseOrderCommand(po.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        po.Status.Should().Be(PurchaseOrderStatus.Cancelled);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_an_approved_order_returns_not_cancellable()
    {
        var po = DraftWithLine();
        po.MarkAutoApproved(Guid.NewGuid());
        _orders.GetByIdAsync(po.Id, Arg.Any<CancellationToken>()).Returns(po);

        var result = await new CancelPurchaseOrderHandler(_orders, _uow).Handle(new CancelPurchaseOrderCommand(po.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PURCHASING.NOT_CANCELLABLE");
    }

    [Fact]
    public async Task Update_edits_a_draft_orders_lines()
    {
        var po = DraftWithLine();
        _orders.GetByIdAsync(po.Id, Arg.Any<CancellationToken>()).Returns(po);
        _masterData.SupplierExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _masterData.WarehouseExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _masterData.ProductExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);

        var cmd = new UpdatePurchaseOrderCommand(
            po.Id, Guid.NewGuid(), Guid.NewGuid(), Date, "edited",
            new[] { new CreatePoLine(Guid.NewGuid(), 4m, 250m, 0.11m, null) });
        var result = await new UpdatePurchaseOrderHandler(_orders, _masterData, _uow).Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        po.Notes.Should().Be("edited");
        po.Lines.Should().ContainSingle(l => l.Quantity == 4m && l.UnitPrice == 250m);
    }
}
