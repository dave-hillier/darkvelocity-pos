using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.PaymentGateway.Api.Entities;

public class ApiKey : BaseEntity
{
    public Guid MerchantId { get; set; }

    public required string KeyType { get; set; } // secret, publishable
    public required string KeyPrefix { get; set; } // sk_live_, pk_live_, sk_test_, pk_test_
    public required string KeyHash { get; set; } // SHA256 hash of the key
    public string? KeyHint { get; set; } // Last 4 characters for identification

    public required string Name { get; set; } // User-friendly name
    public bool IsLive { get; set; } // true = live mode, false = test mode
    public bool IsActive { get; set; } = true;

    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    // Navigation
    public Merchant? Merchant { get; set; }
}
