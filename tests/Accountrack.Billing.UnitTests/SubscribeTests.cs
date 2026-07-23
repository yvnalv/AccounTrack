using Accountrack.Application.Abstractions.Context;
using Accountrack.Billing.Application.Abstractions;
using Accountrack.Billing.Application.Features;
using Accountrack.Billing.Domain;
using Accountrack.SharedKernel.Results;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Billing.UnitTests;

/// <summary>Checkout — creating a hosted invoice for the tenant's plan (SUBSCRIPTION_BILLING.md §4).</summary>
public class SubscribeTests
{
    private static readonly DateTime Now = new(2026, 7, 23, 10, 0, 0, DateTimeKind.Utc);

    private readonly ISubscriptionRepository _subscriptions = Substitute.For<ISubscriptionRepository>();
    private readonly IPlanRepository _plans = Substitute.For<IPlanRepository>();
    private readonly IBillingInvoiceRepository _invoices = Substitute.For<IBillingInvoiceRepository>();
    private readonly IPaymentGateway _gateway = Substitute.For<IPaymentGateway>();
    private readonly IBillingUnitOfWork _uow = Substitute.For<IBillingUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public SubscribeTests() => _clock.UtcNow.Returns(Now);

    private SubscribeHandler Handler() => new(_subscriptions, _plans, _invoices, _gateway, _uow, _clock);

    private static Plan BusinessPlan() =>
        Plan.Create("BUSINESS-MONTHLY", "Business", BillingInterval.Monthly, 499_000, 8, 49_000, 3, "IDR", "{}");

    [Fact]
    public async Task Checkout_creates_an_open_invoice_and_returns_the_pay_url()
    {
        var plan = BusinessPlan();
        var sub = Subscription.StartTrial(plan.Id, plan.Interval, PaymentMode.Invoice,
            new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 24));
        _subscriptions.GetForCurrentTenantAsync(Arg.Any<CancellationToken>()).Returns(sub);
        _plans.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        _invoices.CountForCurrentTenantAsync(Arg.Any<CancellationToken>()).Returns(0);

        BillingInvoice? added = null;
        _invoices.When(r => r.Add(Arg.Any<BillingInvoice>())).Do(ci => added = ci.Arg<BillingInvoice>());

        CreateGatewayInvoiceRequest? sent = null;
        _gateway.CreateInvoiceAsync(Arg.Any<CreateGatewayInvoiceRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                sent = ci.Arg<CreateGatewayInvoiceRequest>();
                return Result.Success(new GatewayInvoice("xnd-99", "https://checkout.xendit.co/xnd-99", "PENDING"));
            });

        var result = await Handler().Handle(new SubscribeCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PayUrl.Should().Be("https://checkout.xendit.co/xnd-99");
        result.Value.AmountMinor.Should().Be(499_000); // base + 0 extra seats
        added.Should().NotBeNull();
        added!.Status.Should().Be(BillingInvoiceStatus.Open);
        added.GatewayInvoiceId.Should().Be("xnd-99");
        added.PeriodEnd.Should().Be(new DateOnly(2026, 8, 23)); // monthly
        // The gateway is billed the plan amount in whole rupiah, referencing the invoice id.
        sent!.AmountMinor.Should().Be(499_000);
        sent.ExternalId.Should().Be(added.Id.ToString());
        // Persisted twice: once to fix the invoice id before calling the gateway, once after issuing.
        await _uow.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Checkout_without_a_subscription_is_rejected()
    {
        _subscriptions.GetForCurrentTenantAsync(Arg.Any<CancellationToken>()).Returns((Subscription?)null);

        var result = await Handler().Handle(new SubscribeCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BILLING.NO_SUBSCRIPTION");
        await _gateway.DidNotReceive().CreateInvoiceAsync(Arg.Any<CreateGatewayInvoiceRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_gateway_failure_leaves_the_invoice_unissued_and_surfaces_the_error()
    {
        var plan = BusinessPlan();
        var sub = Subscription.StartTrial(plan.Id, plan.Interval, PaymentMode.Invoice,
            new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 24));
        _subscriptions.GetForCurrentTenantAsync(Arg.Any<CancellationToken>()).Returns(sub);
        _plans.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        _invoices.CountForCurrentTenantAsync(Arg.Any<CancellationToken>()).Returns(0);
        _gateway.CreateInvoiceAsync(Arg.Any<CreateGatewayInvoiceRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<GatewayInvoice>(BillingErrors.GatewayError("HTTP 500")));

        var result = await Handler().Handle(new SubscribeCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BILLING.GATEWAY_ERROR");
    }
}
