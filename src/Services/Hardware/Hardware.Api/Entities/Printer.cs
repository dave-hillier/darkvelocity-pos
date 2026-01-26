using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Hardware.Api.Entities;

public class Printer : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }
    public required string Name { get; set; }
    public required string PrinterType { get; set; } // receipt, kitchen, label
    public required string ConnectionType { get; set; } // usb, network, bluetooth
    public string? IpAddress { get; set; }
    public int? Port { get; set; }
    public string? MacAddress { get; set; }
    public string? UsbVendorId { get; set; }
    public string? UsbProductId { get; set; }
    public int PaperWidth { get; set; } = 80; // mm (58, 80)
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    // ESC/POS settings
    public string CharacterSet { get; set; } = "CP437";
    public bool SupportsCut { get; set; } = true;
    public bool SupportsCashDrawer { get; set; } = true;

    // Navigation
    public ICollection<CashDrawer> CashDrawers { get; set; } = new List<CashDrawer>();
}
