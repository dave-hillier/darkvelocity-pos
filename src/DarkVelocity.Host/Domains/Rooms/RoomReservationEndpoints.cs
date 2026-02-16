using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class RoomReservationEndpoints
{
    public static WebApplication MapRoomReservationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/reservations").WithTags("Room Reservations");

        group.MapPost("/", async (
            Guid orgId, Guid siteId,
            [FromBody] RequestRoomReservationRequest request,
            IGrainFactory grainFactory) =>
        {
            var reservationId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IRoomReservationGrain>(
                GrainKeys.RoomReservation(orgId, siteId, reservationId));

            var result = await grain.RequestAsync(new RequestRoomReservationCommand(
                orgId, siteId, request.RoomTypeId, request.CheckInDate, request.CheckOutDate,
                request.Adults, request.Guest, request.Children, request.RatePlan,
                request.SpecialRequests, request.Source, request.ExternalRef, request.CustomerId));

            var links = new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reservations/{reservationId}" },
                ["confirm"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reservations/{reservationId}/confirm" },
                ["cancel"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/reservations/{reservationId}/cancel" },
                ["roomType"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/room-types/{request.RoomTypeId}" }
            };

            return Results.Created(
                $"/api/orgs/{orgId}/sites/{siteId}/reservations/{reservationId}",
                Hal.Resource(new
                {
                    id = result.Id,
                    confirmationCode = result.ConfirmationCode,
                    createdAt = result.CreatedAt
                }, links));
        });

        group.MapGet("/{reservationId}", async (
            Guid orgId, Guid siteId, Guid reservationId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRoomReservationGrain>(
                GrainKeys.RoomReservation(orgId, siteId, reservationId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Reservation not found"));

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, BuildReservationLinks(orgId, siteId, reservationId, state)));
        });

        group.MapPost("/{reservationId}/confirm", async (
            Guid orgId, Guid siteId, Guid reservationId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRoomReservationGrain>(
                GrainKeys.RoomReservation(orgId, siteId, reservationId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Reservation not found"));

            await grain.ConfirmAsync();
            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, BuildReservationLinks(orgId, siteId, reservationId, state)));
        });

        group.MapPatch("/{reservationId}", async (
            Guid orgId, Guid siteId, Guid reservationId,
            [FromBody] ModifyRoomReservationRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRoomReservationGrain>(
                GrainKeys.RoomReservation(orgId, siteId, reservationId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Reservation not found"));

            await grain.ModifyAsync(new ModifyRoomReservationCommand(
                request.NewCheckInDate, request.NewCheckOutDate, request.NewRoomTypeId,
                request.NewAdults, request.NewChildren, request.SpecialRequests));

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, BuildReservationLinks(orgId, siteId, reservationId, state)));
        });

        group.MapPost("/{reservationId}/cancel", async (
            Guid orgId, Guid siteId, Guid reservationId,
            [FromBody] CancelRoomReservationRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRoomReservationGrain>(
                GrainKeys.RoomReservation(orgId, siteId, reservationId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Reservation not found"));

            await grain.CancelAsync(new CancelRoomReservationCommand(request.Reason, request.CancelledBy));
            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, BuildReservationLinks(orgId, siteId, reservationId, state)));
        });

        group.MapPost("/{reservationId}/check-in", async (
            Guid orgId, Guid siteId, Guid reservationId,
            [FromBody] RoomCheckInRequest? request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRoomReservationGrain>(
                GrainKeys.RoomReservation(orgId, siteId, reservationId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Reservation not found"));

            await grain.CheckInAsync(new CheckInCommand(request?.RoomId, request?.RoomNumber));
            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, BuildReservationLinks(orgId, siteId, reservationId, state)));
        });

        group.MapPost("/{reservationId}/assign-room", async (
            Guid orgId, Guid siteId, Guid reservationId,
            [FromBody] AssignRoomRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRoomReservationGrain>(
                GrainKeys.RoomReservation(orgId, siteId, reservationId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Reservation not found"));

            await grain.AssignRoomAsync(new AssignRoomCommand(request.RoomId, request.RoomNumber));
            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, BuildReservationLinks(orgId, siteId, reservationId, state)));
        });

        group.MapPost("/{reservationId}/check-out", async (
            Guid orgId, Guid siteId, Guid reservationId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRoomReservationGrain>(
                GrainKeys.RoomReservation(orgId, siteId, reservationId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Reservation not found"));

            await grain.CheckOutAsync();
            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, BuildReservationLinks(orgId, siteId, reservationId, state)));
        });

        group.MapPost("/{reservationId}/no-show", async (
            Guid orgId, Guid siteId, Guid reservationId,
            [FromBody] RoomNoShowRequest? request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRoomReservationGrain>(
                GrainKeys.RoomReservation(orgId, siteId, reservationId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Reservation not found"));

            await grain.MarkNoShowAsync(request?.MarkedBy);
            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, BuildReservationLinks(orgId, siteId, reservationId, state)));
        });

        return app;
    }

    private static Dictionary<string, object> BuildReservationLinks(
        Guid orgId, Guid siteId, Guid reservationId, RoomReservationState state)
    {
        var basePath = $"/api/orgs/{orgId}/sites/{siteId}/reservations/{reservationId}";
        var links = new Dictionary<string, object>
        {
            ["self"] = new { href = basePath },
            ["roomType"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/room-types/{state.RoomTypeId}" }
        };

        switch (state.Status)
        {
            case ReservationStatus.Requested:
            case ReservationStatus.PendingDeposit:
                links["confirm"] = new { href = $"{basePath}/confirm" };
                links["cancel"] = new { href = $"{basePath}/cancel" };
                break;
            case ReservationStatus.Confirmed:
                links["check-in"] = new { href = $"{basePath}/check-in" };
                links["assign-room"] = new { href = $"{basePath}/assign-room" };
                links["cancel"] = new { href = $"{basePath}/cancel" };
                links["no-show"] = new { href = $"{basePath}/no-show" };
                break;
            case ReservationStatus.CheckedIn:
            case ReservationStatus.InHouse:
                links["check-out"] = new { href = $"{basePath}/check-out" };
                if (state.AssignedRoomId.HasValue)
                    links["room"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/rooms/{state.AssignedRoomId}" };
                break;
        }

        if (state.CustomerId.HasValue)
            links["customer"] = new { href = $"/api/orgs/{orgId}/customers/{state.CustomerId}" };

        return links;
    }
}
