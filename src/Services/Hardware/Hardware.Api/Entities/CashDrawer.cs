using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Hardware.Api.Entities;

public class CashDrawer : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }
    public required string Name { get; set; }
    public Guid? PrinterId { get; set; } // Drawer connected via printer
    public string? ConnectionType { get; set; } // printer, usb, network
    public string? IpAddress { get; set; }
    public int? Port { get; set; }
    public bool IsActive { get; set; } = true;

    // Drawer kick settings (ESC/POS)
    public int KickPulsePin { get; set; } = 0; // Pin 2 = 0, Pin 5 = 1
    public int KickPulseOnTime { get; set; } = 100; // ms
    public int KickPulseOffTime { get; set; } = 100; // ms

    // Navigation
    public Printer? Printer { get; set; }
}
