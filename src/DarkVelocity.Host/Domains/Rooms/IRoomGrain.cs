using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record CreateRoomCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid RoomTypeId,
    [property: Id(3)] string Number,
    [property: Id(4)] int Floor,
    [property: Id(5)] string? Name = null,
    [property: Id(6)] List<string>? Features = null,
    [property: Id(7)] bool IsConnecting = false,
    [property: Id(8)] Guid? ConnectingRoomId = null);

[GenerateSerializer]
public record UpdateRoomCommand(
    [property: Id(0)] string? Number = null,
    [property: Id(1)] string? Name = null,
    [property: Id(2)] int? Floor = null,
    [property: Id(3)] Guid? RoomTypeId = null,
    [property: Id(4)] List<string>? Features = null);

[GenerateSerializer]
public record OccupyRoomCommand(
    [property: Id(0)] Guid ReservationId,
    [property: Id(1)] string GuestName,
    [property: Id(2)] int GuestCount,
    [property: Id(3)] DateOnly ExpectedCheckOut);

public interface IRoomGrain : IGrainWithStringKey
{
    Task CreateAsync(CreateRoomCommand command);
    Task UpdateAsync(UpdateRoomCommand command);
    Task<RoomState> GetStateAsync();
    Task<bool> ExistsAsync();

    Task SetStatusAsync(RoomStatus status);
    Task OccupyAsync(OccupyRoomCommand command);
    Task VacateAsync();
    Task SetHousekeepingStatusAsync(HousekeepingStatus status);
    Task SetOutOfOrderAsync(string? reason = null);
    Task ReturnToServiceAsync();
}
