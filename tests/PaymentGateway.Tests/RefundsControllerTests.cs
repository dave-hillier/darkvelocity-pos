using System.Net;
using System.Net.Http.Json;
using DarkVelocity.PaymentGateway.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.PaymentGateway.Tests;

public class RefundsControllerTests : IClassFixture<PaymentGatewayApiFixture>
{
    private readonly PaymentGatewayApiFixture _fixture;
    private readonly HttpClient _client;

    public RefundsControllerTests(PaymentGatewayApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    private async Task<PaymentIntentDto> CreateAndConfirmPaymentIntent(long amount = 5000)
    {
        var createRequest = new CreatePaymentIntentRequest(Amount: amount, Currency: "usd");
        var createResponse = await _client.PostAsJsonAsync("/api/v1/payment_intents", createRequest);
        var pi = await createResponse.Content.ReadFromJsonAsync<PaymentIntentDto>();

        var confirmRequest = new ConfirmPaymentIntentRequest(
            Card: new CardPaymentMethodRequest(
                Number: "4242424242424242",
                ExpMonth: "12",
                ExpYear: "2025",
                Cvc: "123"));

        await _client.PostAsJsonAsync($"/api/v1/payment_intents/{pi!.Id}/confirm", confirmRequest);

        var getResponse = await _client.GetAsync($"/api/v1/payment_intents/{pi.Id}");
        return (await getResponse.Content.ReadFromJsonAsync<PaymentIntentDto>())!;
    }

    [Fact]
    public async Task CreateRefund_FullRefund_RefundsEntireAmount()
    {
        var pi = await CreateAndConfirmPaymentIntent(3000);

        var request = new CreateRefundRequest(
            PaymentIntentId: pi.Id,
            Reason: "requested_by_customer");

        var response = await _client.PostAsJsonAsync("/api/v1/refunds", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var refund = await response.Content.ReadFromJsonAsync<RefundDto>();
        refund.Should().NotBeNull();
        refund!.Amount.Should().Be(3000);
        refund.Currency.Should().Be("usd");
        refund.Status.Should().Be("succeeded");
        refund.Reason.Should().Be("requested_by_customer");
        refund.ReceiptNumber.Should().StartWith("RF-");
    }

    [Fact]
    public async Task CreateRefund_PartialRefund_RefundsSpecifiedAmount()
    {
        var pi = await CreateAndConfirmPaymentIntent(10000);

        var request = new CreateRefundRequest(
            PaymentIntentId: pi.Id,
            Amount: 4000,
            Reason: "requested_by_customer");

        var response = await _client.PostAsJsonAsync("/api/v1/refunds", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var refund = await response.Content.ReadFromJsonAsync<RefundDto>();
        refund!.Amount.Should().Be(4000);
    }

    [Fact]
    public async Task CreateRefund_MultiplePartialRefunds_AllowedUpToOriginalAmount()
    {
        var pi = await CreateAndConfirmPaymentIntent(10000);

        // First refund: 3000
        var request1 = new CreateRefundRequest(PaymentIntentId: pi.Id, Amount: 3000);
        var response1 = await _client.PostAsJsonAsync("/api/v1/refunds", request1);
        response1.StatusCode.Should().Be(HttpStatusCode.Created);

        // Second refund: 5000
        var request2 = new CreateRefundRequest(PaymentIntentId: pi.Id, Amount: 5000);
        var response2 = await _client.PostAsJsonAsync("/api/v1/refunds", request2);
        response2.StatusCode.Should().Be(HttpStatusCode.Created);

        // Third refund: remaining 2000
        var request3 = new CreateRefundRequest(PaymentIntentId: pi.Id, Amount: 2000);
        var response3 = await _client.PostAsJsonAsync("/api/v1/refunds", request3);
        response3.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateRefund_ExceedsAvailableAmount_ReturnsBadRequest()
    {
        var pi = await CreateAndConfirmPaymentIntent(5000);

        var request = new CreateRefundRequest(
            PaymentIntentId: pi.Id,
            Amount: 6000); // More than original amount

        var response = await _client.PostAsJsonAsync("/api/v1/refunds", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateRefund_AlreadyFullyRefunded_ReturnsBadRequest()
    {
        var pi = await CreateAndConfirmPaymentIntent(2000);

        // Full refund
        var request1 = new CreateRefundRequest(PaymentIntentId: pi.Id);
        await _client.PostAsJsonAsync("/api/v1/refunds", request1);

        // Try to refund again
        var request2 = new CreateRefundRequest(PaymentIntentId: pi.Id, Amount: 1000);
        var response = await _client.PostAsJsonAsync("/api/v1/refunds", request2);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateRefund_PaymentNotSucceeded_ReturnsBadRequest()
    {
        // Create but don't confirm
        var createRequest = new CreatePaymentIntentRequest(Amount: 5000, Currency: "usd");
        var createResponse = await _client.PostAsJsonAsync("/api/v1/payment_intents", createRequest);
        var pi = await createResponse.Content.ReadFromJsonAsync<PaymentIntentDto>();

        var request = new CreateRefundRequest(PaymentIntentId: pi!.Id);
        var response = await _client.PostAsJsonAsync("/api/v1/refunds", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAll_ReturnsRefunds()
    {
        var pi = await CreateAndConfirmPaymentIntent(4000);
        await _client.PostAsJsonAsync("/api/v1/refunds", new CreateRefundRequest(PaymentIntentId: pi.Id));

        var response = await _client.GetAsync("/api/v1/refunds");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<RefundDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_FiltersByPaymentIntent()
    {
        var pi = await CreateAndConfirmPaymentIntent(5000);
        await _client.PostAsJsonAsync("/api/v1/refunds", new CreateRefundRequest(PaymentIntentId: pi.Id));

        var response = await _client.GetAsync($"/api/v1/refunds?payment_intent={pi.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<RefundDto>>();
        collection!.Embedded.Items.Should().OnlyContain(r => r.PaymentIntentId == pi.Id);
    }

    [Fact]
    public async Task GetById_ReturnsRefund()
    {
        var pi = await CreateAndConfirmPaymentIntent(6000);
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/refunds",
            new CreateRefundRequest(PaymentIntentId: pi.Id));
        var created = await createResponse.Content.ReadFromJsonAsync<RefundDto>();

        var response = await _client.GetAsync($"/api/v1/refunds/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var refund = await response.Content.ReadFromJsonAsync<RefundDto>();
        refund!.Id.Should().Be(created.Id);
        refund.Links.Should().ContainKey("self");
        refund.Links.Should().ContainKey("payment_intent");
    }
}
