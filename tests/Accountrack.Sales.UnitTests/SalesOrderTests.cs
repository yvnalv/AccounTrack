using Accountrack.Sales.Domain;
using FluentAssertions;
using Xunit;

namespace Accountrack.Sales.UnitTests;

public class SalesOrderTests
{
    private static readonly DateOnly Date = new(2026, 6, 16);

    private static SalesOrder Draft()
    {
        var so = SalesOrder.CreateDraft("SO/202606/00001", Guid.NewGuid(), Guid.NewGuid(), "idr", Date, " note ");
        so.AddLine(Guid.NewGuid(), 3m, 100m, 0.11m, "widget");
        return so;
    }

    [Fact]
    public void Totals_are_computed_from_lines()
    {
        var so = Draft();
        so.AddLine(Guid.NewGuid(), 2m, 50m, 0m, null);

        // line1: 3*100=300 +33 tax; line2: 2*50=100 +0 tax
        so.SubTotal.Should().Be(400m);
        so.TaxTotal.Should().Be(33m);
        so.GrandTotal.Should().Be(433m);
        so.Currency.Should().Be("IDR");
    }

    [Fact]
    public void Submit_pending_then_approved_transitions()
    {
        var so = Draft();
        so.MarkPendingApproval(Guid.NewGuid());
        so.Status.Should().Be(SalesOrderStatus.PendingApproval);

        so.MarkApproved();
        so.Status.Should().Be(SalesOrderStatus.Approved);
    }

    [Fact]
    public void Auto_approved_from_draft()
    {
        var so = Draft();
        so.MarkAutoApproved(Guid.NewGuid());
        so.Status.Should().Be(SalesOrderStatus.Approved);
    }

    [Fact]
    public void Lines_cannot_be_added_after_submission()
    {
        var so = Draft();
        so.MarkPendingApproval(Guid.NewGuid());

        var act = () => so.AddLine(Guid.NewGuid(), 1m, 10m, 0m, null);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_decided_order_cannot_be_cancelled()
    {
        var so = Draft();
        so.MarkAutoApproved(Guid.NewGuid());

        so.CanCancel.Should().BeFalse();
        var act = () => so.Cancel();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_draft_can_be_cancelled()
    {
        var so = Draft();
        so.AddLine(Guid.NewGuid(), 1m, 10m, 0m, null);

        so.CanCancel.Should().BeTrue();
        so.Cancel();
        so.Status.Should().Be(SalesOrderStatus.Cancelled);
    }

    [Fact]
    public void A_pending_approval_order_can_be_cancelled()
    {
        var so = Draft();
        so.AddLine(Guid.NewGuid(), 1m, 10m, 0m, null);
        so.MarkPendingApproval(Guid.NewGuid());

        so.CanCancel.Should().BeTrue();
        so.Cancel();
        so.Status.Should().Be(SalesOrderStatus.Cancelled);
    }

    [Fact]
    public void EditDraft_replaces_header_and_lines_and_recalculates()
    {
        var so = Draft();
        so.AddLine(Guid.NewGuid(), 1m, 10m, 0m, null);

        var newCustomer = Guid.NewGuid();
        var newWarehouse = Guid.NewGuid();
        so.EditDraft(newCustomer, newWarehouse, new DateOnly(2026, 7, 1), "edited", new[]
        {
            (ProductId: Guid.NewGuid(), Quantity: 2m, UnitPrice: 100m, TaxRate: 0.11m, Description: (string?)"A"),
        });

        so.CustomerId.Should().Be(newCustomer);
        so.WarehouseId.Should().Be(newWarehouse);
        so.Notes.Should().Be("edited");
        so.Lines.Should().ContainSingle();
        so.SubTotal.Should().Be(200m);
        so.TaxTotal.Should().Be(22m);
        so.GrandTotal.Should().Be(222m);
    }

    [Fact]
    public void EditDraft_is_rejected_once_submitted()
    {
        var so = Draft();
        so.AddLine(Guid.NewGuid(), 1m, 10m, 0m, null);
        so.MarkAutoApproved(Guid.NewGuid());

        var act = () => so.EditDraft(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 7, 1), null,
            new[] { (Guid.NewGuid(), 1m, 1m, 0m, (string?)null) });
        act.Should().Throw<InvalidOperationException>();
    }
}
