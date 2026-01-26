using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Location.Api.Dtos;

// Location DTOs
public class LocationDto : HalResource
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string Timezone { get; set; } = "";
    public string CurrencyCode { get; set; } = "";
    public string CurrencySymbol { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public AddressDto? Address { get; set; }
    public string? TaxNumber { get; set; }
    public string? BusinessName { get; set; }
    public bool IsActive { get; set; }
    public bool IsOpen { get; set; }
    public LocationSettingsDto? Settings { get; set; }
    public List<OperatingHoursDto>? OperatingHours { get; set; }
}

public class AddressDto
{
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}

public class LocationSummaryDto : HalResource
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string? City { get; set; }
    public bool IsActive { get; set; }
    public bool IsOpen { get; set; }
}

// Location Settings DTOs
public class LocationSettingsDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public decimal DefaultTaxRate { get; set; }
    public bool TaxIncludedInPrices { get; set; }
    public string? ReceiptHeader { get; set; }
    public string? ReceiptFooter { get; set; }
    public bool PrintReceiptByDefault { get; set; }
    public bool ShowTaxBreakdown { get; set; }
    public bool RequireTableForDineIn { get; set; }
    public bool AutoPrintKitchenTickets { get; set; }
    public int OrderNumberResetHour { get; set; }
    public string OrderNumberPrefix { get; set; } = "";
    public bool AllowCashPayments { get; set; }
    public bool AllowCardPayments { get; set; }
    public bool TipsEnabled { get; set; }
    public decimal[] TipSuggestions { get; set; } = [];
    public bool TrackInventory { get; set; }
    public bool WarnOnLowStock { get; set; }
    public bool AllowNegativeStock { get; set; }
}

// Operating Hours DTOs
public class OperatingHoursDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public string DayName => DayOfWeek.ToString();
    public TimeOnly OpenTime { get; set; }
    public TimeOnly CloseTime { get; set; }
    public bool IsClosed { get; set; }
}

// Request DTOs
public record CreateLocationRequest(
    string Name,
    string Code,
    string Timezone,
    string CurrencyCode,
    string? CurrencySymbol = null,
    string? Phone = null,
    string? Email = null,
    string? Website = null,
    string? AddressLine1 = null,
    string? AddressLine2 = null,
    string? City = null,
    string? State = null,
    string? PostalCode = null,
    string? Country = null,
    string? TaxNumber = null,
    string? BusinessName = null);

public record UpdateLocationRequest(
    string? Name = null,
    string? Code = null,
    string? Timezone = null,
    string? CurrencyCode = null,
    string? CurrencySymbol = null,
    string? Phone = null,
    string? Email = null,
    string? Website = null,
    string? AddressLine1 = null,
    string? AddressLine2 = null,
    string? City = null,
    string? State = null,
    string? PostalCode = null,
    string? Country = null,
    string? TaxNumber = null,
    string? BusinessName = null,
    bool? IsActive = null,
    bool? IsOpen = null);

public record UpdateLocationSettingsRequest(
    decimal? DefaultTaxRate = null,
    bool? TaxIncludedInPrices = null,
    string? ReceiptHeader = null,
    string? ReceiptFooter = null,
    bool? PrintReceiptByDefault = null,
    bool? ShowTaxBreakdown = null,
    bool? RequireTableForDineIn = null,
    bool? AutoPrintKitchenTickets = null,
    int? OrderNumberResetHour = null,
    string? OrderNumberPrefix = null,
    bool? AllowCashPayments = null,
    bool? AllowCardPayments = null,
    bool? TipsEnabled = null,
    decimal[]? TipSuggestions = null,
    bool? TrackInventory = null,
    bool? WarnOnLowStock = null,
    bool? AllowNegativeStock = null);

public record SetOperatingHoursRequest(
    DayOfWeek DayOfWeek,
    TimeOnly OpenTime,
    TimeOnly CloseTime,
    bool IsClosed = false);
