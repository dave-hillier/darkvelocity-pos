using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.PaymentGateway.Api.Entities;

public class PaymentIntent : BaseEntity
{
    public Guid MerchantId { get; set; }

    // Amount and Currency
    public long Amount { get; set; } // Amount in smallest currency unit (cents)
    public long AmountCapturable { get; set; }
    public long AmountReceived { get; set; }
    public required string Currency { get; set; } // ISO 4217 (usd, eur, gbp)

    // Status: requires_payment_method, requires_confirmation, requires_action,
    //         processing, requires_capture, canceled, succeeded
    public string Status { get; set; } = "requires_payment_method";

    // Capture method: automatic, manual (for auth-then-capture)
    public string CaptureMethod { get; set; } = "automatic";

    // Confirmation method: automatic, manual
    public string ConfirmationMethod { get; set; } = "automatic";

    // Payment channel: pos (card-present), ecommerce (card-not-present)
    public string Channel { get; set; } = "ecommerce";

    // Client secret for frontend SDK
    public required string ClientSecret { get; set; }

    // Payment method details (once attached)
    public Guid? PaymentMethodId { get; set; }
    public string? PaymentMethodType { get; set; } // card, card_present

    // Card details (populated after confirmation)
    public string? CardBrand { get; set; } // visa, mastercard, amex, etc.
    public string? CardLast4 { get; set; }
    public string? CardExpMonth { get; set; }
    public string? CardExpYear { get; set; }
    public string? CardFunding { get; set; } // credit, debit, prepaid

    // Terminal (for POS)
    public Guid? TerminalId { get; set; }

    // Description and metadata
    public string? Description { get; set; }
    public string? StatementDescriptor { get; set; }
    public string? StatementDescriptorSuffix { get; set; }
    public string? ReceiptEmail { get; set; }
    public string? Metadata { get; set; } // JSON key-value pairs

    // External references
    public string? ExternalOrderId { get; set; }
    public string? ExternalCustomerId { get; set; }

    // Cancellation
    public string? CancellationReason { get; set; } // duplicate, fraudulent, requested_by_customer, abandoned
    public DateTime? CanceledAt { get; set; }

    // Processing timestamps
    public DateTime? ProcessingAt { get; set; }
    public DateTime? SucceededAt { get; set; }

    // Error handling
    public string? LastErrorCode { get; set; }
    public string? LastErrorMessage { get; set; }

    // Navigation
    public Merchant? Merchant { get; set; }
    public Terminal? Terminal { get; set; }
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Refund> Refunds { get; set; } = new List<Refund>();
}
