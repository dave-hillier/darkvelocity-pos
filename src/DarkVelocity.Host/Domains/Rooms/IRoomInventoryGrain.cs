using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record RoomAvailabilityResult(
    [property: Id(0)] DateOnly Date,
    [property: Id(1)] Guid RoomTypeId,
    [property: Id(2)] int TotalRooms,
    [property: Id(3)] int Sold,
    [property: Id(4)] int Blocked,
    [property: Id(5)] int OutOfOrder,
    [property: Id(6)] int OverbookingAllowance,
    [property: Id(7)] int Available);

public interface IRoomInventoryGrain : IGrainWithStringKey
{
    Task InitializeAsync(Guid organizationId, Guid siteId, Guid roomTypeId, DateOnly date, int totalRooms);
    Task<RoomInventoryState> GetStateAsync();
    Task<bool> ExistsAsync();

    Task<RoomAvailabilityResult> GetAvailabilityAsync();
    Task SellRoomAsync(Guid reservationId);
    Task ReleaseSoldRoomAsync(Guid reservationId);
    Task BlockRoomsAsync(Guid holdId, string reason, int count, DateOnly? releaseDate = null);
    Task ReleaseBlockAsync(Guid holdId);
    Task SetOutOfOrderCountAsync(int count);
    Task SetOverbookingAllowanceAsync(int allowance);
}
