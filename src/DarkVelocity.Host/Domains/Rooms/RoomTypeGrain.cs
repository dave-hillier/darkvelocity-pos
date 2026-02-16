using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using Orleans.EventSourcing;
using Orleans.Providers;

namespace DarkVelocity.Host.Grains;

[LogConsistencyProvider(ProviderName = "LogStorage")]
public class RoomTypeGrain : JournaledGrain<RoomTypeState, IRoomTypeEvent>, IRoomTypeGrain
{
    protected override void TransitionState(RoomTypeState state, IRoomTypeEvent @event)
    {
        switch (@event)
        {
            case RoomTypeCreated e:
                state.Id = e.RoomTypeId;
                state.OrganizationId = e.OrganizationId;
                state.SiteId = e.SiteId;
                state.Name = e.Name;
                state.Code = e.Code;
                state.BaseOccupancy = e.BaseOccupancy;
                state.MaxOccupancy = e.MaxOccupancy;
                state.TotalRooms = e.TotalRooms;
                state.RackRate = e.RackRate;
                state.CreatedAt = e.OccurredAt;
                break;

            case RoomTypeUpdated e:
                if (e.Name != null) state.Name = e.Name;
                if (e.Description != null) state.Description = e.Description;
                if (e.MaxOccupancy.HasValue) state.MaxOccupancy = e.MaxOccupancy.Value;
                if (e.MaxAdults.HasValue) state.MaxAdults = e.MaxAdults.Value;
                if (e.MaxChildren.HasValue) state.MaxChildren = e.MaxChildren.Value;
                if (e.TotalRooms.HasValue) state.TotalRooms = e.TotalRooms.Value;
                if (e.RackRate.HasValue) state.RackRate = e.RackRate.Value;
                if (e.ExtraAdultRate.HasValue) state.ExtraAdultRate = e.ExtraAdultRate.Value;
                if (e.ExtraChildRate.HasValue) state.ExtraChildRate = e.ExtraChildRate.Value;
                state.UpdatedAt = e.OccurredAt;
                break;

            case RoomTypeDeactivated e:
                state.IsActive = false;
                state.UpdatedAt = e.OccurredAt;
                break;
        }
    }

    public async Task CreateAsync(CreateRoomTypeCommand command)
    {
        if (State.Id != Guid.Empty)
            throw new InvalidOperationException("Room type already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, _, roomTypeId) = GrainKeys.ParseSiteEntity(key);

        RaiseEvent(new RoomTypeCreated
        {
            RoomTypeId = roomTypeId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            Name = command.Name,
            Code = command.Code,
            BaseOccupancy = command.BaseOccupancy,
            MaxOccupancy = command.MaxOccupancy,
            TotalRooms = command.TotalRooms,
            RackRate = command.RackRate,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        State.MaxAdults = command.MaxAdults;
        State.MaxChildren = command.MaxChildren;
        State.ExtraAdultRate = command.ExtraAdultRate;
        State.ExtraChildRate = command.ExtraChildRate;
        State.Description = command.Description;
        State.Amenities = command.Amenities ?? [];
        State.BedConfigurations = command.BedConfigurations ?? [];
    }

    public async Task UpdateAsync(UpdateRoomTypeCommand command)
    {
        EnsureExists();

        RaiseEvent(new RoomTypeUpdated
        {
            RoomTypeId = State.Id,
            Name = command.Name,
            Description = command.Description,
            MaxOccupancy = command.MaxOccupancy,
            MaxAdults = command.MaxAdults,
            MaxChildren = command.MaxChildren,
            TotalRooms = command.TotalRooms,
            RackRate = command.RackRate,
            ExtraAdultRate = command.ExtraAdultRate,
            ExtraChildRate = command.ExtraChildRate,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        if (command.Amenities != null) State.Amenities = command.Amenities;
        if (command.BedConfigurations != null) State.BedConfigurations = command.BedConfigurations;
    }

    public async Task DeactivateAsync()
    {
        EnsureExists();

        RaiseEvent(new RoomTypeDeactivated
        {
            RoomTypeId = State.Id,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public Task<RoomTypeState> GetStateAsync() => Task.FromResult(State);

    public Task<bool> ExistsAsync() => Task.FromResult(State.Id != Guid.Empty);

    private void EnsureExists()
    {
        if (State.Id == Guid.Empty)
            throw new InvalidOperationException("Room type does not exist");
    }
}
