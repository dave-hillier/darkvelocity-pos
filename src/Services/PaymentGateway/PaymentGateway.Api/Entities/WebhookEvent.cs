using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.PaymentGateway.Api.Entities;

public class WebhookEvent : BaseEntity
{
    public Guid MerchantId { get; set; }
    public Guid WebhookEndpointId { get; set; }

    // Event details
    public required string EventType { get; set; } // payment_intent.succeeded, refund.created, etc.
    public required string ObjectType { get; set; } // payment_intent, refund, terminal
    public Guid ObjectId { get; set; }

    // Payload
    public required string Payload { get; set; } // JSON payload sent to webhook

    // Delivery status
    public string Status { get; set; } = "pending"; // pending, delivered, failed
    public int DeliveryAttempts { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? DeliveredAt { get; set; }

    // Response tracking
    public int? ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public string? ErrorMessage { get; set; }

    // Next retry
    public DateTime? NextRetryAt { get; set; }

    // Navigation
    public Merchant? Merchant { get; set; }
    public WebhookEndpoint? WebhookEndpoint { get; set; }
}
