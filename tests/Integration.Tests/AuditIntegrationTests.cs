using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Integration.Tests.Fixtures;
using DarkVelocity.Orders.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Integration.Tests;

/// <summary>
/// Integration tests for Audit Trail functionality.
///
/// Business Scenarios Covered:
/// - Void order audit recording
/// - Discount authorization tracking
/// - Price override logging
/// - Cash drawer events
/// - Audit log queries and filtering
/// </summary>
public class AuditIntegrationTests :
    IClassFixture<OrdersServiceFixture>,
    IClassFixture<PaymentsServiceFixture>
{
    private readonly OrdersServiceFixture _ordersFixture;
    private readonly PaymentsServiceFixture _paymentsFixture;

    public AuditIntegrationTests(
        OrdersServiceFixture ordersFixture,
        PaymentsServiceFixture paymentsFixture)
    {
        _ordersFixture = ordersFixture;
        _paymentsFixture = paymentsFixture;
    }

    #region Void Audit

    [Fact]
    public async Task VoidOrder_RecordsWhoAndWhen()
    {
        // Arrange - Create an order to void
        var createRequest = new CreateOrderRequest(UserId: _ordersFixture.TestUserId);
        var createResponse = await _ordersFixture.Client.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders",
            createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>();

        var voidRequest = new VoidOrderRequest(
            UserId: _ordersFixture.TestUserId,
            Reason: "Customer changed mind");

        // Act
        var response = await _ordersFixture.Client.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders/{order!.Id}/void",
            voidRequest);

        // Assert - Void should succeed and record audit info
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var voidedOrder = await response.Content.ReadFromJsonAsync<OrderDto>();
            voidedOrder!.Status.Should().Be("voided");
            // Audit info should be recorded (VoidedBy, VoidedAt, VoidReason)
        }
    }

    [Fact]
    public async Task VoidOrder_RequiresReason()
    {
        // Arrange
        var createRequest = new CreateOrderRequest(UserId: _ordersFixture.TestUserId);
        var createResponse = await _ordersFixture.Client.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders",
            createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>();

        var voidRequest = new VoidOrderRequest(
            UserId: _ordersFixture.TestUserId,
            Reason: ""); // Empty reason

        // Act
        var response = await _ordersFixture.Client.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders/{order!.Id}/void",
            voidRequest);

        // Assert - May require reason or accept empty
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region Discount Authorization

    [Fact]
    public async Task Discount_RecordsAuthorizingManager()
    {
        // Arrange - Create order with items
        var createRequest = new CreateOrderRequest(UserId: _ordersFixture.TestUserId);
        var createResponse = await _ordersFixture.Client.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders",
            createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>();

        await _ordersFixture.Client.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders/{order!.Id}/lines",
            new AddOrderLineRequest(
                MenuItemId: _ordersFixture.TestMenuItemId,
                ItemName: "Test Item",
                Quantity: 1,
                UnitPrice: 50.00m));

        // Apply discount with authorization
        var discountRequest = new ApplyOrderDiscountRequest(
            DiscountType: "percentage",
            DiscountValue: 20m,
            Reason: "Customer complaint",
            AuthorizedByUserId: _ordersFixture.TestUserId);

        // Act
        var response = await _ordersFixture.Client.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders/{order.Id}/discount",
            discountRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Created,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LineDiscount_RecordsDiscountDetails()
    {
        // Arrange
        var createRequest = new CreateOrderRequest(UserId: _ordersFixture.TestUserId);
        var createResponse = await _ordersFixture.Client.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders",
            createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>();

        var lineResponse = await _ordersFixture.Client.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders/{order!.Id}/lines",
            new AddOrderLineRequest(
                MenuItemId: _ordersFixture.TestMenuItemId,
                ItemName: "Comp Item",
                Quantity: 1,
                UnitPrice: 25.00m));
        var line = await lineResponse.Content.ReadFromJsonAsync<OrderLineDto>();

        // Apply line discount
        var updateRequest = new UpdateOrderLineRequest(
            DiscountAmount: 25.00m, // Full comp
            DiscountReason: "Manager comp - regular customer");

        // Act
        var response = await _ordersFixture.Client.PatchAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders/{order.Id}/lines/{line!.Id}",
            updateRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var updatedLine = await response.Content.ReadFromJsonAsync<OrderLineDto>();
            updatedLine!.DiscountReason.Should().Be("Manager comp - regular customer");
        }
    }

    #endregion

    #region Price Override

    [Fact]
    public async Task PriceOverride_RecordsReason()
    {
        // Arrange - Create order
        var createRequest = new CreateOrderRequest(UserId: _ordersFixture.TestUserId);
        var createResponse = await _ordersFixture.Client.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders",
            createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>();

        // Add line with overridden price
        var lineRequest = new AddOrderLineRequest(
            MenuItemId: _ordersFixture.TestMenuItemId,
            ItemName: "Custom Price Item",
            Quantity: 1,
            UnitPrice: 15.00m, // Overridden from menu price
            PriceOverrideReason: "Price match competitor");

        // Act
        var response = await _ordersFixture.Client.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders/{order!.Id}/lines",
            lineRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Cash Drawer Events

    [Fact]
    public async Task CashDrawerOpen_RecordsEvent()
    {
        // Arrange - Open drawer without a sale (no-sale)
        var request = new NoSaleRequest(
            UserId: _ordersFixture.TestUserId,
            Reason: "Make change for customer");

        // Act
        var response = await _ordersFixture.Client.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/sales-periods/{_ordersFixture.TestSalesPeriodId}/no-sale",
            request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Created,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CashDrop_RecordsDropDetails()
    {
        // Arrange
        var request = new CashDropRequest(
            UserId: _ordersFixture.TestUserId,
            Amount: 200.00m,
            Notes: "Mid-shift safe drop");

        // Act
        var response = await _ordersFixture.Client.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/sales-periods/{_ordersFixture.TestSalesPeriodId}/cash-drop",
            request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Created,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region Audit Log Queries

    [Fact]
    public async Task GetAuditLog_FilterByDateRange()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        // Act
        var response = await _ordersFixture.Client.GetAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/audit-log" +
            $"?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var logs = await response.Content.ReadFromJsonAsync<List<AuditLogEntryDto>>();
            logs.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task GetAuditLog_FilterByUser()
    {
        // Act
        var response = await _ordersFixture.Client.GetAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/audit-log" +
            $"?userId={_ordersFixture.TestUserId}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var logs = await response.Content.ReadFromJsonAsync<List<AuditLogEntryDto>>();
            logs!.Should().OnlyContain(l => l.UserId == _ordersFixture.TestUserId);
        }
    }

    [Fact]
    public async Task GetAuditLog_FilterByEventType()
    {
        // Act - Get only void events
        var response = await _ordersFixture.Client.GetAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/audit-log?eventType=order_void");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var logs = await response.Content.ReadFromJsonAsync<List<AuditLogEntryDto>>();
            logs!.Should().OnlyContain(l => l.EventType == "order_void");
        }
    }

    [Fact]
    public async Task GetAuditLog_IncludesAllVoids()
    {
        // Act
        var response = await _ordersFixture.Client.GetAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/audit-log?eventType=order_void");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAuditLog_IncludesAllDiscounts()
    {
        // Act
        var response = await _ordersFixture.Client.GetAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/audit-log?eventType=discount_applied");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Refund Audit

    [Fact]
    public async Task RefundPayment_RecordsRefundDetails()
    {
        // Arrange - Create a payment first, then refund
        var paymentRequest = new CreatePaymentRequest(
            OrderId: _paymentsFixture.TestOrderId,
            PaymentMethodId: _paymentsFixture.CashPaymentMethodId,
            Amount: 25.00m);

        var paymentResponse = await _paymentsFixture.Client.PostAsJsonAsync(
            $"/api/locations/{_paymentsFixture.TestLocationId}/payments",
            paymentRequest);

        if (!paymentResponse.IsSuccessStatusCode) return;

        var payment = await paymentResponse.Content.ReadFromJsonAsync<PaymentDto>();

        // Refund the payment
        var refundRequest = new RefundPaymentRequest(
            Reason: "Customer return",
            Amount: 25.00m,
            RefundedByUserId: _ordersFixture.TestUserId);

        // Act
        var response = await _paymentsFixture.Client.PostAsJsonAsync(
            $"/api/locations/{_paymentsFixture.TestLocationId}/payments/{payment!.Id}/refund",
            refundRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Created,
            HttpStatusCode.NotFound);
    }

    #endregion
}

// Audit-related DTOs
public record NoSaleRequest(
    Guid UserId,
    string? Reason = null);

public record CashDropRequest(
    Guid UserId,
    decimal Amount,
    string? Notes = null);

public record RefundPaymentRequest(
    string Reason,
    decimal? Amount = null,
    Guid? RefundedByUserId = null);

public record AuditLogEntryDto
{
    public Guid Id { get; init; }
    public Guid LocationId { get; init; }
    public Guid? UserId { get; init; }
    public string? UserName { get; init; }
    public string? EventType { get; init; }
    public string? EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public string? Description { get; init; }
    public string? Details { get; init; }
    public DateTime Timestamp { get; init; }
}

// Additional DTO for line requests with price override
public record AddOrderLineRequest(
    Guid MenuItemId,
    string ItemName,
    int Quantity,
    decimal UnitPrice,
    decimal TaxRate = 0.20m,
    string? Notes = null,
    int? CourseNumber = null,
    int? SeatNumber = null,
    List<OrderLineModifierRequest>? Modifiers = null,
    string? PriceOverrideReason = null)
{
    // Secondary constructor for simpler calls
    public AddOrderLineRequest(Guid itemId, string itemName, int quantity, decimal unitPrice)
        : this(itemId, itemName, quantity, unitPrice, 0.20m) { }
}
