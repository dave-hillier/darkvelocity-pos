using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Location.Api.Entities;

public class LocationSettings : BaseEntity
{
    public Guid LocationId { get; set; }

    // Tax Settings
    public decimal DefaultTaxRate { get; set; } // Percentage (e.g., 20.00 for 20%)
    public bool TaxIncludedInPrices { get; set; } = true; // UK/EU style vs US style

    // Receipt Settings
    public string? ReceiptHeader { get; set; }
    public string? ReceiptFooter { get; set; }
    public bool PrintReceiptByDefault { get; set; } = true;
    public bool ShowTaxBreakdown { get; set; } = true;

    // Order Settings
    public bool RequireTableForDineIn { get; set; } = false;
    public bool AutoPrintKitchenTickets { get; set; } = true;
    public int OrderNumberResetHour { get; set; } = 4; // Reset at 4 AM
    public string OrderNumberPrefix { get; set; } = "";

    // Payment Settings
    public bool AllowCashPayments { get; set; } = true;
    public bool AllowCardPayments { get; set; } = true;
    public bool TipsEnabled { get; set; } = true;
    public decimal[] TipSuggestions { get; set; } = [10, 15, 20]; // Percentages

    // Inventory Settings
    public bool TrackInventory { get; set; } = true;
    public bool WarnOnLowStock { get; set; } = true;
    public bool AllowNegativeStock { get; set; } = false;

    // Navigation
    public Location? Location { get; set; }
}
