using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.PaymentGateway.Api.Entities;

public class WebhookEndpoint : BaseEntity
{
    public Guid MerchantId { get; set; }

    // Endpoint configuration
    public required string Url { get; set; }
    public required string Secret { get; set; } // For signature verification
    public string? Description { get; set; }

    // Enabled events (comma-separated list or * for all)
    // payment_intent.created, payment_intent.succeeded, payment_intent.canceled,
    // refund.created, refund.succeeded, terminal.online, terminal.offline
    public string EnabledEvents { get; set; } = "*";

    // Status
    public bool IsActive { get; set; } = true;
    public string? ApiVersion { get; set; } = "2024-01-01";

    // Health tracking
    public int ConsecutiveFailures { get; set; }
    public DateTime? DisabledAt { get; set; }
    public string? DisabledReason { get; set; }

    // Metadata
    public string? Metadata { get; set; }

    // Navigation
    public Merchant? Merchant { get; set; }
    public ICollection<WebhookEvent> WebhookEvents { get; set; } = new List<WebhookEvent>();
}
