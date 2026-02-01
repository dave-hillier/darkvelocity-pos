namespace DarkVelocity.Host.Grains;

// ============================================================================
// Delivery Platform Grain
// ============================================================================

public enum DeliveryPlatformType
{
    UberEats,
    DoorDash,
    Deliveroo,
    JustEat,
    Wolt,
    GrubHub,
    Postmates,
    Custom
}

public enum DeliveryPlatformStatus
{
    Active,
    Paused,
    Disconnected,
    Error
}

[GenerateSerializer]
public record ConnectDeliveryPlatformCommand(
    [property: Id(0)] DeliveryPlatformType PlatformType,
    [property: Id(1)] string Name,
    [property: Id(2)] string? ApiCredentialsEncrypted,
    [property: Id(3)] string? WebhookSecret,
    [property: Id(4)] string? MerchantId,
    [property: Id(5)] string? Settings);

[GenerateSerializer]
public record UpdateDeliveryPlatformCommand(
    [property: Id(0)] string? Name,
    [property: Id(1)] DeliveryPlatformStatus? Status,
    [property: Id(2)] string? ApiCredentialsEncrypted,
    [property: Id(3)] string? WebhookSecret,
    [property: Id(4)] string? Settings);

[GenerateSerializer]
public record PlatformLocationMapping(
    [property: Id(0)] Guid LocationId,
    [property: Id(1)] string PlatformStoreId,
    [property: Id(2)] bool IsActive,
    [property: Id(3)] string? OperatingHoursOverride);

[GenerateSerializer]
public record DeliveryPlatformSnapshot(
    [property: Id(0)] Guid DeliveryPlatformId,
    [property: Id(1)] DeliveryPlatformType PlatformType,
    [property: Id(2)] string Name,
    [property: Id(3)] DeliveryPlatformStatus Status,
    [property: Id(4)] string? MerchantId,
    [property: Id(5)] DateTime? ConnectedAt,
    [property: Id(6)] DateTime? LastSyncAt,
    [property: Id(7)] DateTime? LastOrderAt,
    [property: Id(8)] IReadOnlyList<PlatformLocationMapping> Locations,
    [property: Id(9)] int TotalOrdersToday,
    [property: Id(10)] decimal TotalRevenueToday);

/// <summary>
/// Grain for delivery platform management.
/// Key: "{orgId}:deliveryplatform:{platformId}"
/// </summary>
public interface IDeliveryPlatformGrain : IGrainWithStringKey
{
    Task<DeliveryPlatformSnapshot> ConnectAsync(ConnectDeliveryPlatformCommand command);
    Task<DeliveryPlatformSnapshot> UpdateAsync(UpdateDeliveryPlatformCommand command);
    Task DisconnectAsync();
    Task PauseAsync();
    Task ResumeAsync();
    Task AddLocationMappingAsync(PlatformLocationMapping mapping);
    Task RemoveLocationMappingAsync(Guid locationId);
    Task<DeliveryPlatformSnapshot> GetSnapshotAsync();
    Task RecordOrderAsync(decimal orderTotal);
    Task RecordSyncAsync();
}

// ============================================================================
// External Order Grain
// ============================================================================

public enum ExternalOrderStatus
{
    Pending,
    Accepted,
    Rejected,
    Preparing,
    Ready,
    PickedUp,
    Delivered,
    Cancelled,
    Failed
}

public enum ExternalOrderType
{
    Delivery,
    Pickup
}

[GenerateSerializer]
public record ExternalOrderCustomer(
    [property: Id(0)] string Name,
    [property: Id(1)] string? Phone,
    [property: Id(2)] string? DeliveryAddress);

[GenerateSerializer]
public record ExternalOrderItem(
    [property: Id(0)] string PlatformItemId,
    [property: Id(1)] Guid? InternalMenuItemId,
    [property: Id(2)] string Name,
    [property: Id(3)] int Quantity,
    [property: Id(4)] decimal UnitPrice,
    [property: Id(5)] decimal TotalPrice,
    [property: Id(6)] string? SpecialInstructions,
    [property: Id(7)] IReadOnlyList<ExternalOrderModifier>? Modifiers);

[GenerateSerializer]
public record ExternalOrderModifier(
    [property: Id(0)] string Name,
    [property: Id(1)] decimal Price);

[GenerateSerializer]
public record CreateExternalOrderCommand(
    [property: Id(0)] Guid LocationId,
    [property: Id(1)] Guid DeliveryPlatformId,
    [property: Id(2)] string PlatformOrderId,
    [property: Id(3)] string PlatformOrderNumber,
    [property: Id(4)] ExternalOrderType OrderType,
    [property: Id(5)] DateTime PlacedAt,
    [property: Id(6)] ExternalOrderCustomer Customer,
    [property: Id(7)] IReadOnlyList<ExternalOrderItem> Items,
    [property: Id(8)] decimal Subtotal,
    [property: Id(9)] decimal DeliveryFee,
    [property: Id(10)] decimal ServiceFee,
    [property: Id(11)] decimal Tax,
    [property: Id(12)] decimal Tip,
    [property: Id(13)] decimal Total,
    [property: Id(14)] string Currency,
    [property: Id(15)] string? SpecialInstructions,
    [property: Id(16)] string? PlatformRawPayload);

[GenerateSerializer]
public record ExternalOrderSnapshot(
    [property: Id(0)] Guid ExternalOrderId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] Guid DeliveryPlatformId,
    [property: Id(3)] string PlatformOrderId,
    [property: Id(4)] string PlatformOrderNumber,
    [property: Id(5)] Guid? InternalOrderId,
    [property: Id(6)] ExternalOrderStatus Status,
    [property: Id(7)] ExternalOrderType OrderType,
    [property: Id(8)] DateTime PlacedAt,
    [property: Id(9)] DateTime? AcceptedAt,
    [property: Id(10)] DateTime? EstimatedPickupAt,
    [property: Id(11)] DateTime? ActualPickupAt,
    [property: Id(12)] ExternalOrderCustomer Customer,
    [property: Id(13)] IReadOnlyList<ExternalOrderItem> Items,
    [property: Id(14)] decimal Subtotal,
    [property: Id(15)] decimal DeliveryFee,
    [property: Id(16)] decimal ServiceFee,
    [property: Id(17)] decimal Tax,
    [property: Id(18)] decimal Tip,
    [property: Id(19)] decimal Total,
    [property: Id(20)] string Currency,
    [property: Id(21)] string? SpecialInstructions,
    [property: Id(22)] string? ErrorMessage,
    [property: Id(23)] int RetryCount);

/// <summary>
/// Grain for external order management.
/// Key: "{orgId}:externalorder:{externalOrderId}"
/// </summary>
public interface IExternalOrderGrain : IGrainWithStringKey
{
    Task<ExternalOrderSnapshot> CreateAsync(CreateExternalOrderCommand command);
    Task<ExternalOrderSnapshot> AcceptAsync(DateTime? estimatedPickupAt);
    Task<ExternalOrderSnapshot> RejectAsync(string reason);
    Task SetPreparingAsync();
    Task SetReadyAsync();
    Task SetPickedUpAsync();
    Task SetDeliveredAsync();
    Task CancelAsync(string reason);
    Task LinkInternalOrderAsync(Guid internalOrderId);
    Task<ExternalOrderSnapshot> GetSnapshotAsync();
    Task MarkFailedAsync(string errorMessage);
    Task IncrementRetryAsync();
}

// ============================================================================
// Menu Sync Grain
// ============================================================================

public enum MenuSyncStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

[GenerateSerializer]
public record StartMenuSyncCommand(
    [property: Id(0)] Guid DeliveryPlatformId,
    [property: Id(1)] Guid? LocationId);

[GenerateSerializer]
public record MenuItemMappingRecord(
    [property: Id(0)] Guid InternalMenuItemId,
    [property: Id(1)] string PlatformItemId,
    [property: Id(2)] string? PlatformCategoryId,
    [property: Id(3)] decimal? PriceOverride,
    [property: Id(4)] bool IsAvailable);

[GenerateSerializer]
public record MenuSyncSnapshot(
    [property: Id(0)] Guid MenuSyncId,
    [property: Id(1)] Guid DeliveryPlatformId,
    [property: Id(2)] Guid? LocationId,
    [property: Id(3)] MenuSyncStatus Status,
    [property: Id(4)] DateTime StartedAt,
    [property: Id(5)] DateTime? CompletedAt,
    [property: Id(6)] int ItemsSynced,
    [property: Id(7)] int ItemsFailed,
    [property: Id(8)] IReadOnlyList<string> Errors);

/// <summary>
/// Grain for menu sync management.
/// Key: "{orgId}:menusync:{syncId}"
/// </summary>
public interface IMenuSyncGrain : IGrainWithStringKey
{
    Task<MenuSyncSnapshot> StartAsync(StartMenuSyncCommand command);
    Task RecordItemSyncedAsync(MenuItemMappingRecord mapping);
    Task RecordItemFailedAsync(Guid menuItemId, string error);
    Task CompleteAsync();
    Task FailAsync(string error);
    Task<MenuSyncSnapshot> GetSnapshotAsync();
}

// ============================================================================
// Platform Payout Grain
// ============================================================================

public enum PayoutStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

[GenerateSerializer]
public record CreatePayoutCommand(
    [property: Id(0)] Guid DeliveryPlatformId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] DateTime PeriodStart,
    [property: Id(3)] DateTime PeriodEnd,
    [property: Id(4)] decimal GrossAmount,
    [property: Id(5)] decimal PlatformFees,
    [property: Id(6)] decimal NetAmount,
    [property: Id(7)] string Currency,
    [property: Id(8)] string? PayoutReference);

[GenerateSerializer]
public record PayoutSnapshot(
    [property: Id(0)] Guid PayoutId,
    [property: Id(1)] Guid DeliveryPlatformId,
    [property: Id(2)] Guid LocationId,
    [property: Id(3)] DateTime PeriodStart,
    [property: Id(4)] DateTime PeriodEnd,
    [property: Id(5)] decimal GrossAmount,
    [property: Id(6)] decimal PlatformFees,
    [property: Id(7)] decimal NetAmount,
    [property: Id(8)] string Currency,
    [property: Id(9)] PayoutStatus Status,
    [property: Id(10)] string? PayoutReference,
    [property: Id(11)] DateTime? ProcessedAt);

/// <summary>
/// Grain for platform payout management.
/// Key: "{orgId}:payout:{payoutId}"
/// </summary>
public interface IPlatformPayoutGrain : IGrainWithStringKey
{
    Task<PayoutSnapshot> CreateAsync(CreatePayoutCommand command);
    Task SetProcessingAsync();
    Task CompleteAsync(DateTime processedAt);
    Task FailAsync(string reason);
    Task<PayoutSnapshot> GetSnapshotAsync();
}
