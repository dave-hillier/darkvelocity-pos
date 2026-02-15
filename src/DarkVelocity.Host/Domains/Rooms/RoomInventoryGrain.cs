using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

public class RoomInventoryGrain : Grain, IRoomInventoryGrain
{
    private readonly IPersistentState<RoomInventoryState> _state;

    public RoomInventoryGrain(
        [PersistentState("roominventory", "OrleansStorage")]
        IPersistentState<RoomInventoryState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(Guid organizationId, Guid siteId, Guid roomTypeId, DateOnly date, int totalRooms)
    {
        if (_state.State.TotalRooms > 0)
            return;

        _state.State = new RoomInventoryState
        {
            OrganizationId = organizationId,
            SiteId = siteId,
            RoomTypeId = roomTypeId,
            Date = date,
            TotalRooms = totalRooms,
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public Task<RoomInventoryState> GetStateAsync() => Task.FromResult(_state.State);

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.TotalRooms > 0);

    public Task<RoomAvailabilityResult> GetAvailabilityAsync()
    {
        var holdCount = _state.State.Holds.Sum(h => h.RoomCount);
        var effectiveCapacity = _state.State.TotalRooms + _state.State.OverbookingAllowance;
        var consumed = _state.State.SoldCount + _state.State.BlockedCount + _state.State.OutOfOrderCount + holdCount;
        var available = Math.Max(0, effectiveCapacity - consumed);

        return Task.FromResult(new RoomAvailabilityResult(
            _state.State.Date,
            _state.State.RoomTypeId,
            _state.State.TotalRooms,
            _state.State.SoldCount,
            _state.State.BlockedCount,
            _state.State.OutOfOrderCount,
            _state.State.OverbookingAllowance,
            available));
    }

    public async Task SellRoomAsync(Guid reservationId)
    {
        EnsureExists();
        _state.State.SoldCount++;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task ReleaseSoldRoomAsync(Guid reservationId)
    {
        EnsureExists();

        if (_state.State.SoldCount <= 0)
            throw new InvalidOperationException("No sold rooms to release");

        _state.State.SoldCount--;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task BlockRoomsAsync(Guid holdId, string reason, int count, DateOnly? releaseDate)
    {
        EnsureExists();

        if (_state.State.Holds.Any(h => h.HoldId == holdId))
            return;

        _state.State.Holds.Add(new RoomInventoryHold
        {
            HoldId = holdId,
            Reason = reason,
            RoomCount = count,
            ReleaseDate = releaseDate
        });
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task ReleaseBlockAsync(Guid holdId)
    {
        EnsureExists();

        var hold = _state.State.Holds.FirstOrDefault(h => h.HoldId == holdId);
        if (hold != null)
        {
            _state.State.Holds.Remove(hold);
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task SetOutOfOrderCountAsync(int count)
    {
        EnsureExists();
        _state.State.OutOfOrderCount = count;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SetOverbookingAllowanceAsync(int allowance)
    {
        EnsureExists();
        _state.State.OverbookingAllowance = allowance;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private void EnsureExists()
    {
        if (_state.State.TotalRooms == 0 && _state.State.Version == 0)
            throw new InvalidOperationException("Room inventory not initialized");
    }
}
