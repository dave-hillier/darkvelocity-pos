using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using Orleans.EventSourcing;
using Orleans.Providers;

namespace DarkVelocity.Host.Grains;

[LogConsistencyProvider(ProviderName = "LogStorage")]
public class RoomReservationSettingsGrain : JournaledGrain<RoomReservationSettingsState, IRoomReservationSettingsEvent>, IRoomReservationSettingsGrain
{
    protected override void TransitionState(RoomReservationSettingsState state, IRoomReservationSettingsEvent @event)
    {
        switch (@event)
        {
            case RoomReservationSettingsInitialized e:
                state.OrganizationId = e.OrganizationId;
                state.SiteId = e.SiteId;
                break;

            case RoomReservationSettingsUpdated e:
                if (e.DefaultCheckInTime.HasValue) state.DefaultCheckInTime = e.DefaultCheckInTime.Value;
                if (e.DefaultCheckOutTime.HasValue) state.DefaultCheckOutTime = e.DefaultCheckOutTime.Value;
                if (e.AdvanceBookingDays.HasValue) state.AdvanceBookingDays = e.AdvanceBookingDays.Value;
                if (e.MinStayNights.HasValue) state.MinStayNights = e.MinStayNights.Value;
                if (e.MaxStayNights.HasValue) state.MaxStayNights = e.MaxStayNights.Value;
                if (e.OverbookingPercent.HasValue) state.OverbookingPercent = e.OverbookingPercent.Value;
                if (e.RequireDeposit.HasValue) state.RequireDeposit = e.RequireDeposit.Value;
                if (e.DepositAmount.HasValue) state.DepositAmount = e.DepositAmount.Value;
                if (e.FreeCancellationWindow.HasValue) state.FreeCancellationWindow = e.FreeCancellationWindow.Value;
                state.Version++;
                break;
        }
    }

    public async Task InitializeAsync(Guid organizationId, Guid siteId)
    {
        if (State.SiteId != Guid.Empty)
            return;

        RaiseEvent(new RoomReservationSettingsInitialized
        {
            SiteId = siteId,
            OrganizationId = organizationId,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task UpdateAsync(UpdateRoomReservationSettingsCommand command)
    {
        EnsureExists();

        RaiseEvent(new RoomReservationSettingsUpdated
        {
            SiteId = State.SiteId,
            DefaultCheckInTime = command.DefaultCheckInTime,
            DefaultCheckOutTime = command.DefaultCheckOutTime,
            AdvanceBookingDays = command.AdvanceBookingDays,
            MinStayNights = command.MinStayNights,
            MaxStayNights = command.MaxStayNights,
            OverbookingPercent = command.OverbookingPercent,
            RequireDeposit = command.RequireDeposit,
            DepositAmount = command.DepositAmount,
            FreeCancellationWindow = command.FreeCancellationWindow,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        if (command.AllowChildren.HasValue) State.AllowChildren = command.AllowChildren.Value;
        if (command.ChildMaxAge.HasValue) State.ChildMaxAge = command.ChildMaxAge.Value;
    }

    public Task<RoomReservationSettingsState> GetStateAsync() => Task.FromResult(State);

    public Task<bool> ExistsAsync() => Task.FromResult(State.SiteId != Guid.Empty);

    public async Task CloseToArrivalAsync(DateOnly date)
    {
        EnsureExists();
        if (!State.ClosedToArrivalDates.Contains(date))
        {
            State.ClosedToArrivalDates.Add(date);
        }
    }

    public async Task OpenToArrivalAsync(DateOnly date)
    {
        EnsureExists();
        State.ClosedToArrivalDates.Remove(date);
    }

    public async Task CloseToDepartureAsync(DateOnly date)
    {
        EnsureExists();
        if (!State.ClosedToDepartureDates.Contains(date))
        {
            State.ClosedToDepartureDates.Add(date);
        }
    }

    public async Task OpenToDepartureAsync(DateOnly date)
    {
        EnsureExists();
        State.ClosedToDepartureDates.Remove(date);
    }

    private void EnsureExists()
    {
        if (State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Room reservation settings not initialized");
    }
}
