using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class RoomAvailabilityEndpoints
{
    public static WebApplication MapRoomAvailabilityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/room-availability").WithTags("Room Availability");

        // Check availability for a room type across a date range
        group.MapGet("/", async (
            Guid orgId, Guid siteId,
            [FromQuery] Guid roomTypeId,
            [FromQuery] DateOnly checkIn,
            [FromQuery] DateOnly checkOut,
            [FromQuery] int adults = 1,
            [FromQuery] int children = 0,
            IGrainFactory grainFactory = default!) =>
        {
            if (checkOut <= checkIn)
                return Results.BadRequest(Hal.Error("invalid_dates", "Check-out must be after check-in"));

            // Validate room type exists
            var roomTypeGrain = grainFactory.GetGrain<IRoomTypeGrain>(GrainKeys.RoomType(orgId, siteId, roomTypeId));
            if (!await roomTypeGrain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Room type not found"));

            var roomType = await roomTypeGrain.GetStateAsync();

            // Validate occupancy
            if (adults + children > roomType.MaxOccupancy)
                return Results.BadRequest(Hal.Error("exceeds_occupancy",
                    $"Total guests ({adults + children}) exceeds max occupancy ({roomType.MaxOccupancy})"));

            if (adults > roomType.MaxAdults)
                return Results.BadRequest(Hal.Error("exceeds_adults",
                    $"Adults ({adults}) exceeds max adults ({roomType.MaxAdults})"));

            // Validate settings
            var settingsGrain = grainFactory.GetGrain<IRoomReservationSettingsGrain>(
                GrainKeys.RoomReservationSettings(orgId, siteId));
            if (!await settingsGrain.ExistsAsync())
                await settingsGrain.InitializeAsync(orgId, siteId);

            var settings = await settingsGrain.GetStateAsync();
            var stayNights = checkOut.DayNumber - checkIn.DayNumber;

            if (stayNights < settings.MinStayNights)
                return Results.BadRequest(Hal.Error("min_stay",
                    $"Minimum stay is {settings.MinStayNights} night(s)"));

            if (stayNights > settings.MaxStayNights)
                return Results.BadRequest(Hal.Error("max_stay",
                    $"Maximum stay is {settings.MaxStayNights} night(s)"));

            // Check closed-to-arrival
            if (settings.ClosedToArrivalDates.Contains(checkIn))
                return Results.Ok(new
                {
                    roomTypeId,
                    checkIn = checkIn.ToString("yyyy-MM-dd"),
                    checkOut = checkOut.ToString("yyyy-MM-dd"),
                    isAvailable = false,
                    reason = "closed_to_arrival",
                    nights = Array.Empty<object>()
                });

            // Check closed-to-departure
            if (settings.ClosedToDepartureDates.Contains(checkOut))
                return Results.Ok(new
                {
                    roomTypeId,
                    checkIn = checkIn.ToString("yyyy-MM-dd"),
                    checkOut = checkOut.ToString("yyyy-MM-dd"),
                    isAvailable = false,
                    reason = "closed_to_departure",
                    nights = Array.Empty<object>()
                });

            // Check each night in the range
            var nights = new List<object>();
            var allAvailable = true;
            var minAvailable = int.MaxValue;

            for (var date = checkIn; date < checkOut; date = date.AddDays(1))
            {
                var inventoryGrain = grainFactory.GetGrain<IRoomInventoryGrain>(
                    GrainKeys.RoomInventory(orgId, siteId, roomTypeId, date));

                int available;
                if (await inventoryGrain.ExistsAsync())
                {
                    var availability = await inventoryGrain.GetAvailabilityAsync();
                    available = availability.Available;
                }
                else
                {
                    // No inventory initialized for this date — full availability
                    available = roomType.TotalRooms;
                }

                if (available <= 0)
                    allAvailable = false;

                if (available < minAvailable)
                    minAvailable = available;

                nights.Add(new
                {
                    date = date.ToString("yyyy-MM-dd"),
                    isAvailable = available > 0,
                    availableRooms = available,
                    rate = roomType.RackRate
                });
            }

            return Results.Ok(new
            {
                roomTypeId,
                checkIn = checkIn.ToString("yyyy-MM-dd"),
                checkOut = checkOut.ToString("yyyy-MM-dd"),
                adults,
                children,
                isAvailable = allAvailable,
                availableRooms = minAvailable == int.MaxValue ? 0 : minAvailable,
                totalRate = roomType.RackRate * stayNights,
                nights,
                _links = new
                {
                    self = new { href = $"/api/orgs/{orgId}/sites/{siteId}/room-availability?roomTypeId={roomTypeId}&checkIn={checkIn:yyyy-MM-dd}&checkOut={checkOut:yyyy-MM-dd}&adults={adults}&children={children}" },
                    roomType = new { href = $"/api/orgs/{orgId}/sites/{siteId}/room-types/{roomTypeId}" },
                    book = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reservations" }
                }
            });
        });

        // Quick check: is a specific room type available for a date range?
        group.MapGet("/check", async (
            Guid orgId, Guid siteId,
            [FromQuery] Guid roomTypeId,
            [FromQuery] DateOnly checkIn,
            [FromQuery] DateOnly checkOut,
            IGrainFactory grainFactory) =>
        {
            if (checkOut <= checkIn)
                return Results.BadRequest(Hal.Error("invalid_dates", "Check-out must be after check-in"));

            var roomTypeGrain = grainFactory.GetGrain<IRoomTypeGrain>(GrainKeys.RoomType(orgId, siteId, roomTypeId));
            if (!await roomTypeGrain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Room type not found"));

            var roomType = await roomTypeGrain.GetStateAsync();

            for (var date = checkIn; date < checkOut; date = date.AddDays(1))
            {
                var inventoryGrain = grainFactory.GetGrain<IRoomInventoryGrain>(
                    GrainKeys.RoomInventory(orgId, siteId, roomTypeId, date));

                if (await inventoryGrain.ExistsAsync())
                {
                    var availability = await inventoryGrain.GetAvailabilityAsync();
                    if (availability.Available <= 0)
                    {
                        return Results.Ok(new
                        {
                            roomTypeId,
                            checkIn = checkIn.ToString("yyyy-MM-dd"),
                            checkOut = checkOut.ToString("yyyy-MM-dd"),
                            isAvailable = false
                        });
                    }
                }
            }

            return Results.Ok(new
            {
                roomTypeId,
                checkIn = checkIn.ToString("yyyy-MM-dd"),
                checkOut = checkOut.ToString("yyyy-MM-dd"),
                isAvailable = true
            });
        });

        // Reservation settings
        app.MapGet("/api/orgs/{orgId}/sites/{siteId}/room-reservation-settings", async (
            Guid orgId, Guid siteId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRoomReservationSettingsGrain>(
                GrainKeys.RoomReservationSettings(orgId, siteId));
            if (!await grain.ExistsAsync())
                await grain.InitializeAsync(orgId, siteId);

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/room-reservation-settings" },
                ["availability"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/room-availability" }
            }));
        }).WithTags("Room Availability");

        app.MapPatch("/api/orgs/{orgId}/sites/{siteId}/room-reservation-settings", async (
            Guid orgId, Guid siteId,
            [FromBody] UpdateRoomReservationSettingsRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRoomReservationSettingsGrain>(
                GrainKeys.RoomReservationSettings(orgId, siteId));
            if (!await grain.ExistsAsync())
                await grain.InitializeAsync(orgId, siteId);

            await grain.UpdateAsync(new UpdateRoomReservationSettingsCommand(
                request.DefaultCheckInTime, request.DefaultCheckOutTime,
                request.AdvanceBookingDays, request.MinStayNights, request.MaxStayNights,
                request.OverbookingPercent, request.RequireDeposit, request.DepositAmount,
                request.FreeCancellationWindow, request.AllowChildren, request.ChildMaxAge));

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/room-reservation-settings" }
            }));
        }).WithTags("Room Availability");

        return app;
    }
}
