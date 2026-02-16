namespace DarkVelocity.Host.Events;

// ============================================================================
// Room Type Events
// ============================================================================

public interface IRoomTypeEvent
{
    Guid RoomTypeId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record RoomTypeCreated : IRoomTypeEvent
{
    [Id(0)] public Guid RoomTypeId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public Guid SiteId { get; init; }
    [Id(3)] public string Name { get; init; } = "";
    [Id(4)] public string Code { get; init; } = "";
    [Id(5)] public int BaseOccupancy { get; init; }
    [Id(6)] public int MaxOccupancy { get; init; }
    [Id(7)] public int TotalRooms { get; init; }
    [Id(8)] public decimal RackRate { get; init; }
    [Id(9)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record RoomTypeUpdated : IRoomTypeEvent
{
    [Id(0)] public Guid RoomTypeId { get; init; }
    [Id(1)] public string? Name { get; init; }
    [Id(2)] public string? Description { get; init; }
    [Id(3)] public int? MaxOccupancy { get; init; }
    [Id(4)] public int? MaxAdults { get; init; }
    [Id(5)] public int? MaxChildren { get; init; }
    [Id(6)] public int? TotalRooms { get; init; }
    [Id(7)] public decimal? RackRate { get; init; }
    [Id(8)] public decimal? ExtraAdultRate { get; init; }
    [Id(9)] public decimal? ExtraChildRate { get; init; }
    [Id(10)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record RoomTypeDeactivated : IRoomTypeEvent
{
    [Id(0)] public Guid RoomTypeId { get; init; }
    [Id(1)] public DateTime OccurredAt { get; init; }
}

// ============================================================================
// Room Reservation Events
// ============================================================================

public interface IRoomReservationEvent
{
    Guid ReservationId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record RoomReservationCreated : IRoomReservationEvent
{
    [Id(0)] public Guid ReservationId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public Guid SiteId { get; init; }
    [Id(3)] public Guid RoomTypeId { get; init; }
    [Id(4)] public DateOnly CheckInDate { get; init; }
    [Id(5)] public DateOnly CheckOutDate { get; init; }
    [Id(6)] public int Adults { get; init; }
    [Id(7)] public int Children { get; init; }
    [Id(8)] public string GuestName { get; init; } = "";
    [Id(9)] public string? GuestEmail { get; init; }
    [Id(10)] public string? GuestPhone { get; init; }
    [Id(11)] public Guid? CustomerId { get; init; }
    [Id(12)] public string? Source { get; init; }
    [Id(13)] public string? SpecialRequests { get; init; }
    [Id(14)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record RoomReservationConfirmed : IRoomReservationEvent
{
    [Id(0)] public Guid ReservationId { get; init; }
    [Id(1)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record RoomReservationModified : IRoomReservationEvent
{
    [Id(0)] public Guid ReservationId { get; init; }
    [Id(1)] public DateOnly? NewCheckInDate { get; init; }
    [Id(2)] public DateOnly? NewCheckOutDate { get; init; }
    [Id(3)] public Guid? NewRoomTypeId { get; init; }
    [Id(4)] public int? NewAdults { get; init; }
    [Id(5)] public int? NewChildren { get; init; }
    [Id(6)] public string? NewSpecialRequests { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record RoomReservationCancelled : IRoomReservationEvent
{
    [Id(0)] public Guid ReservationId { get; init; }
    [Id(1)] public string Reason { get; init; } = "";
    [Id(2)] public Guid? CancelledBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record GuestCheckedIn : IRoomReservationEvent
{
    [Id(0)] public Guid ReservationId { get; init; }
    [Id(1)] public Guid? RoomId { get; init; }
    [Id(2)] public string? RoomNumber { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record RoomAssigned : IRoomReservationEvent
{
    [Id(0)] public Guid ReservationId { get; init; }
    [Id(1)] public Guid RoomId { get; init; }
    [Id(2)] public string RoomNumber { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record GuestCheckedOut : IRoomReservationEvent
{
    [Id(0)] public Guid ReservationId { get; init; }
    [Id(1)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record RoomReservationNoShow : IRoomReservationEvent
{
    [Id(0)] public Guid ReservationId { get; init; }
    [Id(1)] public Guid? MarkedBy { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record RoomReservationDepositRequired : IRoomReservationEvent
{
    [Id(0)] public Guid ReservationId { get; init; }
    [Id(1)] public decimal AmountRequired { get; init; }
    [Id(2)] public DateTime DueBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record RoomReservationDepositPaid : IRoomReservationEvent
{
    [Id(0)] public Guid ReservationId { get; init; }
    [Id(1)] public decimal Amount { get; init; }
    [Id(2)] public string PaymentMethod { get; init; } = "";
    [Id(3)] public string? PaymentReference { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

// ============================================================================
// Room Reservation Settings Events
// ============================================================================

public interface IRoomReservationSettingsEvent
{
    Guid SiteId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record RoomReservationSettingsInitialized : IRoomReservationSettingsEvent
{
    [Id(0)] public Guid SiteId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record RoomReservationSettingsUpdated : IRoomReservationSettingsEvent
{
    [Id(0)] public Guid SiteId { get; init; }
    [Id(1)] public TimeOnly? DefaultCheckInTime { get; init; }
    [Id(2)] public TimeOnly? DefaultCheckOutTime { get; init; }
    [Id(3)] public int? AdvanceBookingDays { get; init; }
    [Id(4)] public int? MinStayNights { get; init; }
    [Id(5)] public int? MaxStayNights { get; init; }
    [Id(6)] public int? OverbookingPercent { get; init; }
    [Id(7)] public bool? RequireDeposit { get; init; }
    [Id(8)] public decimal? DepositAmount { get; init; }
    [Id(9)] public TimeSpan? FreeCancellationWindow { get; init; }
    [Id(10)] public DateTime OccurredAt { get; init; }
}
