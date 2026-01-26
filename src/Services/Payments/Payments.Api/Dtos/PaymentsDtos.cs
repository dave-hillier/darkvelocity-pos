using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Payments.Api.Dtos;

// PaymentMethod DTOs
public class PaymentMethodDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public required string Name { get; set; }
    public required string MethodType { get; set; }
    public bool IsActive { get; set; }
    public bool RequiresTip { get; set; }
    public bool OpensDrawer { get; set; }
    public int DisplayOrder { get; set; }
    public bool RequiresExternalTerminal { get; set; }
}

public record CreatePaymentMethodRequest(
    string Name,
    string MethodType,
    bool RequiresTip = false,
    bool OpensDrawer = true,
    int DisplayOrder = 0,
    bool RequiresExternalTerminal = false);

public record UpdatePaymentMethodRequest(
    string? Name = null,
    bool? IsActive = null,
    bool? RequiresTip = null,
    bool? OpensDrawer = null,
    int? DisplayOrder = null,
    bool? RequiresExternalTerminal = null);

// Payment DTOs
public class PaymentDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public Guid PaymentMethodId { get; set; }
    public string? PaymentMethodName { get; set; }
    public string? PaymentMethodType { get; set; }
    public decimal Amount { get; set; }
    public decimal TipAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public string? Status { get; set; }
    public string? CardBrand { get; set; }
    public string? CardLastFour { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public record CreateCashPaymentRequest(
    Guid OrderId,
    Guid UserId,
    Guid PaymentMethodId,
    decimal Amount,
    decimal TipAmount = 0,
    decimal ReceivedAmount = 0);

public record CreateCardPaymentRequest(
    Guid OrderId,
    Guid UserId,
    Guid PaymentMethodId,
    decimal Amount,
    decimal TipAmount = 0);

public record CompleteCardPaymentRequest(
    string StripePaymentIntentId,
    string? CardBrand = null,
    string? CardLastFour = null);

public record RefundPaymentRequest(
    string Reason);

public record VoidPaymentRequest(
    string Reason);

// Stripe DTOs
public class CreatePaymentIntentResponse
{
    public string? ClientSecret { get; set; }
    public string? PaymentIntentId { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
}

// Receipt DTOs
public class ReceiptDto : HalResource
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public string? BusinessName { get; set; }
    public string? LocationName { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? TaxId { get; set; }
    public string? OrderNumber { get; set; }
    public DateTime OrderDate { get; set; }
    public string? ServerName { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TipAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string? PaymentMethodName { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal ChangeGiven { get; set; }
    public List<ReceiptLineItemDto> LineItems { get; set; } = new();
    public DateTime? PrintedAt { get; set; }
    public int PrintCount { get; set; }
}

public class ReceiptLineItemDto
{
    public string? ItemName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public record CreateReceiptRequest(
    Guid PaymentId,
    string? BusinessName = null,
    string? LocationName = null,
    string? AddressLine1 = null,
    string? AddressLine2 = null,
    string? TaxId = null,
    string? OrderNumber = null,
    DateTime? OrderDate = null,
    string? ServerName = null,
    decimal Subtotal = 0,
    decimal TaxTotal = 0,
    decimal DiscountTotal = 0,
    decimal TipAmount = 0,
    decimal GrandTotal = 0,
    string? PaymentMethodName = null,
    decimal AmountPaid = 0,
    decimal ChangeGiven = 0,
    List<ReceiptLineItemDto>? LineItems = null);

public record MarkReceiptPrintedRequest();
