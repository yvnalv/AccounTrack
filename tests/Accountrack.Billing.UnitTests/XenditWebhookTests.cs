using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Integration;
using Accountrack.Billing.Application.Abstractions;
using Accountrack.Billing.Application.Features;
using Accountrack.Billing.Domain;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Billing.UnitTests;

/// <summary>
/// The payment webhook (SUBSCRIPTION_BILLING.md §5). These decide whether a real payment activates a
/// subscription and whether a forged or replayed call can, so they are deliberately explicit.
/// </summary>
public class XenditWebhookTests
{
    private static readonly DateTime Now = new(2026, 7, 23, 10, 0, 0, DateTimeKind.Utc);

    private readonly IBillingInvoiceRepository _invoices = Substitute.For<IBillingInvoiceRepository>();
    private readonly ISubscriptionRepository _subscriptions = Substitute.For<ISubscriptionRepository>();
    private readonly IPlanRepository _plans = Substitute.For<IPlanRepository>();
    private readonly IBillingUnitOfWork _uow = Substitute.For<IBillingUnitOfWork>();
    private readonly IInboxStore _inbox = Substitute.For<IInboxStore>();
    private readonly IXenditWebhookVerifier _verifier = Substitute.For<IXenditWebhookVerifier>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public XenditWebhookTests()
    {
        _clock.UtcNow.Returns(Now);
        _verifier.IsValid("good-token").Returns(true);
        _verifier.IsValid(Arg.Is<string?>(t => t != "good-token")).Returns(false);
    }

    private ProcessXenditWebhookHandler Handler() =>
        new(_invoices, _subscriptions, _plans, _uow, _inbox, _verifier, _clock);

    private static (BillingInvoice invoice, Subscription sub) OpenInvoice()
    {
        var plan = Plan.Create("STARTER-MONTHLY", "Starter", BillingInterval.Monthly, 149_000, 2, 59_000, 1, "IDR", "{}");
        var sub = Subscription.StartTrial(plan.Id, plan.Interval, PaymentMode.Invoice,
            new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 24));
        var invoice = BillingInvoice.CreateDraft(sub.Id, "SUB-202607-0001",
            new DateOnly(2026, 7, 23), new DateOnly(2026, 8, 23), 149_000, 0, "IDR", new DateOnly(2026, 7, 30));
        invoice.Issue("xnd-invoice-1");
        return (invoice, sub);
    }

    [Fact]
    public async Task A_paid_webhook_activates_the_subscription_and_marks_the_invoice_paid()
    {
        var (invoice, sub) = OpenInvoice();
        _inbox.HasProcessedAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        _invoices.GetByGatewayInvoiceIdIgnoringFiltersAsync("xnd-invoice-1", Arg.Any<CancellationToken>()).Returns(invoice);
        _subscriptions.GetByIdIgnoringFiltersAsync(sub.Id, Arg.Any<CancellationToken>()).Returns(sub);

        var result = await Handler().Handle(new ProcessXenditWebhookCommand("good-token", "xnd-invoice-1", "PAID"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        invoice.Status.Should().Be(BillingInvoiceStatus.Paid);
        invoice.PaidAt.Should().Be(Now);
        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.CurrentPeriodEnd.Should().Be(invoice.PeriodEnd);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _inbox.Received(1).MarkProcessedAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_forged_token_is_rejected_and_changes_nothing()
    {
        var result = await Handler().Handle(new ProcessXenditWebhookCommand("WRONG", "xnd-invoice-1", "PAID"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BILLING.WEBHOOK_UNAUTHORIZED");
        await _invoices.DidNotReceive().GetByGatewayInvoiceIdIgnoringFiltersAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_missing_token_is_rejected()
    {
        var result = await Handler().Handle(new ProcessXenditWebhookCommand(null, "xnd-invoice-1", "PAID"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BILLING.WEBHOOK_UNAUTHORIZED");
    }

    [Fact]
    public async Task A_replayed_webhook_does_not_activate_twice()
    {
        // Idempotency: the inbox already recorded this gateway invoice, so it must be a no-op.
        _inbox.HasProcessedAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await Handler().Handle(new ProcessXenditWebhookCommand("good-token", "xnd-invoice-1", "PAID"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _invoices.DidNotReceive().GetByGatewayInvoiceIdIgnoringFiltersAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("EXPIRED")]
    [InlineData("PENDING")]
    public async Task A_non_paid_status_is_acknowledged_but_activates_nothing(string status)
    {
        var result = await Handler().Handle(new ProcessXenditWebhookCommand("good-token", "xnd-invoice-1", status), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _invoices.DidNotReceive().GetByGatewayInvoiceIdIgnoringFiltersAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_unknown_invoice_is_acknowledged_without_error()
    {
        _inbox.HasProcessedAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        _invoices.GetByGatewayInvoiceIdIgnoringFiltersAsync("xnd-unknown", Arg.Any<CancellationToken>()).Returns((BillingInvoice?)null);

        var result = await Handler().Handle(new ProcessXenditWebhookCommand("good-token", "xnd-unknown", "PAID"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("acknowledge so the gateway stops retrying");
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _inbox.Received(1).MarkProcessedAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
