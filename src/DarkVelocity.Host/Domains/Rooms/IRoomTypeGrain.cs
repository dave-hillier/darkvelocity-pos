using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record CreateRoomTypeCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string Name,
    [property: Id(3)] string Code,
    [property: Id(4)] int BaseOccupancy,
    [property: Id(5)] int MaxOccupancy,
    [property: Id(6)] int TotalRooms,
    [property: Id(7)] decimal RackRate,
    [property: Id(8)] string? Description = null,
    [property: Id(9)] int MaxAdults = 2,
    [property: Id(10)] int MaxChildren = 0,
    [property: Id(11)] decimal? ExtraAdultRate = null,
    [property: Id(12)] decimal? ExtraChildRate = null,
    [property: Id(13)] List<string>? Amenities = null,
    [property: Id(14)] List<string>? BedConfigurations = null);

[GenerateSerializer]
public record UpdateRoomTypeCommand(
    [property: Id(0)] string? Name = null,
    [property: Id(1)] string? Description = null,
    [property: Id(2)] int? MaxOccupancy = null,
    [property: Id(3)] int? MaxAdults = null,
    [property: Id(4)] int? MaxChildren = null,
    [property: Id(5)] int? TotalRooms = null,
    [property: Id(6)] decimal? RackRate = null,
    [property: Id(7)] decimal? ExtraAdultRate = null,
    [property: Id(8)] decimal? ExtraChildRate = null,
    [property: Id(9)] List<string>? Amenities = null,
    [property: Id(10)] List<string>? BedConfigurations = null);

public interface IRoomTypeGrain : IGrainWithStringKey
{
    Task CreateAsync(CreateRoomTypeCommand command);
    Task UpdateAsync(UpdateRoomTypeCommand command);
    Task DeactivateAsync();
    Task<RoomTypeState> GetStateAsync();
    Task<bool> ExistsAsync();
}
