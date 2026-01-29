using System.Net;
using System.Net.Http.Json;
using DarkVelocity.PaymentGateway.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.PaymentGateway.Tests;

public class CheckoutControllerTests : IClassFixture<PaymentGatewayApiFixture>
{
    private readonly PaymentGatewayApiFixture _fixture;
    private readonly HttpClient _client;

    public CheckoutControllerTests(PaymentGatewayApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task CreateCheckoutSession_CreatesSessionWithPaymentIntent()
    {
        var request = new CreateCheckoutSessionRequest(
            Amount: 9999,
            Currency: "usd",
            SuccessUrl: "https://example.com/success",
            CancelUrl: "https://example.com/cancel",
            CustomerEmail: "customer@example.com");

        var response = await _client.PostAsJsonAsync("/api/v1/checkout/sessions", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var session = await response.Content.ReadFromJsonAsync<CheckoutSessionDto>();
        session.Should().NotBeNull();
        session!.AmountTotal.Should().Be(9999);
        session.Currency.Should().Be("usd");
        session.Status.Should().Be("open");
        session.Url.Should().Contain("checkout.paymentgateway.local");
        session.SuccessUrl.Should().Be("https://example.com/success");
        session.CancelUrl.Should().Be("https://example.com/cancel");
        session.CustomerEmail.Should().Be("customer@example.com");
        session.PaymentIntentId.Should().NotBe(Guid.Empty);
        session.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task CreateCheckoutSession_CreatesUnderlyingPaymentIntent()
    {
        var request = new CreateCheckoutSessionRequest(
            Amount: 5000,
            Currency: "usd",
            ExternalOrderId: "order_12345");

        var response = await _client.PostAsJsonAsync("/api/v1/checkout/sessions", request);
        var session = await response.Content.ReadFromJsonAsync<CheckoutSessionDto>();

        // Verify the payment intent exists
        var piResponse = await _client.GetAsync($"/api/v1/payment_intents/{session!.PaymentIntentId}");
        piResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var pi = await piResponse.Content.ReadFromJsonAsync<PaymentIntentDto>();
        pi!.Amount.Should().Be(5000);
        pi.Currency.Should().Be("usd");
        pi.Channel.Should().Be("ecommerce");
        pi.ExternalOrderId.Should().Be("order_12345");
    }

    [Fact]
    public async Task GetCheckoutSession_ReturnsSession()
    {
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/checkout/sessions",
            new CreateCheckoutSessionRequest(Amount: 2500, Currency: "usd"));
        var created = await createResponse.Content.ReadFromJsonAsync<CheckoutSessionDto>();

        var response = await _client.GetAsync($"/api/v1/checkout/sessions/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var session = await response.Content.ReadFromJsonAsync<CheckoutSessionDto>();
        session!.Id.Should().Be(created.Id);
        session.Status.Should().Be("open");
        session.Links.Should().ContainKey("self");
        session.Links.Should().ContainKey("payment_intent");
    }

    [Fact]
    public async Task ExpireCheckoutSession_ExpiresSession()
    {
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/checkout/sessions",
            new CreateCheckoutSessionRequest(Amount: 3000, Currency: "usd"));
        var created = await createResponse.Content.ReadFromJsonAsync<CheckoutSessionDto>();

        var response = await _client.PostAsync(
            $"/api/v1/checkout/sessions/{created!.Id}/expire",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var expired = await response.Content.ReadFromJsonAsync<CheckoutSessionDto>();
        expired!.Status.Should().Be("expired");

        // Verify payment intent is canceled
        var piResponse = await _client.GetAsync($"/api/v1/payment_intents/{created.PaymentIntentId}");
        var pi = await piResponse.Content.ReadFromJsonAsync<PaymentIntentDto>();
        pi!.Status.Should().Be("canceled");
    }

    [Fact]
    public async Task GetCheckoutSession_AfterPaymentSucceeds_ShowsComplete()
    {
        // Create session
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/checkout/sessions",
            new CreateCheckoutSessionRequest(Amount: 4000, Currency: "usd"));
        var session = await createResponse.Content.ReadFromJsonAsync<CheckoutSessionDto>();

        // Confirm the underlying payment intent
        var confirmRequest = new ConfirmPaymentIntentRequest(
            Card: new CardPaymentMethodRequest(
                Number: "4242424242424242",
                ExpMonth: "12",
                ExpYear: "2025",
                Cvc: "123"));

        await _client.PostAsJsonAsync(
            $"/api/v1/payment_intents/{session!.PaymentIntentId}/confirm",
            confirmRequest);

        // Get the session - should be complete
        var response = await _client.GetAsync($"/api/v1/checkout/sessions/{session.Id}");
        var updated = await response.Content.ReadFromJsonAsync<CheckoutSessionDto>();

        updated!.Status.Should().Be("complete");
    }

    [Fact]
    public async Task ExpireCheckoutSession_AlreadyComplete_ReturnsBadRequest()
    {
        // Create and complete session
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/checkout/sessions",
            new CreateCheckoutSessionRequest(Amount: 5000, Currency: "usd"));
        var session = await createResponse.Content.ReadFromJsonAsync<CheckoutSessionDto>();

        var confirmRequest = new ConfirmPaymentIntentRequest(
            Card: new CardPaymentMethodRequest(
                Number: "4242424242424242",
                ExpMonth: "12",
                ExpYear: "2025",
                Cvc: "123"));

        await _client.PostAsJsonAsync(
            $"/api/v1/payment_intents/{session!.PaymentIntentId}/confirm",
            confirmRequest);

        // Try to expire
        var response = await _client.PostAsync(
            $"/api/v1/checkout/sessions/{session.Id}/expire",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
