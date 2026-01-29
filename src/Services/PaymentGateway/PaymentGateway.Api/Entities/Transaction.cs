using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.PaymentGateway.Api.Entities;

public class Transaction : BaseEntity
{
    public Guid MerchantId { get; set; }
    public Guid PaymentIntentId { get; set; }

    // Transaction type: authorization, capture, charge (auth+capture), void
    public required string Type { get; set; }

    // Amount
    public long Amount { get; set; } // Amount in smallest currency unit
    public required string Currency { get; set; }

    // Status: pending, succeeded, failed
    public string Status { get; set; } = "pending";

    // Card details
    public string? CardBrand { get; set; }
    public string? CardLast4 { get; set; }
    public string? CardFunding { get; set; }

    // Processing details
    public string? AuthorizationCode { get; set; }
    public string? NetworkTransactionId { get; set; }
    public string? ProcessorResponseCode { get; set; }
    public string? ProcessorResponseText { get; set; }

    // Risk assessment
    public string? RiskLevel { get; set; } // normal, elevated, highest
    public int? RiskScore { get; set; }

    // Failure details
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public string? DeclineCode { get; set; }

    // Metadata
    public string? Metadata { get; set; }

    // Navigation
    public Merchant? Merchant { get; set; }
    public PaymentIntent? PaymentIntent { get; set; }
}
