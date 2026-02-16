using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class RoomEndpoints
{
    public static WebApplication MapRoomEndpoints(this WebApplication app)
    {
        // ====================================================================
        // Room Types
        // ====================================================================
        var roomTypes = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/room-types").WithTags("Room Types");

        roomTypes.MapPost("/", async (
            Guid orgId, Guid siteId,
            [FromBody] CreateRoomTypeRequest request,
            IGrainFactory grainFactory) =>
        {
            var roomTypeId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IRoomTypeGrain>(GrainKeys.RoomType(orgId, siteId, roomTypeId));
            await grain.CreateAsync(new CreateRoomTypeCommand(
                orgId, siteId, request.Name, request.Code, request.BaseOccupancy, request.MaxOccupancy,
                request.TotalRooms, request.RackRate, request.Description, request.MaxAdults, request.MaxChildren,
                request.ExtraAdultRate, request.ExtraChildRate, request.Amenities, request.BedConfigurations));

            var state = await grain.GetStateAsync();
            return Results.Created(
                $"/api/orgs/{orgId}/sites/{siteId}/room-types/{roomTypeId}",
                Hal.Resource(state, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/room-types/{roomTypeId}" },
                    ["rooms"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/rooms?roomTypeId={roomTypeId}" }
                }));
        });

        roomTypes.MapGet("/{roomTypeId}", async (Guid orgId, Guid siteId, Guid roomTypeId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRoomTypeGrain>(GrainKeys.RoomType(orgId, siteId, roomTypeId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Room type not found"));

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/room-types/{roomTypeId}" },
                ["rooms"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/rooms?roomTypeId={roomTypeId}" },
                ["availability"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/room-availability?roomTypeId={roomTypeId}" }
            }));
        });

        roomTypes.MapPatch("/{roomTypeId}", async (
            Guid orgId, Guid siteId, Guid roomTypeId,
            [FromBody] UpdateRoomTypeRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRoomTypeGrain>(GrainKeys.RoomType(orgId, siteId, roomTypeId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Room type not found"));

            await grain.UpdateAsync(new UpdateRoomTypeCommand(
                request.Name, request.Description, request.MaxOccupancy, request.MaxAdults, request.MaxChildren,
                request.TotalRooms, request.RackRate, request.ExtraAdultRate, request.ExtraChildRate,
                request.Amenities, request.BedConfigurations));

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/room-types/{roomTypeId}" }
            }));
        });

        roomTypes.MapDelete("/{roomTypeId}", async (Guid orgId, Guid siteId, Guid roomTypeId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRoomTypeGrain>(GrainKeys.RoomType(orgId, siteId, roomTypeId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Room type not found"));

            await grain.DeactivateAsync();
            return Results.NoContent();
        });

        // ====================================================================
        // Rooms (physical)
        // ====================================================================
        var rooms = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/rooms").WithTags("Rooms");

        rooms.MapPost("/", async (
            Guid orgId, Guid siteId,
            [FromBody] CreateRoomRequest request,
            IGrainFactory grainFactory) =>
        {
            var roomId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IRoomGrain>(GrainKeys.Room(orgId, siteId, roomId));
            await grain.CreateAsync(new CreateRoomCommand(
                orgId, siteId, request.RoomTypeId, request.Number, request.Floor,
                request.Name, request.Features, request.IsConnecting, request.ConnectingRoomId));

            var state = await grain.GetStateAsync();
            return Results.Created(
                $"/api/orgs/{orgId}/sites/{siteId}/rooms/{roomId}",
                Hal.Resource(state, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/rooms/{roomId}" },
                    ["roomType"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/room-types/{request.RoomTypeId}" }
                }));
        });

        rooms.MapGet("/{roomId}", async (Guid orgId, Guid siteId, Guid roomId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRoomGrain>(GrainKeys.Room(orgId, siteId, roomId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Room not found"));

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/rooms/{roomId}" },
                ["roomType"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/room-types/{state.RoomTypeId}" }
            }));
        });

        rooms.MapPatch("/{roomId}", async (
            Guid orgId, Guid siteId, Guid roomId,
            [FromBody] UpdateRoomRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRoomGrain>(GrainKeys.Room(orgId, siteId, roomId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Room not found"));

            await grain.UpdateAsync(new UpdateRoomCommand(
                request.Number, request.Name, request.Floor, request.RoomTypeId, request.Features));

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/rooms/{roomId}" }
            }));
        });

        rooms.MapPost("/{roomId}/housekeeping", async (
            Guid orgId, Guid siteId, Guid roomId,
            [FromBody] SetHousekeepingStatusRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRoomGrain>(GrainKeys.Room(orgId, siteId, roomId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Room not found"));

            await grain.SetHousekeepingStatusAsync(request.Status);
            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/rooms/{roomId}" }
            }));
        });

        rooms.MapPost("/{roomId}/out-of-order", async (Guid orgId, Guid siteId, Guid roomId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRoomGrain>(GrainKeys.Room(orgId, siteId, roomId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Room not found"));

            await grain.SetOutOfOrderAsync();
            return Results.Ok();
        });

        rooms.MapPost("/{roomId}/return-to-service", async (Guid orgId, Guid siteId, Guid roomId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRoomGrain>(GrainKeys.Room(orgId, siteId, roomId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Room not found"));

            await grain.ReturnToServiceAsync();
            return Results.Ok();
        });

        return app;
    }
}
