using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Location.Api.Entities;

public class Location : BaseEntity
{
    public required string Name { get; set; }
    public required string Code { get; set; } // Short code for the location (e.g., "NYC-01")

    // Timezone & Currency
    public required string Timezone { get; set; } // IANA timezone (e.g., "America/New_York")
    public required string CurrencyCode { get; set; } // ISO 4217 (e.g., "USD", "GBP")
    public string CurrencySymbol { get; set; } = "$";

    // Contact Information
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }

    // Address
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }

    // Business Information
    public string? TaxNumber { get; set; } // VAT/GST/Tax ID
    public string? BusinessName { get; set; } // Legal business name

    // Status
    public bool IsActive { get; set; } = true;
    public bool IsOpen { get; set; } = true; // Currently open for business

    // Navigation
    public LocationSettings? Settings { get; set; }
    public List<OperatingHours> OperatingHours { get; set; } = new();
}
