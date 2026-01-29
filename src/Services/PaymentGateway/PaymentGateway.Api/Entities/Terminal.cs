using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.PaymentGateway.Api.Entities;

public class Terminal : BaseEntity
{
    public Guid MerchantId { get; set; }

    // Terminal identification
    public required string Label { get; set; } // User-friendly name
    public required string DeviceType { get; set; } // verifone_p400, stripe_m2, bbpos_wisepos_e, simulated
    public string? SerialNumber { get; set; }
    public string? DeviceSwVersion { get; set; }

    // Location info for POS
    public string? LocationName { get; set; }
    public string? LocationAddress { get; set; }
    public Guid? ExternalLocationId { get; set; } // Link to POS location

    // Registration
    public string? RegistrationCode { get; set; } // One-time code for pairing
    public DateTime? RegistrationCodeExpiresAt { get; set; }
    public bool IsRegistered { get; set; }
    public DateTime? RegisteredAt { get; set; }

    // Status
    public string Status { get; set; } = "pending"; // pending, online, offline
    public DateTime? LastSeenAt { get; set; }
    public string? IpAddress { get; set; }

    // Metadata
    public string? Metadata { get; set; }

    // Navigation
    public Merchant? Merchant { get; set; }
    public ICollection<PaymentIntent> PaymentIntents { get; set; } = new List<PaymentIntent>();
}
