using System.Net;
using System.Text;
using Accountrack.Billing.Application.Abstractions;
using Accountrack.Billing.Infrastructure.Payments;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Accountrack.Billing.UnitTests;

/// <summary>
/// The Xendit adapter's request shaping. Regression cover for the live 400: Xendit rejects optional
/// fields sent as empty/null (`success_redirect_url` "not allowed to be empty", `payer_email` "must be a
/// string"), so blank optional values must be <b>omitted</b> from the JSON body, not sent as "" or null.
/// </summary>
public class XenditGatewayTests
{
    /// <summary>Captures the outgoing request body and returns a canned Xendit response.</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public string? AuthHeader { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            AuthHeader = request.Headers.Authorization?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"id":"xnd-123","invoice_url":"https://checkout.xendit.co/xnd-123","status":"PENDING"}""",
                    Encoding.UTF8, "application/json"),
            };
        }
    }

    private static (XenditGateway gateway, CapturingHandler handler) Build(XenditOptions opts)
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri(opts.BaseUrl) };
        var gateway = new XenditGateway(http, Options.Create(opts), NullLogger<XenditGateway>.Instance);
        return (gateway, handler);
    }

    private static CreateGatewayInvoiceRequest Req(string? success = null, string? failure = null, string? email = null) =>
        new("ext-1", 149_000, "IDR", "Accountrack — Starter", email, success, failure);

    [Fact]
    public async Task Blank_optional_fields_are_omitted_from_the_body()
    {
        var (gateway, handler) = Build(new XenditOptions { SecretKey = "xnd_development_x" }); // no redirect config

        var result = await gateway.CreateInvoiceAsync(Req(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be("xnd-123");
        result.Value.InvoiceUrl.Should().Be("https://checkout.xendit.co/xnd-123");

        // The three optional fields must be absent entirely (this is what the 400 was about).
        handler.Body.Should().NotContain("success_redirect_url");
        handler.Body.Should().NotContain("failure_redirect_url");
        handler.Body.Should().NotContain("payer_email");
        // Required fields are present.
        handler.Body.Should().Contain("\"external_id\":\"ext-1\"");
        handler.Body.Should().Contain("\"amount\":149000");
        handler.Body.Should().Contain("\"currency\":\"IDR\"");
    }

    [Fact]
    public async Task Empty_string_config_redirects_are_treated_as_blank_and_omitted()
    {
        // Reproduces the deployment default: env vars supply "" when unset.
        var (gateway, handler) = Build(new XenditOptions
        {
            SecretKey = "xnd_development_x",
            SuccessRedirectUrl = "",
            FailureRedirectUrl = "   ",
        });

        await gateway.CreateInvoiceAsync(Req(), CancellationToken.None);

        handler.Body.Should().NotContain("success_redirect_url");
        handler.Body.Should().NotContain("failure_redirect_url");
    }

    [Fact]
    public async Task Provided_optional_fields_are_included()
    {
        var (gateway, handler) = Build(new XenditOptions { SecretKey = "xnd_development_x" });

        await gateway.CreateInvoiceAsync(
            Req(success: "https://app/ok", failure: "https://app/no", email: "a@b.com"), CancellationToken.None);

        handler.Body.Should().Contain("\"success_redirect_url\":\"https://app/ok\"");
        handler.Body.Should().Contain("\"failure_redirect_url\":\"https://app/no\"");
        handler.Body.Should().Contain("\"payer_email\":\"a@b.com\"");
    }

    [Fact]
    public async Task Authenticates_with_basic_secret_key_and_blank_password()
    {
        var (gateway, handler) = Build(new XenditOptions { SecretKey = "xnd_development_secret" });

        await gateway.CreateInvoiceAsync(Req(), CancellationToken.None);

        // Basic base64("xnd_development_secret:")
        var expected = Convert.ToBase64String(Encoding.ASCII.GetBytes("xnd_development_secret:"));
        handler.AuthHeader.Should().Be($"Basic {expected}");
    }

    [Fact]
    public async Task Unconfigured_gateway_fails_fast_without_calling_out()
    {
        var (gateway, handler) = Build(new XenditOptions { SecretKey = "" });

        var result = await gateway.CreateInvoiceAsync(Req(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BILLING.GATEWAY_ERROR");
        handler.Body.Should().BeNull("no HTTP call should be made when unconfigured");
    }
}
