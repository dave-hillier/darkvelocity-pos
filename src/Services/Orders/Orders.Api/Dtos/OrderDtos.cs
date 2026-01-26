using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Orders.Api.Dtos;

public class SalesPeriodDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public Guid OpenedByUserId { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal OpeningCashAmount { get; set; }
    public decimal? ClosingCashAmount { get; set; }
    public decimal? ExpectedCashAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public int OrderCount { get; set; }
}

public class OrderDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public Guid? SalesPeriodId { get; set; }
    public Guid UserId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? TableId { get; set; }
    public int? GuestCount { get; set; }
    public string? CustomerName { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OrderLineDto> Lines { get; set; } = new();
}

public class OrderLineDto : HalResource
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid MenuItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineTotal { get; set; }
    public string? Notes { get; set; }
    public int? CourseNumber { get; set; }
    public int? SeatNumber { get; set; }
    public bool IsVoided { get; set; }
}

public record OpenSalesPeriodRequest(
    Guid UserId,
    decimal OpeningCashAmount);

public record CloseSalesPeriodRequest(
    Guid UserId,
    decimal ClosingCashAmount);

public record CreateOrderRequest(
    Guid UserId,
    string OrderType = "direct_sale",
    Guid? TableId = null,
    int? GuestCount = null,
    string? CustomerName = null);

public record AddOrderLineRequest(
    Guid MenuItemId,
    string ItemName,
    int Quantity,
    decimal UnitPrice,
    decimal TaxRate,
    string? Notes = null,
    int? CourseNumber = null,
    int? SeatNumber = null);

public record UpdateOrderLineRequest(
    int? Quantity = null,
    decimal? DiscountAmount = null,
    string? DiscountReason = null,
    string? Notes = null);

public record VoidOrderLineRequest(
    Guid UserId,
    string Reason);

public record SendOrderRequest();

public record CompleteOrderRequest();

public record VoidOrderRequest(
    Guid UserId,
    string Reason);
