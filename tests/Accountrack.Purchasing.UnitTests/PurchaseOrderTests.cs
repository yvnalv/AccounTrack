using Accountrack.Purchasing.Domain;
using FluentAssertions;
using Xunit;

namespace Accountrack.Purchasing.UnitTests;

public class PurchaseOrderTests
{
    private static readonly DateOnly Date = new(2026, 6, 14);

    private static PurchaseOrder Draft() =>
        PurchaseOrder.CreateDraft("PO/202606/00001", Guid.NewGuid(), Guid.NewGuid(), "IDR", Date, null);

    [Fact]
    public void Totals_sum_lines_with_tax()
    {
        var po = Draft();
        po.AddLine(Guid.NewGuid(), quantity: 10, unitPrice: 100, taxRate: 0.11m, "A"); // sub 1000, tax 110
        po.AddLine(Guid.NewGuid(), quantity: 2, unitPrice: 50, taxRate: 0m, "B");       // sub 100, tax 0

        po.SubTotal.Should().Be(1100m);
        po.TaxTotal.Should().Be(110m);
        po.GrandTotal.Should().Be(1210m);
    }

    [Fact]
    public void Submit_to_pending_then_approved()
    {
        var po = Draft();
        po.AddLine(Guid.NewGuid(), 1, 100, 0m, null);
        var req = Guid.NewGuid();

        po.MarkPendingApproval(req);
        po.Status.Should().Be(PurchaseOrderStatus.PendingApproval);
        po.ApprovalRequestId.Should().Be(req);

        po.MarkApproved();
        po.Status.Should().Be(PurchaseOrderStatus.Approved);
    }

    [Fact]
    public void Auto_approve_sets_approved_directly()
    {
        var po = Draft();
        po.AddLine(Guid.NewGuid(), 1, 100, 0m, null);
        po.MarkAutoApproved(Guid.NewGuid());
        po.Status.Should().Be(PurchaseOrderStatus.Approved);
    }

    [Fact]
    public void Rejected_from_pending()
    {
        var po = Draft();
        po.AddLine(Guid.NewGuid(), 1, 100, 0m, null);
        po.MarkPendingApproval(Guid.NewGuid());
        po.MarkRejected();
        po.Status.Should().Be(PurchaseOrderStatus.Rejected);
    }

    [Fact]
    public void Cannot_submit_without_lines() =>
        FluentActions.Invoking(() => Draft().MarkPendingApproval(Guid.NewGuid()))
            .Should().Throw<InvalidOperationException>();

    [Fact]
    public void Cannot_add_lines_after_submit()
    {
        var po = Draft();
        po.AddLine(Guid.NewGuid(), 1, 100, 0m, null);
        po.MarkPendingApproval(Guid.NewGuid());
        FluentActions.Invoking(() => po.AddLine(Guid.NewGuid(), 1, 50, 0m, null))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Sequence_formats_and_increments()
    {
        var seq = new PurchaseOrderNumberSequence();
        seq.Take(Date).Should().Be("PO/202606/00001");
        seq.Take(Date).Should().Be("PO/202606/00002");
    }
}
