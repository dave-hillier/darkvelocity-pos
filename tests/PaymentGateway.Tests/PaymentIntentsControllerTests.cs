using System.Net;
using System.Net.Http.Json;
using DarkVelocity.PaymentGateway.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.PaymentGateway.Tests;

public class PaymentIntentsControllerTests : IClassFixture<PaymentGatewayApiFixture>
{
    private readonly PaymentGatewayApiFixture _fixture;
    private readonly HttpClient _client;

    public PaymentIntentsControllerTests(PaymentGatewayApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task CreatePaymentIntent_CreatesPaymentIntent()
    {
        var request = new CreatePaymentIntentRequest(
            Amount: 2500, // $25.00
            Currency: "usd",
            Description: "Test payment");

        var response = await _client.PostAsJsonAsync("/api/v1/payment_intents", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var pi = await response.Content.ReadFromJsonAsync<PaymentIntentDto>();
        pi.Should().NotBeNull();
        pi!.Amount.Should().Be(2500);
        pi.Currency.Should().Be("usd");
        pi.Status.Should().Be("requires_payment_method");
        pi.ClientSecret.Should().NotBeNullOrEmpty();
        pi.Links.Should().ContainKey("self");
        pi.Links.Should().ContainKey("confirm");
        pi.Links.Should().ContainKey("cancel");
    }

    [Fact]
    public async Task CreatePaymentIntent_WithManualCapture_SetsCorrectCaptureMethod()
    {
        var request = new CreatePaymentIntentRequest(
            Amount: 3000,
            Currency: "usd",
            CaptureMethod: "manual");

        var response = await _client.PostAsJsonAsync("/api/v1/payment_intents", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var pi = await response.Content.ReadFromJsonAsync<PaymentIntentDto>();
        pi!.CaptureMethod.Should().Be("manual");
    }

    [Fact]
    public async Task CreatePaymentIntent_ForPOS_RequiresTerminal()
    {
        var request = new CreatePaymentIntentRequest(
            Amount: 1500,
            Currency: "usd",
            Channel: "pos",
            TerminalId: _fixture.TestTerminalId);

        var response = await _client.PostAsJsonAsync("/api/v1/payment_intents", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var pi = await response.Content.ReadFromJsonAsync<PaymentIntentDto>();
        pi!.Channel.Should().Be("pos");
        pi.TerminalId.Should().Be(_fixture.TestTerminalId);
    }

    [Fact]
    public async Task GetAll_ReturnsPaymentIntents()
    {
        var response = await _client.GetAsync("/api/v1/payment_intents");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<PaymentIntentDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_FiltersByStatus()
    {
        var response = await _client.GetAsync("/api/v1/payment_intents?status=requires_payment_method");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<PaymentIntentDto>>();
        collection!.Embedded.Items.Should().OnlyContain(p => p.Status == "requires_payment_method");
    }

    [Fact]
    public async Task GetById_ReturnsPaymentIntent()
    {
        var response = await _client.GetAsync($"/api/v1/payment_intents/{_fixture.TestPaymentIntentId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pi = await response.Content.ReadFromJsonAsync<PaymentIntentDto>();
        pi.Should().NotBeNull();
        pi!.Id.Should().Be(_fixture.TestPaymentIntentId);
    }

    [Fact]
    public async Task ConfirmPaymentIntent_WithCard_ProcessesPayment()
    {
        // Create a new payment intent
        var createRequest = new CreatePaymentIntentRequest(
            Amount: 4242,
            Currency: "usd");

        var createResponse = await _client.PostAsJsonAsync("/api/v1/payment_intents", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PaymentIntentDto>();

        // Confirm with test card
        var confirmRequest = new ConfirmPaymentIntentRequest(
            PaymentMethodType: "card",
            Card: new CardPaymentMethodRequest(
                Number: "4242424242424242",
                ExpMonth: "12",
                ExpYear: "2025",
                Cvc: "123"));

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/payment_intents/{created!.Id}/confirm",
            confirmRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var confirmed = await response.Content.ReadFromJsonAsync<PaymentIntentDto>();
        confirmed!.Status.Should().Be("succeeded");
        confirmed.Card.Should().NotBeNull();
        confirmed.Card!.Brand.Should().Be("visa");
        confirmed.Card.Last4.Should().Be("4242");
    }

    [Fact]
    public async Task ConfirmPaymentIntent_WithDeclinedCard_ReturnsCardError()
    {
        var createRequest = new CreatePaymentIntentRequest(Amount: 1000, Currency: "usd");
        var createResponse = await _client.PostAsJsonAsync("/api/v1/payment_intents", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PaymentIntentDto>();

        var confirmRequest = new ConfirmPaymentIntentRequest(
            PaymentMethodType: "card",
            Card: new CardPaymentMethodRequest(
                Number: "4000000000000002", // Decline card
                ExpMonth: "12",
                ExpYear: "2025",
                Cvc: "123"));

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/payment_intents/{created!.Id}/confirm",
            confirmRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("card_declined");
    }

    [Fact]
    public async Task ConfirmPaymentIntent_WithManualCapture_RequiresCapture()
    {
        var createRequest = new CreatePaymentIntentRequest(
            Amount: 5000,
            Currency: "usd",
            CaptureMethod: "manual");

        var createResponse = await _client.PostAsJsonAsync("/api/v1/payment_intents", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PaymentIntentDto>();

        var confirmRequest = new ConfirmPaymentIntentRequest(
            Card: new CardPaymentMethodRequest(
                Number: "4242424242424242",
                ExpMonth: "12",
                ExpYear: "2025",
                Cvc: "123"));

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/payment_intents/{created!.Id}/confirm",
            confirmRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var confirmed = await response.Content.ReadFromJsonAsync<PaymentIntentDto>();
        confirmed!.Status.Should().Be("requires_capture");
        confirmed.AmountCapturable.Should().Be(5000);
        confirmed.Links.Should().ContainKey("capture");
    }

    [Fact]
    public async Task CapturePaymentIntent_CapturesAuthorization()
    {
        // Create and confirm with manual capture
        var createRequest = new CreatePaymentIntentRequest(
            Amount: 7500,
            Currency: "usd",
            CaptureMethod: "manual");

        var createResponse = await _client.PostAsJsonAsync("/api/v1/payment_intents", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PaymentIntentDto>();

        var confirmRequest = new ConfirmPaymentIntentRequest(
            Card: new CardPaymentMethodRequest(
                Number: "4242424242424242",
                ExpMonth: "12",
                ExpYear: "2025",
                Cvc: "123"));

        await _client.PostAsJsonAsync(
            $"/api/v1/payment_intents/{created!.Id}/confirm",
            confirmRequest);

        // Capture
        var captureRequest = new CapturePaymentIntentRequest();
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/payment_intents/{created.Id}/capture",
            captureRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var captured = await response.Content.ReadFromJsonAsync<PaymentIntentDto>();
        captured!.Status.Should().Be("succeeded");
        captured.AmountReceived.Should().Be(7500);
    }

    [Fact]
    public async Task CapturePaymentIntent_PartialCapture_CapturesSpecifiedAmount()
    {
        var createRequest = new CreatePaymentIntentRequest(
            Amount: 10000,
            Currency: "usd",
            CaptureMethod: "manual");

        var createResponse = await _client.PostAsJsonAsync("/api/v1/payment_intents", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PaymentIntentDto>();

        var confirmRequest = new ConfirmPaymentIntentRequest(
            Card: new CardPaymentMethodRequest(
                Number: "4242424242424242",
                ExpMonth: "12",
                ExpYear: "2025",
                Cvc: "123"));

        await _client.PostAsJsonAsync(
            $"/api/v1/payment_intents/{created!.Id}/confirm",
            confirmRequest);

        // Partial capture
        var captureRequest = new CapturePaymentIntentRequest(AmountToCapture: 5000);
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/payment_intents/{created.Id}/capture",
            captureRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var captured = await response.Content.ReadFromJsonAsync<PaymentIntentDto>();
        captured!.AmountReceived.Should().Be(5000);
        captured.AmountCapturable.Should().Be(5000); // Remaining
    }

    [Fact]
    public async Task CancelPaymentIntent_CancelsPaymentIntent()
    {
        var createRequest = new CreatePaymentIntentRequest(Amount: 1500, Currency: "usd");
        var createResponse = await _client.PostAsJsonAsync("/api/v1/payment_intents", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PaymentIntentDto>();

        var cancelRequest = new CancelPaymentIntentRequest(CancellationReason: "requested_by_customer");
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/payment_intents/{created!.Id}/cancel",
            cancelRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var canceled = await response.Content.ReadFromJsonAsync<PaymentIntentDto>();
        canceled!.Status.Should().Be("canceled");
        canceled.CancellationReason.Should().Be("requested_by_customer");
    }

    [Fact]
    public async Task CancelPaymentIntent_SucceededPayment_ReturnsBadRequest()
    {
        // Create and confirm
        var createRequest = new CreatePaymentIntentRequest(Amount: 2000, Currency: "usd");
        var createResponse = await _client.PostAsJsonAsync("/api/v1/payment_intents", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PaymentIntentDto>();

        var confirmRequest = new ConfirmPaymentIntentRequest(
            Card: new CardPaymentMethodRequest(
                Number: "4242424242424242",
                ExpMonth: "12",
                ExpYear: "2025",
                Cvc: "123"));

        await _client.PostAsJsonAsync(
            $"/api/v1/payment_intents/{created!.Id}/confirm",
            confirmRequest);

        // Try to cancel
        var cancelRequest = new CancelPaymentIntentRequest();
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/payment_intents/{created.Id}/cancel",
            cancelRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdatePaymentIntent_UpdatesDescription()
    {
        var createRequest = new CreatePaymentIntentRequest(Amount: 3000, Currency: "usd");
        var createResponse = await _client.PostAsJsonAsync("/api/v1/payment_intents", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PaymentIntentDto>();

        var updateRequest = new UpdatePaymentIntentRequest(
            Description: "Updated description",
            ReceiptEmail: "customer@example.com");

        var response = await _client.PatchAsJsonAsync(
            $"/api/v1/payment_intents/{created!.Id}",
            updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<PaymentIntentDto>();
        updated!.Description.Should().Be("Updated description");
        updated.ReceiptEmail.Should().Be("customer@example.com");
    }
}
