using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class RoomTypeGrainTests
{
    private readonly TestClusterFixture _fixture;

    public RoomTypeGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    // Given: a hotel site needing room type configuration
    // When: a Deluxe King room type is created with occupancy and rate details
    // Then: the room type is persisted with all properties
    [Fact]
    public async Task CreateAsync_ShouldCreateRoomType()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IRoomTypeGrain>(
            GrainKeys.RoomType(orgId, siteId, roomTypeId));

        await grain.CreateAsync(new CreateRoomTypeCommand(
            orgId, siteId, "Deluxe King", "DLK", 2, 3, 25, 249.00m,
            "Spacious king room with city view", 2, 1, 50.00m, 25.00m,
            ["WiFi", "Mini Bar", "Balcony"], ["King"]));

        var state = await grain.GetStateAsync();
        state.Id.Should().Be(roomTypeId);
        state.Name.Should().Be("Deluxe King");
        state.Code.Should().Be("DLK");
        state.BaseOccupancy.Should().Be(2);
        state.MaxOccupancy.Should().Be(3);
        state.TotalRooms.Should().Be(25);
        state.RackRate.Should().Be(249.00m);
        state.ExtraAdultRate.Should().Be(50.00m);
        state.ExtraChildRate.Should().Be(25.00m);
        state.Amenities.Should().Contain("Mini Bar");
        state.BedConfigurations.Should().Contain("King");
        state.IsActive.Should().BeTrue();
    }

    // Given: an existing room type
    // When: the rack rate and total rooms are updated
    // Then: only the modified fields change
    [Fact]
    public async Task UpdateAsync_ShouldUpdateSpecifiedFields()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IRoomTypeGrain>(
            GrainKeys.RoomType(orgId, siteId, roomTypeId));
        await grain.CreateAsync(new CreateRoomTypeCommand(
            orgId, siteId, "Standard Twin", "STW", 2, 2, 30, 149.00m));

        await grain.UpdateAsync(new UpdateRoomTypeCommand(
            RackRate: 159.00m, TotalRooms: 28));

        var state = await grain.GetStateAsync();
        state.Name.Should().Be("Standard Twin"); // unchanged
        state.RackRate.Should().Be(159.00m);
        state.TotalRooms.Should().Be(28);
        state.UpdatedAt.Should().NotBeNull();
    }

    // Given: an active room type
    // When: the room type is deactivated
    // Then: it is marked as inactive
    [Fact]
    public async Task DeactivateAsync_ShouldSetInactive()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IRoomTypeGrain>(
            GrainKeys.RoomType(orgId, siteId, roomTypeId));
        await grain.CreateAsync(new CreateRoomTypeCommand(
            orgId, siteId, "Suite", "STE", 2, 4, 5, 499.00m));

        await grain.DeactivateAsync();

        var state = await grain.GetStateAsync();
        state.IsActive.Should().BeFalse();
    }
}
