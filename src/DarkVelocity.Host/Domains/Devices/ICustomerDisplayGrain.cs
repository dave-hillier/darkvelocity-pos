namespace DarkVelocity.Host.Grains;

// ============================================================================
// Customer Display Grain
// ============================================================================

public enum CustomerDisplayMode
{
    Idle,
    Order,
    Tip,
    Payment,
    Processing,
    Receipt,
    ThankYou
}

[GenerateSerializer]
public record RegisterCustomerDisplayCommand(
    [property: Id(0)] Guid LocationId,
    [property: Id(1)] string Name,
    [property: Id(2)] string DeviceId,
    [property: Id(3)] Guid? PairedPosDeviceId);

[GenerateSerializer]
public record UpdateCustomerDisplayCommand(
    [property: Id(0)] string? Name,
    [property: Id(1)] Guid? PairedPosDeviceId,
    [property: Id(2)] bool? IsActive,
    [property: Id(3)] string? IdleMessage,
    [property: Id(4)] string? LogoUrl,
    [property: Id(5)] IReadOnlyList<int>? TipPresets,
    [property: Id(6)] bool? TipEnabled,
    [property: Id(7)] bool? ReceiptPromptEnabled);

[GenerateSerializer]
public record CustomerDisplaySnapshot(
    [property: Id(0)] Guid DisplayId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] string Name,
    [property: Id(3)] string DeviceId,
    [property: Id(4)] Guid? PairedPosDeviceId,
    [property: Id(5)] bool IsActive,
    [property: Id(6)] bool IsOnline,
    [property: Id(7)] DateTime? LastSeenAt,
    [property: Id(8)] DateTime? RegisteredAt,
    [property: Id(9)] string IdleMessage,
    [property: Id(10)] string? LogoUrl,
    [property: Id(11)] IReadOnlyList<int> TipPresets,
    [property: Id(12)] bool TipEnabled,
    [property: Id(13)] bool ReceiptPromptEnabled,
    [property: Id(14)] CustomerDisplayMode CurrentMode);

/// <summary>
/// Grain for customer-facing display management.
/// Tracks configuration, pairing, and connection status for displays
/// mounted at POS terminals showing order totals, tip prompts, and receipts.
/// Key: "{orgId}:customerdisplay:{displayId}"
/// </summary>
public interface ICustomerDisplayGrain : IGrainWithStringKey
{
    Task<CustomerDisplaySnapshot> RegisterAsync(RegisterCustomerDisplayCommand command);
    Task<CustomerDisplaySnapshot> UpdateAsync(UpdateCustomerDisplayCommand command);
    Task DeactivateAsync();
    Task<CustomerDisplaySnapshot> GetSnapshotAsync();
    Task RecordHeartbeatAsync();
    Task SetOfflineAsync();
    Task<bool> IsOnlineAsync();
    Task PairAsync(Guid posDeviceId);
    Task UnpairAsync();
    Task SetModeAsync(CustomerDisplayMode mode);
}
