using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class RoomGrainTests
{
    private readonly TestClusterFixture _fixture;

    public RoomGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IRoomGrain> CreateRoomAsync(Guid orgId, Guid siteId, Guid roomId, Guid roomTypeId)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IRoomGrain>(GrainKeys.Room(orgId, siteId, roomId));
        await grain.CreateAsync(new CreateRoomCommand(orgId, siteId, roomTypeId, "101", 1, "Ocean View King"));
        return grain;
    }

    // Given: a new room with type and floor
    // When: the room is created
    // Then: the room has Available status and Clean housekeeping
    [Fact]
    public async Task CreateAsync_ShouldCreateRoomWithAvailableStatus()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();

        var grain = await CreateRoomAsync(orgId, siteId, roomId, roomTypeId);

        var state = await grain.GetStateAsync();
        state.Id.Should().Be(roomId);
        state.Number.Should().Be("101");
        state.Name.Should().Be("Ocean View King");
        state.Floor.Should().Be(1);
        state.RoomTypeId.Should().Be(roomTypeId);
        state.Status.Should().Be(RoomStatus.Available);
        state.HousekeepingStatus.Should().Be(HousekeepingStatus.Clean);
    }

    // Given: an available, clean room
    // When: a guest is checked in
    // Then: the room becomes Occupied with guest occupancy details
    [Fact]
    public async Task OccupyAsync_ShouldMakeRoomOccupied()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();

        var grain = await CreateRoomAsync(orgId, siteId, roomId, roomTypeId);

        var reservationId = Guid.NewGuid();
        var checkOut = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        await grain.OccupyAsync(new OccupyRoomCommand(reservationId, "Jane Doe", 2, checkOut));

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(RoomStatus.Occupied);
        state.CurrentOccupancy.Should().NotBeNull();
        state.CurrentOccupancy!.ReservationId.Should().Be(reservationId);
        state.CurrentOccupancy.GuestName.Should().Be("Jane Doe");
        state.CurrentOccupancy.GuestCount.Should().Be(2);
        state.CurrentOccupancy.ExpectedCheckOut.Should().Be(checkOut);
    }

    // Given: an occupied room
    // When: the room is vacated (guest checks out)
    // Then: the room becomes Dirty with no occupancy and housekeeping is set to Dirty
    [Fact]
    public async Task VacateAsync_ShouldMakeRoomDirty()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();

        var grain = await CreateRoomAsync(orgId, siteId, roomId, roomTypeId);
        await grain.OccupyAsync(new OccupyRoomCommand(
            Guid.NewGuid(), "Guest", 1, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))));

        await grain.VacateAsync();

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(RoomStatus.Dirty);
        state.HousekeepingStatus.Should().Be(HousekeepingStatus.Dirty);
        state.CurrentOccupancy.Should().BeNull();
    }

    // Given: a dirty room after checkout
    // When: housekeeping marks it as Clean
    // Then: the room returns to Available status
    [Fact]
    public async Task SetHousekeepingStatusAsync_CleanDirtyRoom_ShouldMakeAvailable()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();

        var grain = await CreateRoomAsync(orgId, siteId, roomId, roomTypeId);
        await grain.OccupyAsync(new OccupyRoomCommand(
            Guid.NewGuid(), "Guest", 1, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))));
        await grain.VacateAsync();

        await grain.SetHousekeepingStatusAsync(HousekeepingStatus.Clean);

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(RoomStatus.Available);
        state.HousekeepingStatus.Should().Be(HousekeepingStatus.Clean);
    }

    // Given: a dirty room after checkout
    // When: housekeeping marks it as Inspected
    // Then: the room status is Inspected
    [Fact]
    public async Task SetHousekeepingStatusAsync_Inspected_ShouldSetInspectedStatus()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();

        var grain = await CreateRoomAsync(orgId, siteId, roomId, roomTypeId);
        await grain.OccupyAsync(new OccupyRoomCommand(
            Guid.NewGuid(), "Guest", 1, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))));
        await grain.VacateAsync();

        await grain.SetHousekeepingStatusAsync(HousekeepingStatus.Inspected);

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(RoomStatus.Inspected);
    }

    // Given: an available room
    // When: maintenance sets it out of order
    // Then: the room is OutOfOrder and cannot be occupied
    [Fact]
    public async Task SetOutOfOrderAsync_ShouldPreventOccupation()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();

        var grain = await CreateRoomAsync(orgId, siteId, roomId, roomTypeId);
        await grain.SetOutOfOrderAsync("Plumbing repair");

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(RoomStatus.OutOfOrder);

        var act = () => grain.OccupyAsync(new OccupyRoomCommand(
            Guid.NewGuid(), "Guest", 1, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be occupied*");
    }

    // Given: a room that is out of order
    // When: maintenance returns it to service
    // Then: the room status reflects its housekeeping state
    [Fact]
    public async Task ReturnToServiceAsync_ShouldRestoreBasedOnHousekeeping()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();

        var grain = await CreateRoomAsync(orgId, siteId, roomId, roomTypeId);
        await grain.SetOutOfOrderAsync();

        await grain.ReturnToServiceAsync();

        var state = await grain.GetStateAsync();
        // Room was clean when set OOO, so it returns to Available
        state.Status.Should().Be(RoomStatus.Available);
    }
}
