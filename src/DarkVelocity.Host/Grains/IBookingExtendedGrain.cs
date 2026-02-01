namespace DarkVelocity.Host.Grains;

// ============================================================================
// Table Grain
// ============================================================================

public enum TableStatus
{
    Available,
    Occupied,
    Reserved,
    Closed
}

[GenerateSerializer]
public record CreateTableCommand(
    [property: Id(0)] Guid FloorPlanId,
    [property: Id(1)] string TableNumber,
    [property: Id(2)] string? Name,
    [property: Id(3)] int MinCapacity,
    [property: Id(4)] int MaxCapacity,
    [property: Id(5)] string? Shape,
    [property: Id(6)] int PositionX,
    [property: Id(7)] int PositionY,
    [property: Id(8)] int Width,
    [property: Id(9)] int Height,
    [property: Id(10)] int Rotation,
    [property: Id(11)] bool IsCombinationAllowed,
    [property: Id(12)] int AssignmentPriority,
    [property: Id(13)] string? Notes);

[GenerateSerializer]
public record UpdateTableCommand(
    [property: Id(0)] string? TableNumber,
    [property: Id(1)] string? Name,
    [property: Id(2)] int? MinCapacity,
    [property: Id(3)] int? MaxCapacity,
    [property: Id(4)] string? Shape,
    [property: Id(5)] int? PositionX,
    [property: Id(6)] int? PositionY,
    [property: Id(7)] int? Width,
    [property: Id(8)] int? Height,
    [property: Id(9)] int? Rotation,
    [property: Id(10)] TableStatus? Status,
    [property: Id(11)] bool? IsCombinationAllowed,
    [property: Id(12)] bool? IsActive,
    [property: Id(13)] int? AssignmentPriority,
    [property: Id(14)] string? Notes);

[GenerateSerializer]
public record TableSnapshot(
    [property: Id(0)] Guid TableId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] Guid FloorPlanId,
    [property: Id(3)] string? FloorPlanName,
    [property: Id(4)] string TableNumber,
    [property: Id(5)] string? Name,
    [property: Id(6)] int MinCapacity,
    [property: Id(7)] int MaxCapacity,
    [property: Id(8)] string? Shape,
    [property: Id(9)] int PositionX,
    [property: Id(10)] int PositionY,
    [property: Id(11)] int Width,
    [property: Id(12)] int Height,
    [property: Id(13)] int Rotation,
    [property: Id(14)] TableStatus Status,
    [property: Id(15)] bool IsCombinationAllowed,
    [property: Id(16)] bool IsActive,
    [property: Id(17)] int AssignmentPriority,
    [property: Id(18)] string? Notes,
    [property: Id(19)] DateTime CreatedAt);

/// <summary>
/// Grain for table management.
/// Key: "{orgId}:{siteId}:table:{tableId}"
/// </summary>
public interface ITableGrain : IGrainWithStringKey
{
    Task<TableSnapshot> CreateAsync(CreateTableCommand command);
    Task<TableSnapshot> UpdateAsync(UpdateTableCommand command);
    Task<TableSnapshot> GetSnapshotAsync();
    Task<bool> ExistsAsync();
    Task DeleteAsync();

    // Status management
    Task<TableSnapshot> SetStatusAsync(TableStatus status);
    Task<TableStatus> GetStatusAsync();
    Task<bool> IsAvailableAsync();

    // Position update
    Task UpdatePositionAsync(int positionX, int positionY, int? rotation);
}

// ============================================================================
// Floor Plan Grain
// ============================================================================

[GenerateSerializer]
public record CreateFloorPlanCommand(
    [property: Id(0)] string Name,
    [property: Id(1)] string? Description,
    [property: Id(2)] int GridWidth,
    [property: Id(3)] int GridHeight,
    [property: Id(4)] string? BackgroundImageUrl,
    [property: Id(5)] int SortOrder,
    [property: Id(6)] int DefaultTurnTimeMinutes);

[GenerateSerializer]
public record UpdateFloorPlanCommand(
    [property: Id(0)] string? Name,
    [property: Id(1)] string? Description,
    [property: Id(2)] int? GridWidth,
    [property: Id(3)] int? GridHeight,
    [property: Id(4)] string? BackgroundImageUrl,
    [property: Id(5)] int? SortOrder,
    [property: Id(6)] bool? IsActive,
    [property: Id(7)] int? DefaultTurnTimeMinutes);

[GenerateSerializer]
public record FloorPlanSnapshot(
    [property: Id(0)] Guid FloorPlanId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] string Name,
    [property: Id(3)] string? Description,
    [property: Id(4)] int GridWidth,
    [property: Id(5)] int GridHeight,
    [property: Id(6)] string? BackgroundImageUrl,
    [property: Id(7)] int SortOrder,
    [property: Id(8)] bool IsActive,
    [property: Id(9)] int DefaultTurnTimeMinutes,
    [property: Id(10)] int TableCount,
    [property: Id(11)] DateTime CreatedAt);

/// <summary>
/// Grain for floor plan management.
/// Key: "{orgId}:{siteId}:floorplan:{floorPlanId}"
/// </summary>
public interface IFloorPlanGrain : IGrainWithStringKey
{
    Task<FloorPlanSnapshot> CreateAsync(CreateFloorPlanCommand command);
    Task<FloorPlanSnapshot> UpdateAsync(UpdateFloorPlanCommand command);
    Task<FloorPlanSnapshot> GetSnapshotAsync();
    Task<bool> ExistsAsync();
    Task DeleteAsync();

    // Table tracking
    Task IncrementTableCountAsync();
    Task DecrementTableCountAsync();
    Task<int> GetTableCountAsync();
}

// ============================================================================
// Booking Settings Grain
// ============================================================================

[GenerateSerializer]
public record UpdateBookingSettingsCommand(
    [property: Id(0)] int? DefaultBookingDurationMinutes,
    [property: Id(1)] int? MinAdvanceBookingMinutes,
    [property: Id(2)] int? MaxAdvanceBookingDays,
    [property: Id(3)] bool? AllowOnlineBookings,
    [property: Id(4)] bool? RequireDeposit,
    [property: Id(5)] decimal? DepositAmount,
    [property: Id(6)] decimal? DepositPercentage,
    [property: Id(7)] int? CancellationDeadlineMinutes,
    [property: Id(8)] decimal? CancellationFeeAmount,
    [property: Id(9)] decimal? CancellationFeePercentage,
    [property: Id(10)] bool? AllowWaitlist,
    [property: Id(11)] int? MaxWaitlistSize,
    [property: Id(12)] TimeSpan? FirstServiceStart,
    [property: Id(13)] TimeSpan? FirstServiceEnd,
    [property: Id(14)] TimeSpan? SecondServiceStart,
    [property: Id(15)] TimeSpan? SecondServiceEnd,
    [property: Id(16)] int? TurnTimeMinutes,
    [property: Id(17)] int? BufferTimeMinutes,
    [property: Id(18)] string? ConfirmationMessageTemplate,
    [property: Id(19)] string? ReminderMessageTemplate);

[GenerateSerializer]
public record BookingSettingsSnapshot(
    [property: Id(0)] Guid LocationId,
    [property: Id(1)] int DefaultBookingDurationMinutes,
    [property: Id(2)] int MinAdvanceBookingMinutes,
    [property: Id(3)] int MaxAdvanceBookingDays,
    [property: Id(4)] bool AllowOnlineBookings,
    [property: Id(5)] bool RequireDeposit,
    [property: Id(6)] decimal DepositAmount,
    [property: Id(7)] decimal DepositPercentage,
    [property: Id(8)] int CancellationDeadlineMinutes,
    [property: Id(9)] decimal CancellationFeeAmount,
    [property: Id(10)] decimal CancellationFeePercentage,
    [property: Id(11)] bool AllowWaitlist,
    [property: Id(12)] int MaxWaitlistSize,
    [property: Id(13)] TimeSpan? FirstServiceStart,
    [property: Id(14)] TimeSpan? FirstServiceEnd,
    [property: Id(15)] TimeSpan? SecondServiceStart,
    [property: Id(16)] TimeSpan? SecondServiceEnd,
    [property: Id(17)] int TurnTimeMinutes,
    [property: Id(18)] int BufferTimeMinutes,
    [property: Id(19)] string? ConfirmationMessageTemplate,
    [property: Id(20)] string? ReminderMessageTemplate);

/// <summary>
/// Grain for booking settings management.
/// Key: "{orgId}:{siteId}:bookingsettings"
/// </summary>
public interface IBookingSettingsGrain : IGrainWithStringKey
{
    Task InitializeAsync(Guid locationId);
    Task<BookingSettingsSnapshot> GetSettingsAsync();
    Task<BookingSettingsSnapshot> UpdateAsync(UpdateBookingSettingsCommand command);
    Task<bool> ExistsAsync();

    // Helper methods
    Task<bool> CanAcceptOnlineBookingAsync();
    Task<decimal> CalculateDepositAsync(decimal totalAmount);
    Task<decimal> CalculateCancellationFeeAsync(decimal totalAmount, DateTime bookingTime);
    Task<bool> IsWithinBookingWindowAsync(DateTime requestedTime);
}
