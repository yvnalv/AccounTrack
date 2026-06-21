using Accountrack.Application.Abstractions.Context;
using Accountrack.Modules.Contracts.Approval;
using Accountrack.Modules.Contracts.Company;
using Accountrack.Modules.Contracts.Events;
using Accountrack.Modules.Contracts.MasterData;
using Accountrack.Sales.Application;
using Accountrack.Sales.Application.Abstractions;
using Accountrack.Sales.Application.Features;
using Accountrack.Sales.Domain;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Sales.UnitTests;

public class SalesHandlerTests
{
    private static readonly DateOnly Date = new(2026, 6, 16);
    private static readonly Guid CompanyId = Guid.NewGuid();

    private readonly ISalesOrderRepository _orders = Substitute.For<ISalesOrderRepository>();
    private readonly IMasterDataLookup _masterData = Substitute.For<IMasterDataLookup>();
    private readonly ICompanyDirectory _companies = Substitute.For<ICompanyDirectory>();
    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();
    private readonly IApprovalService _approval = Substitute.For<IApprovalService>();
    private readonly ISalesUnitOfWork _uow = Substitute.For<ISalesUnitOfWork>();

    public SalesHandlerTests()
    {
        _tenant.CompanyId.Returns(CompanyId);
        _companies.GetAsync(CompanyId, Arg.Any<CancellationToken>()).Returns(new CompanyInfo(CompanyId, "DEV", "IDR", 1));
        _masterData.CustomerExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _masterData.WarehouseExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _masterData.ProductExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    private static SalesOrder DraftWithLine()
    {
        var so = SalesOrder.CreateDraft("SO/202606/00001", Guid.NewGuid(), Guid.NewGuid(), "IDR", Date, null);
        so.AddLine(Guid.NewGuid(), 1m, 100m, 0.11m, null);
        return so;
    }

    [Fact]
    public async Task Create_validates_references_and_persists()
    {
        SalesOrder? added = null;
        _orders.When(r => r.Add(Arg.Any<SalesOrder>())).Do(ci => added = ci.Arg<SalesOrder>());

        var handler = new CreateSalesOrderHandler(_orders, _masterData, _companies, _tenant, _uow);
        var result = await handler.Handle(
            new CreateSalesOrderCommand(Guid.NewGuid(), Guid.NewGuid(), Date, null,
                new[] { new CreateSoLine(Guid.NewGuid(), 2m, 100m, 0.11m, null) }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        added.Should().NotBeNull();
        added!.GrandTotal.Should().Be(222m); // 200 + 22 VAT
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_rejects_unknown_customer()
    {
        _masterData.CustomerExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var handler = new CreateSalesOrderHandler(_orders, _masterData, _companies, _tenant, _uow);
        var result = await handler.Handle(
            new CreateSalesOrderCommand(Guid.NewGuid(), Guid.NewGuid(), Date, null,
                new[] { new CreateSoLine(Guid.NewGuid(), 1m, 10m, 0m, null) }), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SalesErrors.CustomerNotFound);
    }

    [Fact]
    public async Task Submit_auto_approves_when_no_rule_matches()
    {
        var so = DraftWithLine();
        _orders.GetByIdAsync(so.Id, Arg.Any<CancellationToken>()).Returns(so);
        _approval.SubmitAsync(Arg.Any<string>(), so.Id, Arg.Any<IReadOnlyDictionary<string, decimal>>(), Arg.Any<CancellationToken>())
            .Returns(new ApprovalSubmissionResult(Guid.NewGuid(), "AutoApproved"));

        var handler = new SubmitSalesOrderHandler(_orders, _approval, _uow);
        var result = await handler.Handle(new SubmitSalesOrderCommand(so.Id), CancellationToken.None);

        result.Value.Should().Be("Approved");
        so.Status.Should().Be(SalesOrderStatus.Approved);
    }

    [Fact]
    public async Task Approval_decided_event_advances_to_approved()
    {
        var so = DraftWithLine();
        so.MarkPendingApproval(Guid.NewGuid());
        _orders.GetByIdAsync(so.Id, Arg.Any<CancellationToken>()).Returns(so);

        var consumer = new ApprovalDecidedConsumer(_orders, _uow);
        await consumer.HandleAsync(
            new ApprovalDecided("SalesOrder", so.Id, Guid.NewGuid(), "Approved", Guid.NewGuid(), Guid.NewGuid(), true),
            CancellationToken.None);

        so.Status.Should().Be(SalesOrderStatus.Approved);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approval_decided_for_other_document_type_is_ignored()
    {
        var consumer = new ApprovalDecidedConsumer(_orders, _uow);
        await consumer.HandleAsync(
            new ApprovalDecided("PurchaseOrder", Guid.NewGuid(), Guid.NewGuid(), "Approved", Guid.NewGuid(), Guid.NewGuid(), true),
            CancellationToken.None);

        await _orders.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_marks_a_draft_order_cancelled()
    {
        var so = DraftWithLine();
        _orders.GetByIdAsync(so.Id, Arg.Any<CancellationToken>()).Returns(so);

        var result = await new CancelSalesOrderHandler(_orders, _uow).Handle(new CancelSalesOrderCommand(so.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        so.Status.Should().Be(SalesOrderStatus.Cancelled);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_an_approved_order_returns_not_cancellable()
    {
        var so = DraftWithLine();
        so.MarkAutoApproved(Guid.NewGuid());
        _orders.GetByIdAsync(so.Id, Arg.Any<CancellationToken>()).Returns(so);

        var result = await new CancelSalesOrderHandler(_orders, _uow).Handle(new CancelSalesOrderCommand(so.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("SALES.NOT_CANCELLABLE");
        so.Status.Should().Be(SalesOrderStatus.Approved);
    }
}
