using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Payments.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Payments.Tests;

public class PaymentsControllerTests : IClassFixture<PaymentsApiFixture>
{
    private readonly PaymentsApiFixture _fixture;
    private readonly HttpClient _client;

    public PaymentsControllerTests(PaymentsApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsPayments()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/payments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<PaymentDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_FiltersByOrder()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/payments?orderId={_fixture.TestOrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<PaymentDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().OnlyContain(p => p.OrderId == _fixture.TestOrderId);
    }

    [Fact]
    public async Task GetById_ReturnsPayment()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/payments/{_fixture.TestPaymentId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payment = await response.Content.ReadFromJsonAsync<PaymentDto>();
        payment.Should().NotBeNull();
        payment!.Id.Should().Be(_fixture.TestPaymentId);
        payment.Status.Should().Be("completed");
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/payments/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByOrder_ReturnsPaymentsForOrder()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/payments/by-order/{_fixture.TestOrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<PaymentDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().OnlyContain(p => p.OrderId == _fixture.TestOrderId);
    }

    [Fact]
    public async Task CreateCashPayment_CreatesPayment()
    {
        var orderId = Guid.NewGuid();
        var request = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.TestCashPaymentMethodId,
            Amount: 25.00m,
            TipAmount: 3.00m,
            ReceivedAmount: 30.00m);

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payment = await response.Content.ReadFromJsonAsync<PaymentDto>();
        payment.Should().NotBeNull();
        payment!.Amount.Should().Be(25.00m);
        payment.TipAmount.Should().Be(3.00m);
        payment.TotalAmount.Should().Be(28.00m);
        payment.ReceivedAmount.Should().Be(30.00m);
        payment.ChangeAmount.Should().Be(2.00m);
        payment.Status.Should().Be("completed");
        payment.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateCashPayment_ExactAmount_NoChange()
    {
        var orderId = Guid.NewGuid();
        var request = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.TestCashPaymentMethodId,
            Amount: 20.00m,
            ReceivedAmount: 20.00m);

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payment = await response.Content.ReadFromJsonAsync<PaymentDto>();
        payment!.ChangeAmount.Should().Be(0m);
    }

    [Fact]
    public async Task CreateCashPayment_InsufficientAmount_ReturnsBadRequest()
    {
        var orderId = Guid.NewGuid();
        var request = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.TestCashPaymentMethodId,
            Amount: 30.00m,
            ReceivedAmount: 20.00m);  // Less than amount

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateCashPayment_WrongMethodType_ReturnsBadRequest()
    {
        var orderId = Guid.NewGuid();
        var request = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.TestCardPaymentMethodId,  // Card method, not cash
            Amount: 30.00m,
            ReceivedAmount: 30.00m);

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateCardPayment_CreatesPendingPayment()
    {
        var orderId = Guid.NewGuid();
        var request = new CreateCardPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.TestCardPaymentMethodId,
            Amount: 50.00m,
            TipAmount: 7.50m);

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/card",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payment = await response.Content.ReadFromJsonAsync<PaymentDto>();
        payment.Should().NotBeNull();
        payment!.Amount.Should().Be(50.00m);
        payment.TipAmount.Should().Be(7.50m);
        payment.TotalAmount.Should().Be(57.50m);
        payment.Status.Should().Be("pending");
        payment.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task CreateCardPayment_WrongMethodType_ReturnsBadRequest()
    {
        var orderId = Guid.NewGuid();
        var request = new CreateCardPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.TestCashPaymentMethodId,  // Cash method, not card
            Amount: 50.00m);

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/card",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CompleteCardPayment_CompletesPayment()
    {
        // First create a card payment
        var orderId = Guid.NewGuid();
        var createRequest = new CreateCardPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.TestCardPaymentMethodId,
            Amount: 45.00m);

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/card",
            createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();

        // Complete the payment
        var completeRequest = new CompleteCardPaymentRequest(
            StripePaymentIntentId: "pi_test_123456",
            CardBrand: "Visa",
            CardLastFour: "4242");

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/{created!.Id}/complete",
            completeRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var completed = await response.Content.ReadFromJsonAsync<PaymentDto>();
        completed!.Status.Should().Be("completed");
        completed.CardBrand.Should().Be("Visa");
        completed.CardLastFour.Should().Be("4242");
        completed.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CompleteCardPayment_AlreadyCompleted_ReturnsBadRequest()
    {
        // The test fixture payment is already completed
        var completeRequest = new CompleteCardPaymentRequest(
            StripePaymentIntentId: "pi_test_999",
            CardBrand: "Mastercard",
            CardLastFour: "5555");

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/{_fixture.TestPaymentId}/complete",
            completeRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Refund_RefundsPayment()
    {
        // First create and complete a cash payment
        var orderId = Guid.NewGuid();
        var createRequest = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.TestCashPaymentMethodId,
            Amount: 30.00m,
            ReceivedAmount: 30.00m);

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();

        // Refund it
        var refundRequest = new RefundPaymentRequest(
            Reason: "Customer changed their mind");

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/{created!.Id}/refund",
            refundRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var refunded = await response.Content.ReadFromJsonAsync<PaymentDto>();
        refunded!.Status.Should().Be("refunded");
    }

    [Fact]
    public async Task Refund_PendingPayment_ReturnsBadRequest()
    {
        // Create a pending card payment (not completed)
        var orderId = Guid.NewGuid();
        var createRequest = new CreateCardPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.TestCardPaymentMethodId,
            Amount: 25.00m);

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/card",
            createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();

        // Try to refund pending payment
        var refundRequest = new RefundPaymentRequest(Reason: "Test");

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/{created!.Id}/refund",
            refundRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Void_VoidsPayment()
    {
        // Create a cash payment
        var orderId = Guid.NewGuid();
        var createRequest = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.TestCashPaymentMethodId,
            Amount: 15.00m,
            ReceivedAmount: 20.00m);

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();

        // Void it
        var voidRequest = new VoidPaymentRequest(
            Reason: "Wrong order");

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/{created!.Id}/void",
            voidRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var voided = await response.Content.ReadFromJsonAsync<PaymentDto>();
        voided!.Status.Should().Be("voided");
    }

    [Fact]
    public async Task Void_AlreadyRefunded_ReturnsBadRequest()
    {
        // Create a cash payment
        var orderId = Guid.NewGuid();
        var createRequest = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.TestCashPaymentMethodId,
            Amount: 10.00m,
            ReceivedAmount: 10.00m);

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<PaymentDto>();

        // Refund it first
        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/{created!.Id}/refund",
            new RefundPaymentRequest(Reason: "First refund"));

        // Try to void
        var voidRequest = new VoidPaymentRequest(Reason: "Try void");

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/{created.Id}/void",
            voidRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
