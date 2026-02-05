using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Hal;
using DarkVelocity.Host.State;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class InventoryEndpoints
{
    public static WebApplication MapInventoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/inventory").WithTags("Inventory");

        group.MapPost("/", async (
            Guid orgId,
            Guid siteId,
            [FromBody] InitializeInventoryRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, request.IngredientId));
            await grain.InitializeAsync(new InitializeInventoryCommand(
                orgId, siteId, request.IngredientId, request.IngredientName, request.Sku, request.Unit, request.Category, request.ReorderPoint, request.ParLevel));

            var createdState = await grain.GetStateAsync();
            return Results.Created(
                $"/api/orgs/{orgId}/sites/{siteId}/inventory/{request.IngredientId}",
                Hal.Resource(new
                {
                    ingredientId = request.IngredientId,
                    ingredientName = request.IngredientName
                }, BuildInventoryLinks(orgId, siteId, request.IngredientId, createdState)));
        });

        group.MapGet("/{ingredientId}", async (Guid orgId, Guid siteId, Guid ingredientId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Inventory item not found"));

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, BuildInventoryLinks(orgId, siteId, ingredientId, state)));
        });

        group.MapPost("/{ingredientId}/receive", async (
            Guid orgId, Guid siteId, Guid ingredientId,
            [FromBody] ReceiveBatchRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Inventory item not found"));

            var result = await grain.ReceiveBatchAsync(new ReceiveBatchCommand(
                request.BatchNumber, request.Quantity, request.UnitCost, request.ExpiryDate, request.SupplierId, request.DeliveryId, request.Location, request.Notes, request.ReceivedBy));

            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["inventory"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}" }
            }));
        });

        group.MapPost("/{ingredientId}/consume", async (
            Guid orgId, Guid siteId, Guid ingredientId,
            [FromBody] ConsumeStockRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Inventory item not found"));

            var result = await grain.ConsumeAsync(new ConsumeStockCommand(request.Quantity, request.Reason, request.OrderId, request.PerformedBy));
            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["inventory"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}" }
            }));
        });

        group.MapPost("/{ingredientId}/adjust", async (
            Guid orgId, Guid siteId, Guid ingredientId,
            [FromBody] AdjustInventoryRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Inventory item not found"));

            await grain.AdjustQuantityAsync(new AdjustQuantityCommand(request.NewQuantity, request.Reason, request.AdjustedBy, request.ApprovedBy));
            var level = await grain.GetLevelInfoAsync();

            return Results.Ok(Hal.Resource(level, new Dictionary<string, object>
            {
                ["inventory"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}" }
            }));
        });

        group.MapGet("/{ingredientId}/level", async (Guid orgId, Guid siteId, Guid ingredientId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Inventory item not found"));

            var level = await grain.GetLevelInfoAsync();
            return Results.Ok(Hal.Resource(level, new Dictionary<string, object>
            {
                ["inventory"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}" }
            }));
        });

        return app;
    }

    /// <summary>
    /// Builds HAL links for an inventory resource with cross-domain relationships.
    /// </summary>
    private static Dictionary<string, object> BuildInventoryLinks(
        Guid orgId,
        Guid siteId,
        Guid ingredientId,
        InventoryState? state = null)
    {
        var basePath = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}";

        var links = new Dictionary<string, object>
        {
            // Core resource links
            ["self"] = new HalLink(basePath),
            ["site"] = new HalLink($"/api/orgs/{orgId}/sites/{siteId}"),

            // Action links for inventory operations
            ["receive"] = new HalLink($"{basePath}/receive", Title: "Receive stock into inventory"),
            ["consume"] = new HalLink($"{basePath}/consume", Title: "Consume stock from inventory"),
            ["adjust"] = new HalLink($"{basePath}/adjust", Title: "Adjust inventory quantity"),

            // Cross-domain links (templated where appropriate)
            ["ingredient"] = new HalLink(
                $"/api/orgs/{orgId}/ingredients/{ingredientId}",
                Title: "Organization-level ingredient definition"),
            ["recipe-usages"] = new HalLink(
                $"/api/orgs/{orgId}/inventory/{ingredientId}/recipes",
                Title: "Recipes using this ingredient"),
            ["purchase-orders"] = new HalLink(
                $"/api/orgs/{orgId}/sites/{siteId}/purchases{{?ingredientId}}",
                Title: "Purchase orders containing this ingredient",
                Templated: true)
        };

        // Add supplier links if any batch has a supplier
        if (state?.Batches != null)
        {
            var supplierIds = state.Batches
                .Where(b => b.SupplierId.HasValue)
                .Select(b => b.SupplierId!.Value)
                .Distinct()
                .ToList();

            if (supplierIds.Count == 1)
            {
                // Single supplier - add direct link
                links["supplier"] = new HalLink(
                    $"/api/orgs/{orgId}/suppliers/{supplierIds[0]}",
                    Title: "Supplier for this inventory item");
            }
            else if (supplierIds.Count > 1)
            {
                // Multiple suppliers - add templated link and list individual suppliers
                links["suppliers"] = new HalLink(
                    $"/api/orgs/{orgId}/suppliers{{?ids}}",
                    Title: "Suppliers for this inventory item",
                    Templated: true);

                // Add individual supplier links with relation names
                for (int i = 0; i < supplierIds.Count; i++)
                {
                    links[$"supplier:{supplierIds[i]}"] = new HalLink(
                        $"/api/orgs/{orgId}/suppliers/{supplierIds[i]}",
                        Title: $"Supplier {i + 1}");
                }
            }
        }

        return links;
    }
}
