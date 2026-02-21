using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

// ============================================================================
// Customer Display State
// ============================================================================

[GenerateSerializer]
public sealed class CustomerDisplayState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid DisplayId { get; set; }
    [Id(2)] public Guid LocationId { get; set; }
    [Id(3)] public string Name { get; set; } = string.Empty;
    [Id(4)] public string DeviceId { get; set; } = string.Empty;
    [Id(5)] public Guid? PairedPosDeviceId { get; set; }
    [Id(6)] public bool IsActive { get; set; } = true;
    [Id(7)] public bool IsOnline { get; set; }
    [Id(8)] public DateTime? LastSeenAt { get; set; }
    [Id(9)] public DateTime? RegisteredAt { get; set; }
    [Id(10)] public string IdleMessage { get; set; } = "Welcome";
    [Id(11)] public string? LogoUrl { get; set; }
    [Id(12)] public List<int> TipPresets { get; set; } = [10, 15, 20];
    [Id(13)] public bool TipEnabled { get; set; } = true;
    [Id(14)] public bool ReceiptPromptEnabled { get; set; } = true;
    [Id(15)] public CustomerDisplayMode CurrentMode { get; set; } = CustomerDisplayMode.Idle;
    [Id(16)] public int Version { get; set; }
}
