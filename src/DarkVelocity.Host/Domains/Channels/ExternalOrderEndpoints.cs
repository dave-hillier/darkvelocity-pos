using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Adapters;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

/// <summary>
/// API endpoints for managing external orders from delivery platforms.
/// </summary>
public static class ExternalOrderEndpoints
{
    /// <summary>
    /// Builds HAL links for an external order resource with cross-domain relationships.
    /// </summary>
    private static Dictionary<string, object> BuildExternalOrderLinks(
        Guid orgId,
        ExternalOrderSnapshot snapshot,
        bool includeChannelPath = false)
    {
        var externalOrderId = snapshot.ExternalOrderId;
        var channelId = snapshot.DeliveryPlatformId;
        var locationId = snapshot.LocationId;

        // Self link - support both URL patterns
        var selfPath = includeChannelPath
            ? $"/api/orgs/{orgId}/channels/{channelId}/external-orders/{externalOrderId}"
            : $"/api/orgs/{orgId}/external-orders/{externalOrderId}";

        var links = new Dictionary<string, object>
        {
            ["self"] = new { href = selfPath },
            ["channel"] = new { href = $"/api/orgs/{orgId}/channels/{channelId}" },
            ["site"] = new { href = $"/api/orgs/{orgId}/sites/{locationId}" },
            ["organization"] = new { href = $"/api/orgs/{orgId}" }
        };

        // Add internal-order link if linked to a POS order
        if (snapshot.InternalOrderId.HasValue)
        {
            links["internal-order"] = new { href = $"/api/orgs/{orgId}/sites/{locationId}/orders/{snapshot.InternalOrderId}" };
        }

        // Add action links based on current status
        var basePath = $"/api/orgs/{orgId}/external-orders/{externalOrderId}";

        switch (snapshot.Status)
        {
            case ExternalOrderStatus.Pending:
                links["accept"] = new { href = $"{basePath}/accept" };
                links["reject"] = new { href = $"{basePath}/reject" };
                break;
            case ExternalOrderStatus.Accepted:
                links["preparing"] = new { href = $"{basePath}/status", method = "POST", body = new { status = "Preparing" } };
                links["reject"] = new { href = $"{basePath}/reject" };
                break;
            case ExternalOrderStatus.Preparing:
                links["ready"] = new { href = $"{basePath}/status", method = "POST", body = new { status = "Ready" } };
                break;
            case ExternalOrderStatus.Ready:
                links["picked-up"] = new { href = $"{basePath}/status", method = "POST", body = new { status = "PickedUp" } };
                break;
            case ExternalOrderStatus.PickedUp:
                links["delivered"] = new { href = $"{basePath}/status", method = "POST", body = new { status = "Delivered" } };
                break;
        }

        // Add link action if not yet linked to internal order
        if (!snapshot.InternalOrderId.HasValue && snapshot.Status != ExternalOrderStatus.Rejected &&
            snapshot.Status != ExternalOrderStatus.Cancelled && snapshot.Status != ExternalOrderStatus.Failed)
        {
            links["link"] = new { href = $"{basePath}/link" };
        }

        return links;
    }

    /// <summary>
    /// Builds a full external order HAL response with all links.
    /// </summary>
    private static object BuildExternalOrderResponse(
        Guid orgId,
        ExternalOrderSnapshot snapshot,
        bool includeChannelPath = false)
    {
        var links = BuildExternalOrderLinks(orgId, snapshot, includeChannelPath);
        return Hal.Resource(snapshot, links);
    }

    public static WebApplication MapExternalOrderEndpoints(this WebApplication app)
    {
        var ordersGroup = app.MapGroup("/api/orgs/{orgId}/external-orders").WithTags("External Orders");
        var channelOrdersGroup = app.MapGroup("/api/orgs/{orgId}/channels/{channelId}/orders").WithTags("Channel Orders");

        // ============================================================================
        // External Order Endpoints
        // ============================================================================

        ordersGroup.MapGet("/", async (
            Guid orgId,
            [FromQuery] ExternalOrderStatus? status,
            [FromQuery] DateTime? since,
            [FromQuery] int limit,
            IGrainFactory grainFactory) =>
        {
            // Note: In a real implementation, this would query an index grain
            // For now, return a placeholder
            return Results.Ok(Hal.Collection(
                $"/api/orgs/{orgId}/external-orders",
                new List<object>(),
                0));
        })
        .WithName("ListExternalOrders")
        .WithSummary("List external orders with optional filters");

        ordersGroup.MapGet("/{externalOrderId}", async (
            Guid orgId,
            Guid externalOrderId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IExternalOrderGrain>(GrainKeys.ExternalOrder(orgId, externalOrderId));

            try
            {
                var snapshot = await grain.GetSnapshotAsync();
                return Results.Ok(BuildExternalOrderResponse(orgId, snapshot));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", "External order not found"));
            }
        })
        .WithName("GetExternalOrder")
        .WithSummary("Get external order details");

        ordersGroup.MapPost("/{externalOrderId}/accept", async (
            Guid orgId,
            Guid externalOrderId,
            [FromBody] AcceptExternalOrderRequest request,
            IGrainFactory grainFactory,
            [FromServices] IPlatformAdapterFactory adapterFactory) =>
        {
            var grain = grainFactory.GetGrain<IExternalOrderGrain>(GrainKeys.ExternalOrder(orgId, externalOrderId));

            try
            {
                var snapshot = await grain.AcceptAsync(request.EstimatedPickupAt);

                // Notify the platform
                if (!string.IsNullOrEmpty(snapshot.PlatformOrderId))
                {
                    try
                    {
                        var channelGrain = grainFactory.GetGrain<IChannelGrain>(
                            GrainKeys.Channel(orgId, snapshot.DeliveryPlatformId));
                        var channelSnapshot = await channelGrain.GetSnapshotAsync();

                        var adapter = adapterFactory.GetAdapter(channelSnapshot.PlatformType);
                        await adapter.AcceptOrderAsync(snapshot.PlatformOrderId, request.EstimatedPickupAt);
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail the acceptance
                        Console.WriteLine($"Failed to notify platform of acceptance: {ex.Message}");
                    }
                }

                return Results.Ok(BuildExternalOrderResponse(orgId, snapshot));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_state", ex.Message));
            }
        })
        .WithName("AcceptExternalOrder")
        .WithSummary("Accept an external order");

        ordersGroup.MapPost("/{externalOrderId}/reject", async (
            Guid orgId,
            Guid externalOrderId,
            [FromBody] RejectExternalOrderRequest request,
            IGrainFactory grainFactory,
            [FromServices] IPlatformAdapterFactory adapterFactory) =>
        {
            var grain = grainFactory.GetGrain<IExternalOrderGrain>(GrainKeys.ExternalOrder(orgId, externalOrderId));

            try
            {
                var snapshot = await grain.RejectAsync(request.Reason);

                // Notify the platform
                if (!string.IsNullOrEmpty(snapshot.PlatformOrderId))
                {
                    try
                    {
                        var channelGrain = grainFactory.GetGrain<IChannelGrain>(
                            GrainKeys.Channel(orgId, snapshot.DeliveryPlatformId));
                        var channelSnapshot = await channelGrain.GetSnapshotAsync();

                        var adapter = adapterFactory.GetAdapter(channelSnapshot.PlatformType);
                        await adapter.RejectOrderAsync(snapshot.PlatformOrderId, request.Reason);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to notify platform of rejection: {ex.Message}");
                    }
                }

                return Results.Ok(BuildExternalOrderResponse(orgId, snapshot));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_state", ex.Message));
            }
        })
        .WithName("RejectExternalOrder")
        .WithSummary("Reject an external order");

        ordersGroup.MapPost("/{externalOrderId}/status", async (
            Guid orgId,
            Guid externalOrderId,
            [FromBody] UpdateExternalOrderStatusRequest request,
            IGrainFactory grainFactory,
            [FromServices] IPlatformAdapterFactory adapterFactory) =>
        {
            var grain = grainFactory.GetGrain<IExternalOrderGrain>(GrainKeys.ExternalOrder(orgId, externalOrderId));

            try
            {
                // Update internal status
                switch (request.Status)
                {
                    case ExternalOrderStatus.Preparing:
                        await grain.SetPreparingAsync();
                        break;
                    case ExternalOrderStatus.Ready:
                        await grain.SetReadyAsync();
                        break;
                    case ExternalOrderStatus.PickedUp:
                        await grain.SetPickedUpAsync();
                        break;
                    case ExternalOrderStatus.Delivered:
                        await grain.SetDeliveredAsync();
                        break;
                    case ExternalOrderStatus.Cancelled:
                        await grain.CancelAsync(request.Reason ?? "Cancelled by restaurant");
                        break;
                    default:
                        return Results.BadRequest(Hal.Error("invalid_status", $"Cannot update to status: {request.Status}"));
                }

                var snapshot = await grain.GetSnapshotAsync();

                // Notify the platform
                if (!string.IsNullOrEmpty(snapshot.PlatformOrderId))
                {
                    try
                    {
                        var channelGrain = grainFactory.GetGrain<IChannelGrain>(
                            GrainKeys.Channel(orgId, snapshot.DeliveryPlatformId));
                        var channelSnapshot = await channelGrain.GetSnapshotAsync();

                        var adapter = adapterFactory.GetAdapter(channelSnapshot.PlatformType);
                        await adapter.UpdateStatusAsync(snapshot.PlatformOrderId, request.Status);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to notify platform of status update: {ex.Message}");
                    }
                }

                return Results.Ok(BuildExternalOrderResponse(orgId, snapshot));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_state", ex.Message));
            }
        })
        .WithName("UpdateExternalOrderStatus")
        .WithSummary("Update external order status");

        ordersGroup.MapPost("/{externalOrderId}/link", async (
            Guid orgId,
            Guid externalOrderId,
            [FromBody] LinkInternalOrderRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IExternalOrderGrain>(GrainKeys.ExternalOrder(orgId, externalOrderId));

            try
            {
                await grain.LinkInternalOrderAsync(request.InternalOrderId);
                var snapshot = await grain.GetSnapshotAsync();

                return Results.Ok(BuildExternalOrderResponse(orgId, snapshot));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_state", ex.Message));
            }
        })
        .WithName("LinkInternalOrder")
        .WithSummary("Link external order to internal order");

        // ============================================================================
        // Channel Orders Endpoints
        // ============================================================================

        channelOrdersGroup.MapGet("/", async (
            Guid orgId,
            Guid channelId,
            [FromQuery] ExternalOrderStatus? status,
            [FromQuery] DateTime? since,
            [FromQuery] int limit,
            IGrainFactory grainFactory) =>
        {
            // Note: In a real implementation, this would query an index grain
            return Results.Ok(Hal.Collection(
                $"/api/orgs/{orgId}/channels/{channelId}/orders",
                new List<object>(),
                0));
        })
        .WithName("ListChannelOrders")
        .WithSummary("List orders from a specific channel");

        return app;
    }
}

// ============================================================================
// Request DTOs
// ============================================================================

public record AcceptExternalOrderRequest(DateTime? EstimatedPickupAt);

public record RejectExternalOrderRequest(string Reason);

public record UpdateExternalOrderStatusRequest(ExternalOrderStatus Status, string? Reason);

public record LinkInternalOrderRequest(Guid InternalOrderId);
