using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Contracts;

// ============================================================================
// Room Type Request DTOs
// ============================================================================

public record CreateRoomTypeRequest(
    string Name,
    string Code,
    int BaseOccupancy,
    int MaxOccupancy,
    int TotalRooms,
    decimal RackRate,
    string? Description = null,
    int MaxAdults = 2,
    int MaxChildren = 0,
    decimal? ExtraAdultRate = null,
    decimal? ExtraChildRate = null,
    List<string>? Amenities = null,
    List<string>? BedConfigurations = null);

public record UpdateRoomTypeRequest(
    string? Name = null,
    string? Description = null,
    int? MaxOccupancy = null,
    int? MaxAdults = null,
    int? MaxChildren = null,
    int? TotalRooms = null,
    decimal? RackRate = null,
    decimal? ExtraAdultRate = null,
    decimal? ExtraChildRate = null,
    List<string>? Amenities = null,
    List<string>? BedConfigurations = null);

// ============================================================================
// Room Request DTOs
// ============================================================================

public record CreateRoomRequest(
    Guid RoomTypeId,
    string Number,
    int Floor,
    string? Name = null,
    List<string>? Features = null,
    bool IsConnecting = false,
    Guid? ConnectingRoomId = null);

public record UpdateRoomRequest(
    string? Number = null,
    string? Name = null,
    int? Floor = null,
    Guid? RoomTypeId = null,
    List<string>? Features = null);

public record SetHousekeepingStatusRequest(HousekeepingStatus Status);

// ============================================================================
// Reservation Request DTOs
// ============================================================================

public record RequestRoomReservationRequest(
    Guid RoomTypeId,
    DateOnly CheckInDate,
    DateOnly CheckOutDate,
    int Adults,
    GuestInfo Guest,
    int Children = 0,
    RatePlanType RatePlan = RatePlanType.BestAvailable,
    string? SpecialRequests = null,
    ReservationSource Source = ReservationSource.Direct,
    string? ExternalRef = null,
    Guid? CustomerId = null);

public record ModifyRoomReservationRequest(
    DateOnly? NewCheckInDate = null,
    DateOnly? NewCheckOutDate = null,
    Guid? NewRoomTypeId = null,
    int? NewAdults = null,
    int? NewChildren = null,
    string? SpecialRequests = null);

public record CancelRoomReservationRequest(string Reason, Guid CancelledBy);
public record RoomCheckInRequest(Guid? RoomId = null, string? RoomNumber = null);
public record AssignRoomRequest(Guid RoomId, string RoomNumber);
public record RoomNoShowRequest(Guid? MarkedBy = null);

// ============================================================================
// Reservation Settings Request DTOs
// ============================================================================

public record UpdateRoomReservationSettingsRequest(
    TimeOnly? DefaultCheckInTime = null,
    TimeOnly? DefaultCheckOutTime = null,
    int? AdvanceBookingDays = null,
    int? MinStayNights = null,
    int? MaxStayNights = null,
    int? OverbookingPercent = null,
    bool? RequireDeposit = null,
    decimal? DepositAmount = null,
    TimeSpan? FreeCancellationWindow = null,
    bool? AllowChildren = null,
    int? ChildMaxAge = null);
