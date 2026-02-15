using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record UpdateRoomReservationSettingsCommand(
    [property: Id(0)] TimeOnly? DefaultCheckInTime = null,
    [property: Id(1)] TimeOnly? DefaultCheckOutTime = null,
    [property: Id(2)] int? AdvanceBookingDays = null,
    [property: Id(3)] int? MinStayNights = null,
    [property: Id(4)] int? MaxStayNights = null,
    [property: Id(5)] int? OverbookingPercent = null,
    [property: Id(6)] bool? RequireDeposit = null,
    [property: Id(7)] decimal? DepositAmount = null,
    [property: Id(8)] TimeSpan? FreeCancellationWindow = null,
    [property: Id(9)] bool? AllowChildren = null,
    [property: Id(10)] int? ChildMaxAge = null);

[GenerateSerializer]
public record GetRoomAvailabilityQuery(
    [property: Id(0)] Guid RoomTypeId,
    [property: Id(1)] DateOnly CheckInDate,
    [property: Id(2)] DateOnly CheckOutDate,
    [property: Id(3)] int Adults = 1,
    [property: Id(4)] int Children = 0);

[GenerateSerializer]
public record RoomAvailabilitySlot(
    [property: Id(0)] DateOnly Date,
    [property: Id(1)] bool IsAvailable,
    [property: Id(2)] int AvailableRooms,
    [property: Id(3)] decimal? Rate);

public interface IRoomReservationSettingsGrain : IGrainWithStringKey
{
    Task InitializeAsync(Guid organizationId, Guid siteId);
    Task UpdateAsync(UpdateRoomReservationSettingsCommand command);
    Task<RoomReservationSettingsState> GetStateAsync();
    Task<bool> ExistsAsync();

    // Date restrictions
    Task CloseToArrivalAsync(DateOnly date);
    Task OpenToArrivalAsync(DateOnly date);
    Task CloseToDepartureAsync(DateOnly date);
    Task OpenToDepartureAsync(DateOnly date);
}
