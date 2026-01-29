using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.PaymentGateway.Api.Entities;

public class Merchant : BaseEntity
{
    public required string Name { get; set; }
    public required string Email { get; set; }
    public string? BusinessName { get; set; }
    public string? BusinessType { get; set; } // individual, company, non_profit
    public string? Country { get; set; } = "US";
    public string? DefaultCurrency { get; set; } = "USD";

    // Address
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }

    // Status
    public string Status { get; set; } = "active"; // active, suspended, closed
    public bool PayoutsEnabled { get; set; } = true;
    public bool ChargesEnabled { get; set; } = true;

    // Settings
    public string? StatementDescriptor { get; set; }
    public string? WebhookUrl { get; set; }

    // Metadata
    public string? Metadata { get; set; } // JSON key-value pairs

    // Navigation
    public ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
    public ICollection<PaymentIntent> PaymentIntents { get; set; } = new List<PaymentIntent>();
    public ICollection<Terminal> Terminals { get; set; } = new List<Terminal>();
    public ICollection<WebhookEndpoint> WebhookEndpoints { get; set; } = new List<WebhookEndpoint>();
}
