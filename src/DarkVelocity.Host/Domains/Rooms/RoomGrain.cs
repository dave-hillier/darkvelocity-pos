using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

public class RoomGrain : Grain, IRoomGrain
{
    private readonly IPersistentState<RoomState> _state;

    public RoomGrain(
        [PersistentState("room", "OrleansStorage")]
        IPersistentState<RoomState> state)
    {
        _state = state;
    }

    public async Task CreateAsync(CreateRoomCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("Room already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, _, roomId) = GrainKeys.ParseSiteEntity(key);

        _state.State = new RoomState
        {
            Id = roomId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            RoomTypeId = command.RoomTypeId,
            Number = command.Number,
            Name = command.Name,
            Floor = command.Floor,
            Features = command.Features ?? [],
            IsConnecting = command.IsConnecting,
            ConnectingRoomId = command.ConnectingRoomId,
            Status = RoomStatus.Available,
            HousekeepingStatus = HousekeepingStatus.Clean,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public async Task UpdateAsync(UpdateRoomCommand command)
    {
        EnsureExists();

        if (command.Number != null) _state.State.Number = command.Number;
        if (command.Name != null) _state.State.Name = command.Name;
        if (command.Floor.HasValue) _state.State.Floor = command.Floor.Value;
        if (command.RoomTypeId.HasValue) _state.State.RoomTypeId = command.RoomTypeId.Value;
        if (command.Features != null) _state.State.Features = command.Features;

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<RoomState> GetStateAsync() => Task.FromResult(_state.State);

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.Id != Guid.Empty);

    public async Task SetStatusAsync(RoomStatus status)
    {
        EnsureExists();
        _state.State.Status = status;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task OccupyAsync(OccupyRoomCommand command)
    {
        EnsureExists();

        if (_state.State.Status == RoomStatus.Occupied)
            throw new InvalidOperationException("Room is already occupied");

        if (_state.State.Status is RoomStatus.OutOfOrder or RoomStatus.OutOfService)
            throw new InvalidOperationException($"Room cannot be occupied: {_state.State.Status}");

        _state.State.Status = RoomStatus.Occupied;
        _state.State.CurrentOccupancy = new RoomOccupancy
        {
            ReservationId = command.ReservationId,
            GuestName = command.GuestName,
            GuestCount = command.GuestCount,
            CheckedInAt = DateTime.UtcNow,
            ExpectedCheckOut = command.ExpectedCheckOut
        };
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task VacateAsync()
    {
        EnsureExists();

        _state.State.Status = RoomStatus.Dirty;
        _state.State.HousekeepingStatus = HousekeepingStatus.Dirty;
        _state.State.CurrentOccupancy = null;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SetHousekeepingStatusAsync(HousekeepingStatus status)
    {
        EnsureExists();

        _state.State.HousekeepingStatus = status;

        if (status == HousekeepingStatus.Clean || status == HousekeepingStatus.Inspected)
        {
            if (_state.State.Status == RoomStatus.Dirty)
                _state.State.Status = status == HousekeepingStatus.Inspected
                    ? RoomStatus.Inspected
                    : RoomStatus.Available;
        }

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SetOutOfOrderAsync(string? reason)
    {
        EnsureExists();

        if (_state.State.Status == RoomStatus.Occupied)
            throw new InvalidOperationException("Cannot set occupied room out of order");

        _state.State.Status = RoomStatus.OutOfOrder;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task ReturnToServiceAsync()
    {
        EnsureExists();

        if (_state.State.Status is not (RoomStatus.OutOfOrder or RoomStatus.OutOfService))
            throw new InvalidOperationException($"Room is not out of order/service: {_state.State.Status}");

        _state.State.Status = _state.State.HousekeepingStatus == HousekeepingStatus.Clean
            ? RoomStatus.Available
            : RoomStatus.Dirty;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("Room does not exist");
    }
}
