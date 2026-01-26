using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Hardware.Api.Entities;

public class PosDevice : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }
    public required string Name { get; set; }
    public required string DeviceId { get; set; } // Unique device identifier
    public string? DeviceType { get; set; } // tablet, terminal, mobile
    public string? Model { get; set; }
    public string? OsVersion { get; set; }
    public string? AppVersion { get; set; }

    // Configuration
    public Guid? DefaultPrinterId { get; set; }
    public Guid? DefaultCashDrawerId { get; set; }
    public bool AutoPrintReceipts { get; set; } = true;
    public bool OpenDrawerOnCash { get; set; } = true;

    // Status
    public bool IsActive { get; set; } = true;
    public bool IsOnline { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime? RegisteredAt { get; set; }

    // Navigation
    public Printer? DefaultPrinter { get; set; }
    public CashDrawer? DefaultCashDrawer { get; set; }
}
