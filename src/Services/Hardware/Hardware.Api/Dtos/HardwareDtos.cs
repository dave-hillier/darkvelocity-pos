using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Hardware.Api.Dtos;

// Printer DTOs
public class PrinterDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public required string Name { get; set; }
    public required string PrinterType { get; set; }
    public required string ConnectionType { get; set; }
    public string? IpAddress { get; set; }
    public int? Port { get; set; }
    public string? MacAddress { get; set; }
    public string? UsbVendorId { get; set; }
    public string? UsbProductId { get; set; }
    public int PaperWidth { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public string? CharacterSet { get; set; }
    public bool SupportsCut { get; set; }
    public bool SupportsCashDrawer { get; set; }
}

public record CreatePrinterRequest(
    string Name,
    string PrinterType,
    string ConnectionType,
    string? IpAddress = null,
    int? Port = null,
    string? MacAddress = null,
    string? UsbVendorId = null,
    string? UsbProductId = null,
    int PaperWidth = 80,
    bool IsDefault = false,
    string CharacterSet = "CP437",
    bool SupportsCut = true,
    bool SupportsCashDrawer = true);

public record UpdatePrinterRequest(
    string? Name = null,
    string? IpAddress = null,
    int? Port = null,
    string? MacAddress = null,
    string? UsbVendorId = null,
    string? UsbProductId = null,
    int? PaperWidth = null,
    bool? IsDefault = null,
    bool? IsActive = null,
    string? CharacterSet = null,
    bool? SupportsCut = null,
    bool? SupportsCashDrawer = null);

// CashDrawer DTOs
public class CashDrawerDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public required string Name { get; set; }
    public Guid? PrinterId { get; set; }
    public string? PrinterName { get; set; }
    public string? ConnectionType { get; set; }
    public string? IpAddress { get; set; }
    public int? Port { get; set; }
    public bool IsActive { get; set; }
    public int KickPulsePin { get; set; }
    public int KickPulseOnTime { get; set; }
    public int KickPulseOffTime { get; set; }
}

public record CreateCashDrawerRequest(
    string Name,
    Guid? PrinterId = null,
    string? ConnectionType = null,
    string? IpAddress = null,
    int? Port = null,
    int KickPulsePin = 0,
    int KickPulseOnTime = 100,
    int KickPulseOffTime = 100);

public record UpdateCashDrawerRequest(
    string? Name = null,
    Guid? PrinterId = null,
    string? ConnectionType = null,
    string? IpAddress = null,
    int? Port = null,
    bool? IsActive = null,
    int? KickPulsePin = null,
    int? KickPulseOnTime = null,
    int? KickPulseOffTime = null);

// PosDevice DTOs
public class PosDeviceDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public required string Name { get; set; }
    public required string DeviceId { get; set; }
    public string? DeviceType { get; set; }
    public string? Model { get; set; }
    public string? OsVersion { get; set; }
    public string? AppVersion { get; set; }
    public Guid? DefaultPrinterId { get; set; }
    public string? DefaultPrinterName { get; set; }
    public Guid? DefaultCashDrawerId { get; set; }
    public string? DefaultCashDrawerName { get; set; }
    public bool AutoPrintReceipts { get; set; }
    public bool OpenDrawerOnCash { get; set; }
    public bool IsActive { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime? RegisteredAt { get; set; }
}

public record RegisterPosDeviceRequest(
    string Name,
    string DeviceId,
    string? DeviceType = null,
    string? Model = null,
    string? OsVersion = null,
    string? AppVersion = null);

public record UpdatePosDeviceRequest(
    string? Name = null,
    string? AppVersion = null,
    Guid? DefaultPrinterId = null,
    Guid? DefaultCashDrawerId = null,
    bool? AutoPrintReceipts = null,
    bool? OpenDrawerOnCash = null,
    bool? IsActive = null);

public record HeartbeatRequest(
    string? AppVersion = null,
    string? OsVersion = null);

public class HeartbeatResponse
{
    public bool Success { get; set; }
    public DateTime ServerTime { get; set; }
    public PosDeviceDto? Device { get; set; }
}
