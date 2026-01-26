using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Payments.Api.Entities;

public class Receipt : BaseEntity
{
    public Guid PaymentId { get; set; }

    // Receipt header info
    public string? BusinessName { get; set; }
    public string? LocationName { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? TaxId { get; set; }

    // Order info
    public string? OrderNumber { get; set; }
    public DateTime OrderDate { get; set; }
    public string? ServerName { get; set; }

    // Amounts
    public decimal Subtotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TipAmount { get; set; }
    public decimal GrandTotal { get; set; }

    // Payment info
    public string? PaymentMethodName { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal ChangeGiven { get; set; }

    // Line items stored as JSON
    public string LineItemsJson { get; set; } = "[]";

    public DateTime? PrintedAt { get; set; }
    public int PrintCount { get; set; }

    // Navigation
    public Payment? Payment { get; set; }
}
