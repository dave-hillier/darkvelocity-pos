using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using Orleans.EventSourcing;
using Orleans.Providers;

namespace DarkVelocity.Host.Grains;

[LogConsistencyProvider(ProviderName = "LogStorage")]
public class RoomReservationGrain : JournaledGrain<RoomReservationState, IRoomReservationEvent>, IRoomReservationGrain
{
    protected override void TransitionState(RoomReservationState state, IRoomReservationEvent @event)
    {
        switch (@event)
        {
            case RoomReservationCreated e:
                state.Id = e.ReservationId;
                state.OrganizationId = e.OrganizationId;
                state.SiteId = e.SiteId;
                state.RoomTypeId = e.RoomTypeId;
                state.CheckInDate = e.CheckInDate;
                state.CheckOutDate = e.CheckOutDate;
                state.Adults = e.Adults;
                state.Children = e.Children;
                state.Status = ReservationStatus.Requested;
                state.Guest = new GuestInfo
                {
                    Name = e.GuestName,
                    Email = e.GuestEmail,
                    Phone = e.GuestPhone
                };
                state.CustomerId = e.CustomerId;
                state.SpecialRequests = e.SpecialRequests;
                state.Source = !string.IsNullOrEmpty(e.Source) && Enum.TryParse<ReservationSource>(e.Source, out var source)
                    ? source : ReservationSource.Direct;
                state.CreatedAt = e.OccurredAt;
                break;

            case RoomReservationConfirmed e:
                state.Status = ReservationStatus.Confirmed;
                state.ConfirmedAt = e.OccurredAt;
                break;

            case RoomReservationModified e:
                if (e.NewCheckInDate.HasValue) state.CheckInDate = e.NewCheckInDate.Value;
                if (e.NewCheckOutDate.HasValue) state.CheckOutDate = e.NewCheckOutDate.Value;
                if (e.NewRoomTypeId.HasValue) state.RoomTypeId = e.NewRoomTypeId.Value;
                if (e.NewAdults.HasValue) state.Adults = e.NewAdults.Value;
                if (e.NewChildren.HasValue) state.Children = e.NewChildren.Value;
                if (e.NewSpecialRequests != null) state.SpecialRequests = e.NewSpecialRequests;
                break;

            case RoomReservationCancelled e:
                state.Status = ReservationStatus.Cancelled;
                state.CancelledAt = e.OccurredAt;
                state.CancellationReason = e.Reason;
                state.CancelledBy = e.CancelledBy;
                break;

            case GuestCheckedIn e:
                state.Status = ReservationStatus.CheckedIn;
                state.CheckedInAt = e.OccurredAt;
                if (e.RoomId.HasValue)
                {
                    state.AssignedRoomId = e.RoomId;
                    state.AssignedRoomNumber = e.RoomNumber;
                }
                break;

            case RoomAssigned e:
                state.AssignedRoomId = e.RoomId;
                state.AssignedRoomNumber = e.RoomNumber;
                break;

            case GuestCheckedOut e:
                state.Status = ReservationStatus.CheckedOut;
                state.CheckedOutAt = e.OccurredAt;
                break;

            case RoomReservationNoShow e:
                state.Status = ReservationStatus.NoShow;
                break;

            case RoomReservationDepositRequired e:
                state.Deposit = new DepositInfo
                {
                    Amount = e.AmountRequired,
                    Status = DepositStatus.Required,
                    RequiredAt = e.DueBy
                };
                state.Status = ReservationStatus.PendingDeposit;
                break;

            case RoomReservationDepositPaid e:
                if (state.Deposit != null)
                {
                    state.Deposit = state.Deposit with
                    {
                        Status = DepositStatus.Paid,
                        PaidAt = e.OccurredAt,
                        PaymentMethod = Enum.TryParse<PaymentMethod>(e.PaymentMethod, out var pm) ? pm : PaymentMethod.Cash,
                        PaymentReference = e.PaymentReference
                    };
                }
                break;
        }
    }

    public async Task<RoomReservationRequestedResult> RequestAsync(RequestRoomReservationCommand command)
    {
        if (State.Id != Guid.Empty)
            throw new InvalidOperationException("Reservation already exists");

        if (command.CheckOutDate <= command.CheckInDate)
            throw new ArgumentException("Check-out date must be after check-in date");

        var key = this.GetPrimaryKeyString();
        var (_, _, _, reservationId) = GrainKeys.ParseSiteEntity(key);

        var confirmationCode = GenerateConfirmationCode();

        RaiseEvent(new RoomReservationCreated
        {
            ReservationId = reservationId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            RoomTypeId = command.RoomTypeId,
            CheckInDate = command.CheckInDate,
            CheckOutDate = command.CheckOutDate,
            Adults = command.Adults,
            Children = command.Children,
            GuestName = command.Guest.Name,
            GuestEmail = command.Guest.Email,
            GuestPhone = command.Guest.Phone,
            CustomerId = command.CustomerId,
            Source = command.Source.ToString(),
            SpecialRequests = command.SpecialRequests,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        State.ConfirmationCode = confirmationCode;
        State.RatePlan = command.RatePlan;
        State.ExternalRef = command.ExternalRef;

        // Sell inventory for each night of the stay
        await SellInventoryAsync(command.OrganizationId, command.SiteId, command.RoomTypeId,
            command.CheckInDate, command.CheckOutDate);

        return new RoomReservationRequestedResult(reservationId, confirmationCode, State.CreatedAt);
    }

    public Task<RoomReservationState> GetStateAsync() => Task.FromResult(State);

    public async Task ConfirmAsync()
    {
        EnsureExists();
        EnsureStatus(ReservationStatus.Requested, ReservationStatus.PendingDeposit);

        if (State.Deposit != null && State.Deposit.Status == DepositStatus.Required)
            throw new InvalidOperationException("Deposit required but not paid");

        RaiseEvent(new RoomReservationConfirmed
        {
            ReservationId = State.Id,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task ModifyAsync(ModifyRoomReservationCommand command)
    {
        EnsureExists();
        EnsureStatus(ReservationStatus.Requested, ReservationStatus.Confirmed);

        var oldCheckIn = State.CheckInDate;
        var oldCheckOut = State.CheckOutDate;
        var oldRoomTypeId = State.RoomTypeId;

        var newCheckIn = command.NewCheckInDate ?? oldCheckIn;
        var newCheckOut = command.NewCheckOutDate ?? oldCheckOut;
        var newRoomTypeId = command.NewRoomTypeId ?? oldRoomTypeId;

        if (newCheckOut <= newCheckIn)
            throw new ArgumentException("Check-out date must be after check-in date");

        RaiseEvent(new RoomReservationModified
        {
            ReservationId = State.Id,
            NewCheckInDate = command.NewCheckInDate,
            NewCheckOutDate = command.NewCheckOutDate,
            NewRoomTypeId = command.NewRoomTypeId,
            NewAdults = command.NewAdults,
            NewChildren = command.NewChildren,
            NewSpecialRequests = command.SpecialRequests,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Adjust inventory if dates or room type changed
        var datesChanged = newCheckIn != oldCheckIn || newCheckOut != oldCheckOut;
        var roomTypeChanged = newRoomTypeId != oldRoomTypeId;

        if (datesChanged || roomTypeChanged)
        {
            await ReleaseInventoryAsync(State.OrganizationId, State.SiteId, oldRoomTypeId, oldCheckIn, oldCheckOut);
            await SellInventoryAsync(State.OrganizationId, State.SiteId, newRoomTypeId, newCheckIn, newCheckOut);
        }
    }

    public async Task CancelAsync(CancelRoomReservationCommand command)
    {
        EnsureExists();

        if (State.Status == ReservationStatus.Cancelled)
            throw new InvalidOperationException("Reservation already cancelled");

        if (State.Status is ReservationStatus.CheckedOut)
            throw new InvalidOperationException("Cannot cancel a checked-out reservation");

        RaiseEvent(new RoomReservationCancelled
        {
            ReservationId = State.Id,
            Reason = command.Reason,
            CancelledBy = command.CancelledBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Release inventory
        await ReleaseInventoryAsync(State.OrganizationId, State.SiteId, State.RoomTypeId,
            State.CheckInDate, State.CheckOutDate);
    }

    public async Task CheckInAsync(CheckInCommand command)
    {
        EnsureExists();
        EnsureStatus(ReservationStatus.Confirmed);

        RaiseEvent(new GuestCheckedIn
        {
            ReservationId = State.Id,
            RoomId = command.RoomId,
            RoomNumber = command.RoomNumber,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // If a room was assigned, occupy it
        if (command.RoomId.HasValue)
        {
            var roomGrain = GrainFactory.GetGrain<IRoomGrain>(
                GrainKeys.Room(State.OrganizationId, State.SiteId, command.RoomId.Value));
            await roomGrain.OccupyAsync(new OccupyRoomCommand(
                State.Id, State.Guest.Name, State.Adults + State.Children, State.CheckOutDate));
        }
    }

    public async Task AssignRoomAsync(AssignRoomCommand command)
    {
        EnsureExists();

        RaiseEvent(new RoomAssigned
        {
            ReservationId = State.Id,
            RoomId = command.RoomId,
            RoomNumber = command.RoomNumber,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // If already checked in, occupy the new room
        if (State.Status is ReservationStatus.CheckedIn or ReservationStatus.InHouse)
        {
            var roomGrain = GrainFactory.GetGrain<IRoomGrain>(
                GrainKeys.Room(State.OrganizationId, State.SiteId, command.RoomId));
            await roomGrain.OccupyAsync(new OccupyRoomCommand(
                State.Id, State.Guest.Name, State.Adults + State.Children, State.CheckOutDate));
        }
    }

    public async Task CheckOutAsync()
    {
        EnsureExists();
        EnsureStatus(ReservationStatus.CheckedIn, ReservationStatus.InHouse);

        RaiseEvent(new GuestCheckedOut
        {
            ReservationId = State.Id,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Vacate the room
        if (State.AssignedRoomId.HasValue)
        {
            var roomGrain = GrainFactory.GetGrain<IRoomGrain>(
                GrainKeys.Room(State.OrganizationId, State.SiteId, State.AssignedRoomId.Value));
            await roomGrain.VacateAsync();
        }
    }

    public async Task MarkNoShowAsync(Guid? markedBy = null)
    {
        EnsureExists();
        EnsureStatus(ReservationStatus.Confirmed);

        RaiseEvent(new RoomReservationNoShow
        {
            ReservationId = State.Id,
            MarkedBy = markedBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Release inventory
        await ReleaseInventoryAsync(State.OrganizationId, State.SiteId, State.RoomTypeId,
            State.CheckInDate, State.CheckOutDate);
    }

    public async Task RequireDepositAsync(RequireRoomDepositCommand command)
    {
        EnsureExists();
        EnsureStatus(ReservationStatus.Requested);

        RaiseEvent(new RoomReservationDepositRequired
        {
            ReservationId = State.Id,
            AmountRequired = command.Amount,
            DueBy = command.RequiredBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task RecordDepositPaymentAsync(RecordRoomDepositPaymentCommand command)
    {
        EnsureExists();

        if (State.Deposit == null)
            throw new InvalidOperationException("No deposit required");

        if (State.Deposit.Status != DepositStatus.Required)
            throw new InvalidOperationException($"Deposit is not required: {State.Deposit.Status}");

        RaiseEvent(new RoomReservationDepositPaid
        {
            ReservationId = State.Id,
            Amount = State.Deposit.Amount,
            PaymentMethod = command.Method.ToString(),
            PaymentReference = command.PaymentReference,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public Task<bool> ExistsAsync() => Task.FromResult(State.Id != Guid.Empty);

    private void EnsureExists()
    {
        if (State.Id == Guid.Empty)
            throw new InvalidOperationException("Reservation does not exist");
    }

    private void EnsureStatus(params ReservationStatus[] allowedStatuses)
    {
        if (!allowedStatuses.Contains(State.Status))
            throw new InvalidOperationException(
                $"Invalid status. Expected one of [{string.Join(", ", allowedStatuses)}], got {State.Status}");
    }

    private async Task SellInventoryAsync(Guid orgId, Guid siteId, Guid roomTypeId, DateOnly checkIn, DateOnly checkOut)
    {
        var roomTypeGrain = GrainFactory.GetGrain<IRoomTypeGrain>(GrainKeys.RoomType(orgId, siteId, roomTypeId));
        var roomType = await roomTypeGrain.GetStateAsync();
        var totalRooms = roomType.TotalRooms;

        for (var date = checkIn; date < checkOut; date = date.AddDays(1))
        {
            var inventoryGrain = GrainFactory.GetGrain<IRoomInventoryGrain>(
                GrainKeys.RoomInventory(orgId, siteId, roomTypeId, date));

            if (!await inventoryGrain.ExistsAsync())
                await inventoryGrain.InitializeAsync(orgId, siteId, roomTypeId, date, totalRooms);

            await inventoryGrain.SellRoomAsync(State.Id);
        }
    }

    private async Task ReleaseInventoryAsync(Guid orgId, Guid siteId, Guid roomTypeId, DateOnly checkIn, DateOnly checkOut)
    {
        for (var date = checkIn; date < checkOut; date = date.AddDays(1))
        {
            var inventoryGrain = GrainFactory.GetGrain<IRoomInventoryGrain>(
                GrainKeys.RoomInventory(orgId, siteId, roomTypeId, date));

            if (await inventoryGrain.ExistsAsync())
                await inventoryGrain.ReleaseSoldRoomAsync(State.Id);
        }
    }

    private static string GenerateConfirmationCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 8).Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
