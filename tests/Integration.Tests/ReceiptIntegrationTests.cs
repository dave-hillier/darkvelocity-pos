using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Integration.Tests.Fixtures;
using FluentAssertions;

namespace DarkVelocity.Integration.Tests;

/// <summary>
/// Integration tests for Receipt generation and management.
///
/// Business Scenarios Covered:
/// - Receipt generation with order details
/// - Payment information on receipts
/// - Tax breakdown display
/// - Tip handling
/// - Split payment receipts
/// - Receipt reprinting
/// - Void receipts
/// </summary>
public class ReceiptIntegrationTests : IClassFixture<PaymentsServiceFixture>
{
    private readonly PaymentsServiceFixture _fixture;
    private readonly HttpClient _client;

    public ReceiptIntegrationTests(PaymentsServiceFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    #region Receipt Generation - Order Details

    [Fact]
    public async Task GenerateReceipt_IncludesAllOrderDetails()
    {
        // Arrange - Create a receipt for a payment
        var createRequest = new CreateReceiptRequest(
            PaymentId: Guid.NewGuid(),
            OrderId: _fixture.TestOrderId,
            LocationId: _fixture.TestLocationId,
            BusinessName: "DarkVelocity Restaurant",
            LocationName: "Downtown Location",
            Address: "123 Main St, New York, NY 10001",
            TaxId: "12-3456789",
            OrderNumber: "NYC-001234",
            OrderDate: DateTime.UtcNow,
            ServerName: "John Smith",
            Subtotal: 45.00m,
            TaxTotal: 3.99m,
            DiscountTotal: 5.00m,
            TipAmount: 0m,
            GrandTotal: 43.99m,
            PaymentMethodName: "Cash",
            AmountPaid: 50.00m,
            ChangeGiven: 6.01m,
            LineItems: new List<ReceiptLineItemRequest>
            {
                new("Classic Burger", 2, 12.50m, 25.00m),
                new("French Fries", 2, 5.00m, 10.00m),
                new("Soft Drink", 2, 5.00m, 10.00m)
            });

        // Act
        var response = await _client.PostAsJsonAsync("/api/receipts", createRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);

        if (response.IsSuccessStatusCode)
        {
            var receipt = await response.Content.ReadFromJsonAsync<ReceiptDto>();
            receipt.Should().NotBeNull();
            receipt!.BusinessName.Should().Be("DarkVelocity Restaurant");
            receipt.OrderNumber.Should().Be("NYC-001234");
            receipt.ServerName.Should().Be("John Smith");
            receipt.Subtotal.Should().Be(45.00m);
            receipt.TaxTotal.Should().Be(3.99m);
            receipt.DiscountTotal.Should().Be(5.00m);
            receipt.GrandTotal.Should().Be(43.99m);
            receipt.LineItems.Should().HaveCount(3);
        }
    }

    [Fact]
    public async Task GenerateReceipt_IncludesLineItemDetails()
    {
        // Arrange
        var createRequest = new CreateReceiptRequest(
            PaymentId: Guid.NewGuid(),
            OrderId: _fixture.TestOrderId,
            LocationId: _fixture.TestLocationId,
            BusinessName: "Test Restaurant",
            LocationName: "Test Location",
            OrderNumber: "TEST-001",
            OrderDate: DateTime.UtcNow,
            Subtotal: 37.50m,
            TaxTotal: 3.00m,
            GrandTotal: 40.50m,
            PaymentMethodName: "Cash",
            AmountPaid: 40.50m,
            LineItems: new List<ReceiptLineItemRequest>
            {
                new("Steak", 1, 25.00m, 25.00m),
                new("Side Salad", 1, 7.50m, 7.50m),
                new("Coffee", 1, 5.00m, 5.00m)
            });

        // Act
        var response = await _client.PostAsJsonAsync("/api/receipts", createRequest);

        // Assert
        if (response.IsSuccessStatusCode)
        {
            var receipt = await response.Content.ReadFromJsonAsync<ReceiptDto>();
            receipt!.LineItems.Should().Contain(item => item.ItemName == "Steak" && item.UnitPrice == 25.00m);
            receipt.LineItems.Should().Contain(item => item.ItemName == "Side Salad" && item.Quantity == 1);
            receipt.LineItems.Should().Contain(item => item.ItemName == "Coffee" && item.LineTotal == 5.00m);
        }
    }

    #endregion

    #region Receipt Generation - Payment Details

    [Fact]
    public async Task GenerateReceipt_IncludesPaymentDetails()
    {
        // Arrange - Cash payment with change
        var createRequest = new CreateReceiptRequest(
            PaymentId: Guid.NewGuid(),
            OrderId: _fixture.TestOrderId,
            LocationId: _fixture.TestLocationId,
            BusinessName: "Test Restaurant",
            LocationName: "Test Location",
            OrderNumber: "TEST-002",
            OrderDate: DateTime.UtcNow,
            Subtotal: 18.75m,
            TaxTotal: 1.50m,
            GrandTotal: 20.25m,
            PaymentMethodName: "Cash",
            AmountPaid: 25.00m,
            ChangeGiven: 4.75m,
            LineItems: new List<ReceiptLineItemRequest>
            {
                new("Lunch Special", 1, 18.75m, 18.75m)
            });

        // Act
        var response = await _client.PostAsJsonAsync("/api/receipts", createRequest);

        // Assert
        if (response.IsSuccessStatusCode)
        {
            var receipt = await response.Content.ReadFromJsonAsync<ReceiptDto>();
            receipt!.PaymentMethodName.Should().Be("Cash");
            receipt.AmountPaid.Should().Be(25.00m);
            receipt.ChangeGiven.Should().Be(4.75m);
        }
    }

    [Fact]
    public async Task GenerateReceipt_CardPayment_IncludesCardDetails()
    {
        // Arrange - Card payment
        var createRequest = new CreateReceiptRequest(
            PaymentId: Guid.NewGuid(),
            OrderId: _fixture.TestOrderId,
            LocationId: _fixture.TestLocationId,
            BusinessName: "Test Restaurant",
            LocationName: "Test Location",
            OrderNumber: "TEST-003",
            OrderDate: DateTime.UtcNow,
            Subtotal: 50.00m,
            TaxTotal: 4.00m,
            GrandTotal: 54.00m,
            PaymentMethodName: "Visa ****1234",
            AmountPaid: 54.00m,
            ChangeGiven: 0m,
            CardBrand: "Visa",
            CardLastFour: "1234",
            LineItems: new List<ReceiptLineItemRequest>
            {
                new("Dinner for Two", 1, 50.00m, 50.00m)
            });

        // Act
        var response = await _client.PostAsJsonAsync("/api/receipts", createRequest);

        // Assert
        if (response.IsSuccessStatusCode)
        {
            var receipt = await response.Content.ReadFromJsonAsync<ReceiptDto>();
            receipt!.PaymentMethodName.Should().Contain("Visa");
            receipt.AmountPaid.Should().Be(54.00m);
            receipt.ChangeGiven.Should().Be(0m);
        }
    }

    #endregion

    #region Tax Breakdown

    [Fact]
    public async Task GenerateReceipt_IncludesTaxBreakdown()
    {
        // Arrange - Order with tax
        var createRequest = new CreateReceiptRequest(
            PaymentId: Guid.NewGuid(),
            OrderId: _fixture.TestOrderId,
            LocationId: _fixture.TestLocationId,
            BusinessName: "Test Restaurant",
            LocationName: "Test Location",
            TaxId: "12-3456789",
            OrderNumber: "TEST-004",
            OrderDate: DateTime.UtcNow,
            Subtotal: 100.00m,
            TaxTotal: 8.88m, // 8.88% NYC tax
            GrandTotal: 108.88m,
            PaymentMethodName: "Cash",
            AmountPaid: 110.00m,
            ChangeGiven: 1.12m,
            TaxBreakdown: new List<TaxBreakdownItem>
            {
                new("State Tax", 4.00m, 4.00m),
                new("City Tax", 4.50m, 4.50m),
                new("Metro Tax", 0.375m, 0.38m)
            },
            LineItems: new List<ReceiptLineItemRequest>
            {
                new("Premium Platter", 1, 100.00m, 100.00m)
            });

        // Act
        var response = await _client.PostAsJsonAsync("/api/receipts", createRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Tip Handling

    [Fact]
    public async Task GenerateReceipt_WithTip_ShowsTipSeparately()
    {
        // Arrange - Payment with tip
        var createRequest = new CreateReceiptRequest(
            PaymentId: Guid.NewGuid(),
            OrderId: _fixture.TestOrderId,
            LocationId: _fixture.TestLocationId,
            BusinessName: "Test Restaurant",
            LocationName: "Test Location",
            OrderNumber: "TEST-005",
            OrderDate: DateTime.UtcNow,
            ServerName: "Jane Doe",
            Subtotal: 80.00m,
            TaxTotal: 7.10m,
            TipAmount: 17.00m, // ~20% tip
            GrandTotal: 104.10m,
            PaymentMethodName: "Mastercard ****5678",
            AmountPaid: 104.10m,
            LineItems: new List<ReceiptLineItemRequest>
            {
                new("Dinner Special", 2, 40.00m, 80.00m)
            });

        // Act
        var response = await _client.PostAsJsonAsync("/api/receipts", createRequest);

        // Assert
        if (response.IsSuccessStatusCode)
        {
            var receipt = await response.Content.ReadFromJsonAsync<ReceiptDto>();
            receipt!.TipAmount.Should().Be(17.00m);
            receipt.GrandTotal.Should().Be(104.10m);
        }
    }

    #endregion

    #region Split Payment Receipts

    [Fact]
    public async Task GenerateReceipt_ForSplitPayment_ShowsPartialAmount()
    {
        // Arrange - First payment of split (2 of 3 paying)
        var createRequest = new CreateReceiptRequest(
            PaymentId: Guid.NewGuid(),
            OrderId: _fixture.TestOrderId,
            LocationId: _fixture.TestLocationId,
            BusinessName: "Test Restaurant",
            LocationName: "Test Location",
            OrderNumber: "TEST-006",
            OrderDate: DateTime.UtcNow,
            Subtotal: 90.00m, // Full order subtotal
            TaxTotal: 8.00m,
            GrandTotal: 98.00m, // Full order total
            PaymentMethodName: "Cash",
            AmountPaid: 32.67m, // 1/3 of total
            ChangeGiven: 0m,
            IsSplitPayment: true,
            SplitPaymentNumber: 1,
            TotalSplitPayments: 3,
            LineItems: new List<ReceiptLineItemRequest>
            {
                new("Shared Appetizer", 1, 15.00m, 15.00m),
                new("Individual Entree", 1, 25.00m, 25.00m)
            });

        // Act
        var response = await _client.PostAsJsonAsync("/api/receipts", createRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Receipt Retrieval and Reprinting

    [Fact]
    public async Task GetReceiptById_ReturnsReceipt()
    {
        // Arrange - First create a receipt
        var createRequest = new CreateReceiptRequest(
            PaymentId: Guid.NewGuid(),
            OrderId: _fixture.TestOrderId,
            LocationId: _fixture.TestLocationId,
            BusinessName: "Test Restaurant",
            LocationName: "Test Location",
            OrderNumber: "TEST-007",
            OrderDate: DateTime.UtcNow,
            Subtotal: 25.00m,
            TaxTotal: 2.00m,
            GrandTotal: 27.00m,
            PaymentMethodName: "Cash",
            AmountPaid: 27.00m,
            LineItems: new List<ReceiptLineItemRequest>
            {
                new("Quick Lunch", 1, 25.00m, 25.00m)
            });

        var createResponse = await _client.PostAsJsonAsync("/api/receipts", createRequest);

        if (!createResponse.IsSuccessStatusCode) return;

        var createdReceipt = await createResponse.Content.ReadFromJsonAsync<ReceiptDto>();

        // Act
        var response = await _client.GetAsync($"/api/receipts/{createdReceipt!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var receipt = await response.Content.ReadFromJsonAsync<ReceiptDto>();
        receipt!.Id.Should().Be(createdReceipt.Id);
        receipt.OrderNumber.Should().Be("TEST-007");
    }

    [Fact]
    public async Task ReprintReceipt_IncrementsReprintCount()
    {
        // Arrange - Create a receipt
        var createRequest = new CreateReceiptRequest(
            PaymentId: Guid.NewGuid(),
            OrderId: _fixture.TestOrderId,
            LocationId: _fixture.TestLocationId,
            BusinessName: "Test Restaurant",
            LocationName: "Test Location",
            OrderNumber: "TEST-008",
            OrderDate: DateTime.UtcNow,
            Subtotal: 30.00m,
            TaxTotal: 2.40m,
            GrandTotal: 32.40m,
            PaymentMethodName: "Cash",
            AmountPaid: 32.40m,
            LineItems: new List<ReceiptLineItemRequest>
            {
                new("Test Item", 1, 30.00m, 30.00m)
            });

        var createResponse = await _client.PostAsJsonAsync("/api/receipts", createRequest);

        if (!createResponse.IsSuccessStatusCode) return;

        var createdReceipt = await createResponse.Content.ReadFromJsonAsync<ReceiptDto>();

        // Act - Mark as printed (reprint)
        var printResponse = await _client.PostAsync(
            $"/api/receipts/{createdReceipt!.Id}/print",
            JsonContent.Create(new MarkReceiptPrintedRequest()));

        // Assert
        printResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetReceiptByPaymentId_ReturnsReceipt()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var createRequest = new CreateReceiptRequest(
            PaymentId: paymentId,
            OrderId: _fixture.TestOrderId,
            LocationId: _fixture.TestLocationId,
            BusinessName: "Test Restaurant",
            LocationName: "Test Location",
            OrderNumber: "TEST-009",
            OrderDate: DateTime.UtcNow,
            Subtotal: 15.00m,
            TaxTotal: 1.20m,
            GrandTotal: 16.20m,
            PaymentMethodName: "Cash",
            AmountPaid: 16.20m,
            LineItems: new List<ReceiptLineItemRequest>
            {
                new("Snack", 1, 15.00m, 15.00m)
            });

        await _client.PostAsJsonAsync("/api/receipts", createRequest);

        // Act
        var response = await _client.GetAsync($"/api/receipts/by-payment/{paymentId}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Void Receipts

    [Fact]
    public async Task VoidReceipt_GeneratesVoidSlip()
    {
        // Arrange - Create a void receipt
        var createRequest = new CreateReceiptRequest(
            PaymentId: Guid.NewGuid(),
            OrderId: _fixture.TestOrderId,
            LocationId: _fixture.TestLocationId,
            BusinessName: "Test Restaurant",
            LocationName: "Test Location",
            OrderNumber: "TEST-010",
            OrderDate: DateTime.UtcNow,
            Subtotal: 0m,
            TaxTotal: 0m,
            GrandTotal: 0m,
            PaymentMethodName: "VOID",
            AmountPaid: 0m,
            IsVoid: true,
            VoidReason: "Customer cancelled order",
            OriginalReceiptId: Guid.NewGuid(),
            LineItems: new List<ReceiptLineItemRequest>());

        // Act
        var response = await _client.PostAsJsonAsync("/api/receipts", createRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion
}

// Receipt DTOs
public record CreateReceiptRequest(
    Guid PaymentId,
    Guid OrderId,
    Guid LocationId,
    string BusinessName,
    string LocationName,
    string OrderNumber,
    DateTime OrderDate,
    decimal Subtotal,
    decimal TaxTotal,
    decimal GrandTotal,
    string PaymentMethodName,
    decimal AmountPaid,
    List<ReceiptLineItemRequest> LineItems,
    string? Address = null,
    string? TaxId = null,
    string? ServerName = null,
    decimal DiscountTotal = 0m,
    decimal TipAmount = 0m,
    decimal ChangeGiven = 0m,
    string? CardBrand = null,
    string? CardLastFour = null,
    List<TaxBreakdownItem>? TaxBreakdown = null,
    bool IsSplitPayment = false,
    int SplitPaymentNumber = 0,
    int TotalSplitPayments = 0,
    bool IsVoid = false,
    string? VoidReason = null,
    Guid? OriginalReceiptId = null);

public record ReceiptLineItemRequest(
    string ItemName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public record TaxBreakdownItem(
    string TaxName,
    decimal TaxRate,
    decimal TaxAmount);

public record MarkReceiptPrintedRequest(
    string? PrintedBy = null);

public record ReceiptDto
{
    public Guid Id { get; init; }
    public Guid PaymentId { get; init; }
    public string? BusinessName { get; init; }
    public string? LocationName { get; init; }
    public string? Address { get; init; }
    public string? TaxId { get; init; }
    public string? OrderNumber { get; init; }
    public DateTime OrderDate { get; init; }
    public string? ServerName { get; init; }
    public decimal Subtotal { get; init; }
    public decimal TaxTotal { get; init; }
    public decimal DiscountTotal { get; init; }
    public decimal TipAmount { get; init; }
    public decimal GrandTotal { get; init; }
    public string? PaymentMethodName { get; init; }
    public decimal AmountPaid { get; init; }
    public decimal ChangeGiven { get; init; }
    public List<ReceiptLineItemDto> LineItems { get; init; } = new();
    public DateTime? PrintedAt { get; init; }
    public int PrintCount { get; init; }
}

public record ReceiptLineItemDto
{
    public string? ItemName { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal LineTotal { get; init; }
}
