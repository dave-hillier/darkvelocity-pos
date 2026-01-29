using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.PaymentGateway.Api.Entities;

public class Refund : BaseEntity
{
    public Guid MerchantId { get; set; }
    public Guid PaymentIntentId { get; set; }

    // Amount
    public long Amount { get; set; } // Amount in smallest currency unit
    public required string Currency { get; set; }

    // Status: pending, succeeded, failed, canceled
    public string Status { get; set; } = "pending";

    // Reason: duplicate, fraudulent, requested_by_customer
    public string? Reason { get; set; }

    // Receipt number for customer
    public string? ReceiptNumber { get; set; }

    // Processing
    public string? FailureReason { get; set; }
    public DateTime? SucceededAt { get; set; }
    public DateTime? FailedAt { get; set; }

    // Metadata
    public string? Metadata { get; set; }

    // Navigation
    public Merchant? Merchant { get; set; }
    public PaymentIntent? PaymentIntent { get; set; }
}
