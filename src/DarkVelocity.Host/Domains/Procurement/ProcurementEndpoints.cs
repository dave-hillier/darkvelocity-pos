using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Hal;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class ProcurementEndpoints
{
    public static WebApplication MapProcurementEndpoints(this WebApplication app)
    {
        // ============================================================================
        // Purchase Order Endpoints
        // ============================================================================
        var poGroup = app.MapGroup("/api/orgs/{orgId}/purchase-orders").WithTags("Purchase Orders");

        poGroup.MapPost("/", async (
            Guid orgId,
            [FromBody] CreatePurchaseOrderRequest request,
            IGrainFactory grainFactory) =>
        {
            var poId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IPurchaseOrderGrain>(GrainKeys.PurchaseOrder(orgId, poId));
            var snapshot = await grain.CreateAsync(new CreatePurchaseOrderCommand(
                request.SupplierId, request.LocationId, request.CreatedByUserId,
                request.ExpectedDeliveryDate, request.Notes));

            return Results.Created(
                $"/api/orgs/{orgId}/purchase-orders/{poId}",
                Hal.Resource(snapshot, BuildPOLinks(orgId, poId)));
        });

        poGroup.MapGet("/{poId}", async (Guid orgId, Guid poId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPurchaseOrderGrain>(GrainKeys.PurchaseOrder(orgId, poId));
            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildPOLinks(orgId, poId)));
        });

        poGroup.MapPost("/{poId}/lines", async (
            Guid orgId, Guid poId,
            [FromBody] AddPOLineRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPurchaseOrderGrain>(GrainKeys.PurchaseOrder(orgId, poId));
            var lineId = Guid.NewGuid();
            await grain.AddLineAsync(new AddPurchaseOrderLineCommand(
                lineId, request.IngredientId, request.IngredientName,
                request.QuantityOrdered, request.UnitPrice, request.Notes));

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildPOLinks(orgId, poId)));
        });

        poGroup.MapPost("/{poId}/submit", async (
            Guid orgId, Guid poId,
            [FromBody] SubmitPORequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPurchaseOrderGrain>(GrainKeys.PurchaseOrder(orgId, poId));
            var snapshot = await grain.SubmitAsync(new SubmitPurchaseOrderCommand(request.SubmittedByUserId));
            return Results.Ok(Hal.Resource(snapshot, BuildPOLinks(orgId, poId)));
        });

        poGroup.MapPost("/{poId}/cancel", async (
            Guid orgId, Guid poId,
            [FromBody] CancelPORequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IPurchaseOrderGrain>(GrainKeys.PurchaseOrder(orgId, poId));
            var snapshot = await grain.CancelAsync(new CancelPurchaseOrderCommand(
                request.Reason, request.CancelledByUserId));
            return Results.Ok(Hal.Resource(snapshot, BuildPOLinks(orgId, poId)));
        });

        // ============================================================================
        // Delivery Endpoints
        // ============================================================================
        var deliveryGroup = app.MapGroup("/api/orgs/{orgId}/deliveries").WithTags("Deliveries");

        deliveryGroup.MapPost("/", async (
            Guid orgId,
            [FromBody] CreateDeliveryRequest request,
            IGrainFactory grainFactory) =>
        {
            var deliveryId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IDeliveryGrain>(GrainKeys.Delivery(orgId, deliveryId));
            var snapshot = await grain.CreateAsync(new CreateDeliveryCommand(
                request.SupplierId, request.PurchaseOrderId, request.LocationId,
                request.ReceivedByUserId, request.SupplierInvoiceNumber, request.Notes));

            return Results.Created(
                $"/api/orgs/{orgId}/deliveries/{deliveryId}",
                Hal.Resource(snapshot, BuildDeliveryLinks(orgId, deliveryId)));
        });

        deliveryGroup.MapGet("/{deliveryId}", async (Guid orgId, Guid deliveryId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeliveryGrain>(GrainKeys.Delivery(orgId, deliveryId));
            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildDeliveryLinks(orgId, deliveryId)));
        });

        deliveryGroup.MapPost("/{deliveryId}/lines", async (
            Guid orgId, Guid deliveryId,
            [FromBody] AddDeliveryLineRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeliveryGrain>(GrainKeys.Delivery(orgId, deliveryId));
            var lineId = Guid.NewGuid();
            await grain.AddLineAsync(new AddDeliveryLineCommand(
                lineId, request.IngredientId, request.IngredientName,
                request.PurchaseOrderLineId, request.QuantityReceived,
                request.UnitCost, request.BatchNumber, request.ExpiryDate, request.Notes));

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildDeliveryLinks(orgId, deliveryId)));
        });

        deliveryGroup.MapPost("/{deliveryId}/accept", async (
            Guid orgId, Guid deliveryId,
            [FromBody] AcceptDeliveryRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeliveryGrain>(GrainKeys.Delivery(orgId, deliveryId));
            var snapshot = await grain.AcceptAsync(new AcceptDeliveryCommand(request.AcceptedByUserId));
            return Results.Ok(Hal.Resource(snapshot, BuildDeliveryLinks(orgId, deliveryId)));
        });

        deliveryGroup.MapPost("/{deliveryId}/reject", async (
            Guid orgId, Guid deliveryId,
            [FromBody] RejectDeliveryRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeliveryGrain>(GrainKeys.Delivery(orgId, deliveryId));
            var snapshot = await grain.RejectAsync(new RejectDeliveryCommand(
                request.Reason, request.RejectedByUserId));
            return Results.Ok(Hal.Resource(snapshot, BuildDeliveryLinks(orgId, deliveryId)));
        });

        // ============================================================================
        // Supplier Endpoints
        // ============================================================================
        var supplierGroup = app.MapGroup("/api/orgs/{orgId}/suppliers").WithTags("Suppliers");

        supplierGroup.MapPost("/", async (
            Guid orgId,
            [FromBody] CreateSupplierCommand command,
            IGrainFactory grainFactory) =>
        {
            var supplierId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<ISupplierGrain>(GrainKeys.Supplier(orgId, supplierId));
            var snapshot = await grain.CreateAsync(command);
            return Results.Created(
                $"/api/orgs/{orgId}/suppliers/{supplierId}",
                Hal.Resource(snapshot, BuildSupplierLinks(orgId, supplierId)));
        });

        supplierGroup.MapGet("/{supplierId}", async (Guid orgId, Guid supplierId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ISupplierGrain>(GrainKeys.Supplier(orgId, supplierId));
            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildSupplierLinks(orgId, supplierId)));
        });

        supplierGroup.MapPut("/{supplierId}", async (
            Guid orgId, Guid supplierId,
            [FromBody] UpdateSupplierCommand command,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ISupplierGrain>(GrainKeys.Supplier(orgId, supplierId));
            var snapshot = await grain.UpdateAsync(command);
            return Results.Ok(Hal.Resource(snapshot, BuildSupplierLinks(orgId, supplierId)));
        });

        return app;
    }

    private static Dictionary<string, object> BuildPOLinks(Guid orgId, Guid poId)
    {
        var basePath = $"/api/orgs/{orgId}/purchase-orders/{poId}";
        return new Dictionary<string, object>
        {
            ["self"] = new HalLink(basePath),
            ["lines"] = new HalLink($"{basePath}/lines", Title: "Add line items"),
            ["submit"] = new HalLink($"{basePath}/submit", Title: "Submit for approval"),
            ["cancel"] = new HalLink($"{basePath}/cancel", Title: "Cancel order")
        };
    }

    private static Dictionary<string, object> BuildDeliveryLinks(Guid orgId, Guid deliveryId)
    {
        var basePath = $"/api/orgs/{orgId}/deliveries/{deliveryId}";
        return new Dictionary<string, object>
        {
            ["self"] = new HalLink(basePath),
            ["lines"] = new HalLink($"{basePath}/lines", Title: "Add delivery lines"),
            ["accept"] = new HalLink($"{basePath}/accept", Title: "Accept delivery"),
            ["reject"] = new HalLink($"{basePath}/reject", Title: "Reject delivery")
        };
    }

    private static Dictionary<string, object> BuildSupplierLinks(Guid orgId, Guid supplierId)
    {
        var basePath = $"/api/orgs/{orgId}/suppliers/{supplierId}";
        return new Dictionary<string, object>
        {
            ["self"] = new HalLink(basePath),
            ["ingredients"] = new HalLink($"{basePath}/ingredients", Title: "Supplier ingredients")
        };
    }
}

// Request DTOs
public record CreatePurchaseOrderRequest(
    Guid SupplierId, Guid LocationId, DateTime ExpectedDeliveryDate,
    Guid? CreatedByUserId = null, string? Notes = null);

public record AddPOLineRequest(
    Guid IngredientId, string IngredientName,
    decimal QuantityOrdered, decimal UnitPrice, string? Notes = null);

public record SubmitPORequest(Guid SubmittedByUserId);

public record CancelPORequest(string Reason, Guid CancelledByUserId);

public record CreateDeliveryRequest(
    Guid SupplierId, Guid LocationId,
    Guid? PurchaseOrderId = null, Guid? ReceivedByUserId = null,
    string? SupplierInvoiceNumber = null, string? Notes = null);

public record AddDeliveryLineRequest(
    Guid IngredientId, string IngredientName,
    decimal QuantityReceived, decimal UnitCost,
    Guid? PurchaseOrderLineId = null, string? BatchNumber = null,
    DateTime? ExpiryDate = null, string? Notes = null);

public record AcceptDeliveryRequest(Guid AcceptedByUserId);

public record RejectDeliveryRequest(string Reason, Guid RejectedByUserId);
