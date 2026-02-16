using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class RoomReservationGrainTests
{
    private readonly TestClusterFixture _fixture;

    public RoomReservationGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private GuestInfo CreateGuestInfo(string name = "John Doe") => new()
    {
        Name = name,
        Phone = "+1234567890",
        Email = "john@example.com"
    };

    private async Task<IRoomTypeGrain> CreateRoomTypeAsync(Guid orgId, Guid siteId, Guid roomTypeId, int totalRooms = 20)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IRoomTypeGrain>(GrainKeys.RoomType(orgId, siteId, roomTypeId));
        await grain.CreateAsync(new CreateRoomTypeCommand(
            orgId, siteId, "Deluxe King", "DLK", 2, 3, totalRooms, 199.00m,
            "A luxurious king room", 2, 1));
        return grain;
    }

    private async Task<IRoomReservationGrain> CreateReservationAsync(
        Guid orgId, Guid siteId, Guid reservationId, Guid roomTypeId,
        DateOnly? checkIn = null, DateOnly? checkOut = null, int adults = 2)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IRoomReservationGrain>(
            GrainKeys.RoomReservation(orgId, siteId, reservationId));
        await grain.RequestAsync(new RequestRoomReservationCommand(
            orgId, siteId, roomTypeId,
            checkIn ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            checkOut ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            adults, CreateGuestInfo()));
        return grain;
    }

    // Given: a guest requesting a 3-night stay in a Deluxe King room
    // When: the reservation is submitted via the website
    // Then: a new reservation is created with an 8-character confirmation code and all details persisted
    [Fact]
    public async Task RequestAsync_ShouldCreateReservation()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        await CreateRoomTypeAsync(orgId, siteId, roomTypeId);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IRoomReservationGrain>(
            GrainKeys.RoomReservation(orgId, siteId, reservationId));

        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14));
        var checkOut = checkIn.AddDays(3);

        var result = await grain.RequestAsync(new RequestRoomReservationCommand(
            orgId, siteId, roomTypeId, checkIn, checkOut, 2, CreateGuestInfo("Jane Smith"),
            Children: 1, RatePlan: RatePlanType.BestAvailable,
            SpecialRequests: "High floor please", Source: ReservationSource.Website));

        result.Id.Should().Be(reservationId);
        result.ConfirmationCode.Should().HaveLength(8);

        var state = await grain.GetStateAsync();
        state.RoomTypeId.Should().Be(roomTypeId);
        state.CheckInDate.Should().Be(checkIn);
        state.CheckOutDate.Should().Be(checkOut);
        state.Adults.Should().Be(2);
        state.Children.Should().Be(1);
        state.Guest.Name.Should().Be("Jane Smith");
        state.Status.Should().Be(ReservationStatus.Requested);
        state.SpecialRequests.Should().Be("High floor please");
        state.Source.Should().Be(ReservationSource.Website);
    }

    // Given: checkout date is before or equal to check-in date
    // When: the reservation is submitted
    // Then: an ArgumentException is thrown
    [Fact]
    public async Task RequestAsync_InvalidDates_ShouldThrow()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        await CreateRoomTypeAsync(orgId, siteId, roomTypeId);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IRoomReservationGrain>(
            GrainKeys.RoomReservation(orgId, siteId, reservationId));

        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

        var act = () => grain.RequestAsync(new RequestRoomReservationCommand(
            orgId, siteId, roomTypeId, checkIn, checkIn, 2, CreateGuestInfo()));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Check-out date must be after check-in date*");
    }

    // Given: a reservation in Requested status
    // When: the front desk confirms the reservation
    // Then: the status changes to Confirmed with a confirmation timestamp
    [Fact]
    public async Task ConfirmAsync_ShouldConfirmReservation()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        await CreateRoomTypeAsync(orgId, siteId, roomTypeId);
        var grain = await CreateReservationAsync(orgId, siteId, reservationId, roomTypeId);

        await grain.ConfirmAsync();

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(ReservationStatus.Confirmed);
        state.ConfirmedAt.Should().NotBeNull();
    }

    // Given: a confirmed reservation
    // When: the guest checks in with a specific room assigned
    // Then: the status changes to CheckedIn and the room is recorded
    [Fact]
    public async Task CheckInAsync_ShouldCheckInGuest()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        await CreateRoomTypeAsync(orgId, siteId, roomTypeId);

        // Create a room
        var roomGrain = _fixture.Cluster.GrainFactory.GetGrain<IRoomGrain>(
            GrainKeys.Room(orgId, siteId, roomId));
        await roomGrain.CreateAsync(new CreateRoomCommand(orgId, siteId, roomTypeId, "412", 4));

        var grain = await CreateReservationAsync(orgId, siteId, reservationId, roomTypeId);
        await grain.ConfirmAsync();

        await grain.CheckInAsync(new CheckInCommand(roomId, "412"));

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(ReservationStatus.CheckedIn);
        state.AssignedRoomId.Should().Be(roomId);
        state.AssignedRoomNumber.Should().Be("412");
        state.CheckedInAt.Should().NotBeNull();

        // Room should be occupied
        var roomState = await roomGrain.GetStateAsync();
        roomState.Status.Should().Be(RoomStatus.Occupied);
        roomState.CurrentOccupancy.Should().NotBeNull();
        roomState.CurrentOccupancy!.ReservationId.Should().Be(reservationId);
    }

    // Given: a checked-in guest
    // When: the guest checks out
    // Then: the reservation is CheckedOut and the room becomes Dirty
    [Fact]
    public async Task CheckOutAsync_ShouldCheckOutAndVacateRoom()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        await CreateRoomTypeAsync(orgId, siteId, roomTypeId);

        var roomGrain = _fixture.Cluster.GrainFactory.GetGrain<IRoomGrain>(
            GrainKeys.Room(orgId, siteId, roomId));
        await roomGrain.CreateAsync(new CreateRoomCommand(orgId, siteId, roomTypeId, "501", 5));

        var grain = await CreateReservationAsync(orgId, siteId, reservationId, roomTypeId);
        await grain.ConfirmAsync();
        await grain.CheckInAsync(new CheckInCommand(roomId, "501"));

        await grain.CheckOutAsync();

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(ReservationStatus.CheckedOut);
        state.CheckedOutAt.Should().NotBeNull();

        // Room should be dirty
        var roomState = await roomGrain.GetStateAsync();
        roomState.Status.Should().Be(RoomStatus.Dirty);
        roomState.HousekeepingStatus.Should().Be(HousekeepingStatus.Dirty);
        roomState.CurrentOccupancy.Should().BeNull();
    }

    // Given: a confirmed reservation
    // When: the guest is marked as a no-show
    // Then: the reservation is marked NoShow and inventory is released
    [Fact]
    public async Task MarkNoShowAsync_ShouldReleaseInventory()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        await CreateRoomTypeAsync(orgId, siteId, roomTypeId, totalRooms: 10);

        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var checkOut = checkIn.AddDays(2);

        var grain = await CreateReservationAsync(orgId, siteId, reservationId, roomTypeId, checkIn, checkOut);
        await grain.ConfirmAsync();

        // Check inventory before no-show
        var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IRoomInventoryGrain>(
            GrainKeys.RoomInventory(orgId, siteId, roomTypeId, checkIn));
        var beforeAvailability = await inventoryGrain.GetAvailabilityAsync();
        beforeAvailability.Sold.Should().Be(1);

        await grain.MarkNoShowAsync();

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(ReservationStatus.NoShow);

        // Inventory should be released
        var afterAvailability = await inventoryGrain.GetAvailabilityAsync();
        afterAvailability.Sold.Should().Be(0);
    }

    // Given: a confirmed reservation
    // When: the guest cancels the reservation
    // Then: the reservation is Cancelled and inventory is released
    [Fact]
    public async Task CancelAsync_ShouldReleaseInventory()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        await CreateRoomTypeAsync(orgId, siteId, roomTypeId, totalRooms: 10);

        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20));
        var checkOut = checkIn.AddDays(3);

        var grain = await CreateReservationAsync(orgId, siteId, reservationId, roomTypeId, checkIn, checkOut);
        await grain.ConfirmAsync();

        await grain.CancelAsync(new CancelRoomReservationCommand("Plans changed", Guid.NewGuid()));

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(ReservationStatus.Cancelled);
        state.CancellationReason.Should().Be("Plans changed");

        // Inventory should be released for all 3 nights
        for (var date = checkIn; date < checkOut; date = date.AddDays(1))
        {
            var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IRoomInventoryGrain>(
                GrainKeys.RoomInventory(orgId, siteId, roomTypeId, date));
            var availability = await inventoryGrain.GetAvailabilityAsync();
            availability.Sold.Should().Be(0, $"night of {date} should have 0 sold after cancellation");
        }
    }

    // Given: a reservation for 3 nights
    // When: the guest modifies to a different check-out date (extending by 2 nights)
    // Then: the inventory adjusts: old nights released, new nights sold
    [Fact]
    public async Task ModifyAsync_ChangeDates_ShouldAdjustInventory()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        await CreateRoomTypeAsync(orgId, siteId, roomTypeId, totalRooms: 10);

        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var checkOut = checkIn.AddDays(2);

        var grain = await CreateReservationAsync(orgId, siteId, reservationId, roomTypeId, checkIn, checkOut);

        // Extend stay by 2 nights
        var newCheckOut = checkOut.AddDays(2);
        await grain.ModifyAsync(new ModifyRoomReservationCommand(NewCheckOutDate: newCheckOut));

        var state = await grain.GetStateAsync();
        state.CheckOutDate.Should().Be(newCheckOut);

        // Inventory should be sold for all 4 nights
        for (var date = checkIn; date < newCheckOut; date = date.AddDays(1))
        {
            var inventoryGrain = _fixture.Cluster.GrainFactory.GetGrain<IRoomInventoryGrain>(
                GrainKeys.RoomInventory(orgId, siteId, roomTypeId, date));
            var availability = await inventoryGrain.GetAvailabilityAsync();
            availability.Sold.Should().Be(1, $"night of {date} should have 1 sold after modification");
        }
    }
}
