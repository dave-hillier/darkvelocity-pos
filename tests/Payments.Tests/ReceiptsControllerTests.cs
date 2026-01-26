using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Payments.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Payments.Tests;

public class ReceiptsControllerTests : IClassFixture<PaymentsApiFixture>
{
    private readonly PaymentsApiFixture _fixture;
    private readonly HttpClient _client;

    public ReceiptsControllerTests(PaymentsApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task Create_CreatesReceipt()
    {
        // First create a payment
        var orderId = Guid.NewGuid();
        var paymentRequest = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.TestCashPaymentMethodId,
            Amount: 45.00m,
            TipAmount: 5.00m,
            ReceivedAmount: 60.00m);

        var paymentResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            paymentRequest);
        var payment = await paymentResponse.Content.ReadFromJsonAsync<PaymentDto>();

        // Create receipt
        var lineItems = new List<ReceiptLineItemDto>
        {
            new() { ItemName = "Burger", Quantity = 2, UnitPrice = 15.00m, LineTotal = 30.00m },
            new() { ItemName = "Fries", Quantity = 2, UnitPrice = 5.00m, LineTotal = 10.00m },
            new() { ItemName = "Drink", Quantity = 2, UnitPrice = 2.50m, LineTotal = 5.00m }
        };

        var receiptRequest = new CreateReceiptRequest(
            PaymentId: payment!.Id,
            BusinessName: "DarkVelocity Restaurant",
            LocationName: "Main Street",
            AddressLine1: "123 Main Street",
            AddressLine2: "London, UK",
            TaxId: "GB123456789",
            OrderNumber: "ORD-001",
            OrderDate: DateTime.UtcNow,
            ServerName: "John",
            Subtotal: 45.00m,
            TaxTotal: 9.00m,
            DiscountTotal: 0m,
            TipAmount: 5.00m,
            GrandTotal: 59.00m,
            PaymentMethodName: "Cash",
            AmountPaid: 60.00m,
            ChangeGiven: 1.00m,
            LineItems: lineItems);

        var response = await _client.PostAsJsonAsync("/api/receipts", receiptRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var receipt = await response.Content.ReadFromJsonAsync<ReceiptDto>();
        receipt.Should().NotBeNull();
        receipt!.BusinessName.Should().Be("DarkVelocity Restaurant");
        receipt.OrderNumber.Should().Be("ORD-001");
        receipt.LineItems.Should().HaveCount(3);
        receipt.GrandTotal.Should().Be(59.00m);
        receipt.PrintCount.Should().Be(0);
    }

    [Fact]
    public async Task Create_InvalidPayment_ReturnsBadRequest()
    {
        var receiptRequest = new CreateReceiptRequest(
            PaymentId: Guid.NewGuid(),  // Non-existent payment
            GrandTotal: 50.00m);

        var response = await _client.PostAsJsonAsync("/api/receipts", receiptRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_DuplicateReceipt_ReturnsConflict()
    {
        // Create a payment
        var orderId = Guid.NewGuid();
        var paymentRequest = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.TestCashPaymentMethodId,
            Amount: 20.00m,
            ReceivedAmount: 20.00m);

        var paymentResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            paymentRequest);
        var payment = await paymentResponse.Content.ReadFromJsonAsync<PaymentDto>();

        // Create first receipt
        var receiptRequest = new CreateReceiptRequest(
            PaymentId: payment!.Id,
            GrandTotal: 20.00m);

        await _client.PostAsJsonAsync("/api/receipts", receiptRequest);

        // Try to create duplicate
        var response = await _client.PostAsJsonAsync("/api/receipts", receiptRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetById_ReturnsReceipt()
    {
        // Create a payment and receipt
        var orderId = Guid.NewGuid();
        var paymentRequest = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.TestCashPaymentMethodId,
            Amount: 30.00m,
            ReceivedAmount: 30.00m);

        var paymentResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            paymentRequest);
        var payment = await paymentResponse.Content.ReadFromJsonAsync<PaymentDto>();

        var receiptRequest = new CreateReceiptRequest(
            PaymentId: payment!.Id,
            BusinessName: "Test Business",
            GrandTotal: 30.00m);

        var createResponse = await _client.PostAsJsonAsync("/api/receipts", receiptRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<ReceiptDto>();

        // Get by ID
        var response = await _client.GetAsync($"/api/receipts/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var receipt = await response.Content.ReadFromJsonAsync<ReceiptDto>();
        receipt!.Id.Should().Be(created.Id);
        receipt.BusinessName.Should().Be("Test Business");
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/receipts/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByPayment_ReturnsReceipt()
    {
        // Create a payment and receipt
        var orderId = Guid.NewGuid();
        var paymentRequest = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.TestCashPaymentMethodId,
            Amount: 25.00m,
            ReceivedAmount: 25.00m);

        var paymentResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            paymentRequest);
        var payment = await paymentResponse.Content.ReadFromJsonAsync<PaymentDto>();

        var receiptRequest = new CreateReceiptRequest(
            PaymentId: payment!.Id,
            OrderNumber: "TEST-123",
            GrandTotal: 25.00m);

        await _client.PostAsJsonAsync("/api/receipts", receiptRequest);

        // Get by payment ID
        var response = await _client.GetAsync($"/api/receipts/by-payment/{payment.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var receipt = await response.Content.ReadFromJsonAsync<ReceiptDto>();
        receipt!.PaymentId.Should().Be(payment.Id);
        receipt.OrderNumber.Should().Be("TEST-123");
    }

    [Fact]
    public async Task GetByPayment_NoReceipt_ReturnsNotFound()
    {
        // Create a payment without a receipt
        var orderId = Guid.NewGuid();
        var paymentRequest = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.TestCashPaymentMethodId,
            Amount: 15.00m,
            ReceivedAmount: 15.00m);

        var paymentResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            paymentRequest);
        var payment = await paymentResponse.Content.ReadFromJsonAsync<PaymentDto>();

        // Get by payment ID (no receipt exists)
        var response = await _client.GetAsync($"/api/receipts/by-payment/{payment!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkPrinted_IncrementsPrintCount()
    {
        // Create a payment and receipt
        var orderId = Guid.NewGuid();
        var paymentRequest = new CreateCashPaymentRequest(
            OrderId: orderId,
            UserId: _fixture.TestUserId,
            PaymentMethodId: _fixture.TestCashPaymentMethodId,
            Amount: 40.00m,
            ReceivedAmount: 50.00m);

        var paymentResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/payments/cash",
            paymentRequest);
        var payment = await paymentResponse.Content.ReadFromJsonAsync<PaymentDto>();

        var receiptRequest = new CreateReceiptRequest(
            PaymentId: payment!.Id,
            GrandTotal: 40.00m);

        var createResponse = await _client.PostAsJsonAsync("/api/receipts", receiptRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<ReceiptDto>();

        // Mark as printed
        var response = await _client.PostAsJsonAsync($"/api/receipts/{created!.Id}/print", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var printed = await response.Content.ReadFromJsonAsync<ReceiptDto>();
        printed!.PrintCount.Should().Be(1);
        printed.PrintedAt.Should().NotBeNull();

        // Print again
        var response2 = await _client.PostAsJsonAsync($"/api/receipts/{created.Id}/print", new { });
        var printed2 = await response2.Content.ReadFromJsonAsync<ReceiptDto>();
        printed2!.PrintCount.Should().Be(2);
    }

    [Fact]
    public async Task MarkPrinted_NotFound_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync($"/api/receipts/{Guid.NewGuid()}/print", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
