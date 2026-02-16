using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class RoomInventoryGrainTests
{
    private readonly TestClusterFixture _fixture;

    public RoomInventoryGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IRoomInventoryGrain> CreateInventoryAsync(
        Guid orgId, Guid siteId, Guid roomTypeId, DateOnly date, int totalRooms = 20)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IRoomInventoryGrain>(
            GrainKeys.RoomInventory(orgId, siteId, roomTypeId, date));
        await grain.InitializeAsync(orgId, siteId, roomTypeId, date, totalRooms);
        return grain;
    }

    // Given: a room type with 20 rooms and no bookings
    // When: availability is queried
    // Then: all 20 rooms are available
    [Fact]
    public async Task GetAvailabilityAsync_NoBookings_ShouldReturnFullCapacity()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

        var grain = await CreateInventoryAsync(orgId, siteId, roomTypeId, date, totalRooms: 20);

        var result = await grain.GetAvailabilityAsync();

        result.TotalRooms.Should().Be(20);
        result.Sold.Should().Be(0);
        result.Available.Should().Be(20);
    }

    // Given: 3 rooms sold out of 10
    // When: availability is queried
    // Then: 7 rooms are available
    [Fact]
    public async Task GetAvailabilityAsync_SomeRoomsSold_ShouldReturnCorrectAvailability()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14));

        var grain = await CreateInventoryAsync(orgId, siteId, roomTypeId, date, totalRooms: 10);

        await grain.SellRoomAsync(Guid.NewGuid());
        await grain.SellRoomAsync(Guid.NewGuid());
        await grain.SellRoomAsync(Guid.NewGuid());

        var result = await grain.GetAvailabilityAsync();

        result.Sold.Should().Be(3);
        result.Available.Should().Be(7);
    }

    // Given: all 5 rooms sold
    // When: 1 reservation is cancelled (room released)
    // Then: 1 room becomes available
    [Fact]
    public async Task ReleaseSoldRoomAsync_ShouldIncreaseAvailability()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(21));

        var grain = await CreateInventoryAsync(orgId, siteId, roomTypeId, date, totalRooms: 5);

        var resIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        foreach (var id in resIds)
            await grain.SellRoomAsync(id);

        var before = await grain.GetAvailabilityAsync();
        before.Available.Should().Be(0);

        await grain.ReleaseSoldRoomAsync(resIds[0]);

        var after = await grain.GetAvailabilityAsync();
        after.Available.Should().Be(1);
        after.Sold.Should().Be(4);
    }

    // Given: 10 rooms with 2 overbooking allowance
    // When: 11 rooms are sold
    // Then: 1 room still available (10 + 2 - 11 = 1)
    [Fact]
    public async Task OverbookingAllowance_ShouldExpandCapacity()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(28));

        var grain = await CreateInventoryAsync(orgId, siteId, roomTypeId, date, totalRooms: 10);
        await grain.SetOverbookingAllowanceAsync(2);

        for (int i = 0; i < 11; i++)
            await grain.SellRoomAsync(Guid.NewGuid());

        var result = await grain.GetAvailabilityAsync();

        result.TotalRooms.Should().Be(10);
        result.OverbookingAllowance.Should().Be(2);
        result.Sold.Should().Be(11);
        result.Available.Should().Be(1); // 10 + 2 - 11 = 1
    }

    // Given: rooms with a group block hold of 5 rooms
    // When: availability is queried
    // Then: the held rooms reduce availability
    [Fact]
    public async Task BlockRoomsAsync_ShouldReduceAvailability()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(35));

        var grain = await CreateInventoryAsync(orgId, siteId, roomTypeId, date, totalRooms: 20);

        var holdId = Guid.NewGuid();
        await grain.BlockRoomsAsync(holdId, "Wedding block", 5, date.AddDays(30));

        var result = await grain.GetAvailabilityAsync();
        result.Available.Should().Be(15); // 20 - 5 held

        // Release the block
        await grain.ReleaseBlockAsync(holdId);

        var afterRelease = await grain.GetAvailabilityAsync();
        afterRelease.Available.Should().Be(20);
    }

    // Given: 2 rooms out of order
    // When: availability is queried
    // Then: out-of-order rooms reduce availability
    [Fact]
    public async Task OutOfOrderCount_ShouldReduceAvailability()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(42));

        var grain = await CreateInventoryAsync(orgId, siteId, roomTypeId, date, totalRooms: 15);
        await grain.SetOutOfOrderCountAsync(2);

        var result = await grain.GetAvailabilityAsync();
        result.OutOfOrder.Should().Be(2);
        result.Available.Should().Be(13); // 15 - 2
    }
}
