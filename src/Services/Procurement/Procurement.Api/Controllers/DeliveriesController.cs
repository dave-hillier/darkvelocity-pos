using DarkVelocity.Procurement.Api.Data;
using DarkVelocity.Procurement.Api.Dtos;
using DarkVelocity.Procurement.Api.Entities;
using DarkVelocity.Procurement.Api.Services;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Procurement.Api.Controllers;

[ApiController]
[Route("api/deliveries")]
public class DeliveriesController : ControllerBase
{
    private readonly ProcurementDbContext _context;
    private readonly IDeliveryNumberGenerator _deliveryNumberGenerator;

    public DeliveriesController(
        ProcurementDbContext context,
        IDeliveryNumberGenerator deliveryNumberGenerator)
    {
        _context = context;
        _deliveryNumberGenerator = deliveryNumberGenerator;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<DeliveryDto>>> GetAll(
        [FromQuery] Guid? locationId = null,
        [FromQuery] Guid? supplierId = null,
        [FromQuery] string? status = null)
    {
        var query = _context.Deliveries
            .Include(d => d.Supplier)
            .Include(d => d.PurchaseOrder)
            .Include(d => d.Lines)
            .Include(d => d.Discrepancies)
            .AsQueryable();

        if (locationId.HasValue)
            query = query.Where(d => d.LocationId == locationId.Value);
        if (supplierId.HasValue)
            query = query.Where(d => d.SupplierId == supplierId.Value);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(d => d.Status == status);

        var deliveries = await query
            .OrderByDescending(d => d.CreatedAt)
            .Take(100)
            .Select(d => MapToDto(d))
            .ToListAsync();

        foreach (var delivery in deliveries)
        {
            delivery.AddSelfLink($"/api/deliveries/{delivery.Id}");
        }

        return Ok(HalCollection<DeliveryDto>.Create(
            deliveries,
            "/api/deliveries",
            deliveries.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DeliveryDto>> GetById(Guid id)
    {
        var delivery = await _context.Deliveries
            .Include(d => d.Supplier)
            .Include(d => d.PurchaseOrder)
            .Include(d => d.Lines)
            .Include(d => d.Discrepancies)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (delivery == null)
            return NotFound();

        var dto = MapToDto(delivery);
        dto.AddSelfLink($"/api/deliveries/{delivery.Id}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<DeliveryDto>> Create([FromBody] CreateDeliveryRequest request)
    {
        var supplier = await _context.Suppliers.FindAsync(request.SupplierId);
        if (supplier == null)
            return BadRequest(new { message = "Invalid supplier" });

        PurchaseOrder? purchaseOrder = null;
        if (request.PurchaseOrderId.HasValue)
        {
            purchaseOrder = await _context.PurchaseOrders.FindAsync(request.PurchaseOrderId.Value);
            if (purchaseOrder == null)
                return BadRequest(new { message = "Invalid purchase order" });
            if (purchaseOrder.SupplierId != request.SupplierId)
                return BadRequest(new { message = "Purchase order is for a different supplier" });
        }

        var deliveryNumber = await _deliveryNumberGenerator.GenerateAsync(request.LocationId);

        var delivery = new Delivery
        {
            DeliveryNumber = deliveryNumber,
            SupplierId = request.SupplierId,
            PurchaseOrderId = request.PurchaseOrderId,
            LocationId = request.LocationId,
            ReceivedByUserId = request.ReceivedByUserId,
            ReceivedAt = DateTime.UtcNow,
            SupplierInvoiceNumber = request.SupplierInvoiceNumber,
            Notes = request.Notes
        };

        _context.Deliveries.Add(delivery);
        await _context.SaveChangesAsync();

        // Reload with navigation
        await _context.Entry(delivery).Reference(d => d.Supplier).LoadAsync();
        if (delivery.PurchaseOrderId.HasValue)
        {
            await _context.Entry(delivery).Reference(d => d.PurchaseOrder).LoadAsync();
        }

        var dto = MapToDto(delivery);
        dto.AddSelfLink($"/api/deliveries/{delivery.Id}");

        return CreatedAtAction(nameof(GetById), new { id = delivery.Id }, dto);
    }

    [HttpPost("{id:guid}/lines")]
    public async Task<ActionResult<DeliveryLineDto>> AddLine(Guid id, [FromBody] AddDeliveryLineRequest request)
    {
        var delivery = await _context.Deliveries
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (delivery == null)
            return NotFound();

        if (delivery.Status != "pending")
            return BadRequest(new { message = "Cannot add lines to an accepted or rejected delivery" });

        var line = new DeliveryLine
        {
            DeliveryId = id,
            IngredientId = request.IngredientId,
            IngredientName = request.IngredientName,
            PurchaseOrderLineId = request.PurchaseOrderLineId,
            QuantityReceived = request.QuantityReceived,
            UnitCost = request.UnitCost,
            LineTotal = request.QuantityReceived * request.UnitCost,
            BatchNumber = request.BatchNumber,
            ExpiryDate = request.ExpiryDate,
            Notes = request.Notes
        };

        _context.DeliveryLines.Add(line);
        await _context.SaveChangesAsync();

        // Reload lines for recalculation
        await _context.Entry(delivery).Collection(d => d.Lines).LoadAsync();
        delivery.TotalValue = delivery.Lines.Sum(l => l.LineTotal);
        await _context.SaveChangesAsync();

        var dto = new DeliveryLineDto
        {
            Id = line.Id,
            DeliveryId = line.DeliveryId,
            IngredientId = line.IngredientId,
            IngredientName = line.IngredientName,
            PurchaseOrderLineId = line.PurchaseOrderLineId,
            QuantityReceived = line.QuantityReceived,
            UnitCost = line.UnitCost,
            LineTotal = line.LineTotal,
            BatchNumber = line.BatchNumber,
            ExpiryDate = line.ExpiryDate,
            Notes = line.Notes
        };

        dto.AddSelfLink($"/api/deliveries/{id}/lines/{line.Id}");

        return Created($"/api/deliveries/{id}/lines/{line.Id}", dto);
    }

    [HttpDelete("{id:guid}/lines/{lineId:guid}")]
    public async Task<IActionResult> RemoveLine(Guid id, Guid lineId)
    {
        var delivery = await _context.Deliveries
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (delivery == null)
            return NotFound();

        if (delivery.Status != "pending")
            return BadRequest(new { message = "Cannot remove lines from an accepted or rejected delivery" });

        var line = delivery.Lines.FirstOrDefault(l => l.Id == lineId);
        if (line == null)
            return NotFound();

        _context.DeliveryLines.Remove(line);

        // Recalculate delivery total
        delivery.TotalValue = delivery.Lines.Where(l => l.Id != lineId).Sum(l => l.LineTotal);

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:guid}/discrepancies")]
    public async Task<ActionResult<DeliveryDiscrepancyDto>> AddDiscrepancy(
        Guid id,
        [FromBody] AddDeliveryDiscrepancyRequest request)
    {
        var delivery = await _context.Deliveries
            .Include(d => d.Lines)
            .Include(d => d.Discrepancies)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (delivery == null)
            return NotFound();

        if (delivery.Status != "pending")
            return BadRequest(new { message = "Cannot add discrepancies to an accepted or rejected delivery" });

        var line = delivery.Lines.FirstOrDefault(l => l.Id == request.DeliveryLineId);
        if (line == null)
            return BadRequest(new { message = "Invalid delivery line" });

        var discrepancy = new DeliveryDiscrepancy
        {
            DeliveryId = id,
            DeliveryLineId = request.DeliveryLineId,
            DiscrepancyType = request.DiscrepancyType,
            QuantityAffected = request.QuantityAffected,
            PriceDifference = request.PriceDifference,
            Description = request.Description
        };

        _context.DeliveryDiscrepancies.Add(discrepancy);
        delivery.HasDiscrepancies = true;

        await _context.SaveChangesAsync();

        var dto = new DeliveryDiscrepancyDto
        {
            Id = discrepancy.Id,
            DeliveryId = discrepancy.DeliveryId,
            DeliveryLineId = discrepancy.DeliveryLineId,
            DiscrepancyType = discrepancy.DiscrepancyType,
            QuantityAffected = discrepancy.QuantityAffected,
            PriceDifference = discrepancy.PriceDifference,
            Description = discrepancy.Description,
            ActionTaken = discrepancy.ActionTaken,
            ResolvedAt = discrepancy.ResolvedAt,
            ResolutionNotes = discrepancy.ResolutionNotes
        };

        dto.AddSelfLink($"/api/deliveries/{id}/discrepancies/{discrepancy.Id}");

        return Created($"/api/deliveries/{id}/discrepancies/{discrepancy.Id}", dto);
    }

    [HttpPost("{id:guid}/discrepancies/{discrepancyId:guid}/resolve")]
    public async Task<ActionResult<DeliveryDiscrepancyDto>> ResolveDiscrepancy(
        Guid id,
        Guid discrepancyId,
        [FromBody] ResolveDiscrepancyRequest request)
    {
        var discrepancy = await _context.DeliveryDiscrepancies
            .FirstOrDefaultAsync(d => d.DeliveryId == id && d.Id == discrepancyId);

        if (discrepancy == null)
            return NotFound();

        if (discrepancy.ActionTaken != "pending")
            return BadRequest(new { message = "Discrepancy already resolved" });

        discrepancy.ActionTaken = request.ActionTaken;
        discrepancy.ResolvedByUserId = request.ResolvedByUserId;
        discrepancy.ResolvedAt = DateTime.UtcNow;
        discrepancy.ResolutionNotes = request.ResolutionNotes;

        await _context.SaveChangesAsync();

        var dto = new DeliveryDiscrepancyDto
        {
            Id = discrepancy.Id,
            DeliveryId = discrepancy.DeliveryId,
            DeliveryLineId = discrepancy.DeliveryLineId,
            DiscrepancyType = discrepancy.DiscrepancyType,
            QuantityAffected = discrepancy.QuantityAffected,
            PriceDifference = discrepancy.PriceDifference,
            Description = discrepancy.Description,
            ActionTaken = discrepancy.ActionTaken,
            ResolvedAt = discrepancy.ResolvedAt,
            ResolutionNotes = discrepancy.ResolutionNotes
        };

        dto.AddSelfLink($"/api/deliveries/{id}/discrepancies/{discrepancy.Id}");

        return Ok(dto);
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<ActionResult<DeliveryDto>> Accept(Guid id, [FromBody] AcceptDeliveryRequest request)
    {
        var delivery = await _context.Deliveries
            .Include(d => d.Supplier)
            .Include(d => d.PurchaseOrder)
            .Include(d => d.Lines)
            .Include(d => d.Discrepancies)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (delivery == null)
            return NotFound();

        if (delivery.Status != "pending")
            return BadRequest(new { message = "Delivery already processed" });

        if (!delivery.Lines.Any())
            return BadRequest(new { message = "Cannot accept a delivery with no lines" });

        // Check for unresolved discrepancies
        var unresolvedDiscrepancies = delivery.Discrepancies.Any(d => d.ActionTaken == "pending");
        if (unresolvedDiscrepancies)
            return BadRequest(new { message = "Cannot accept delivery with unresolved discrepancies" });

        delivery.Status = "accepted";
        delivery.AcceptedAt = DateTime.UtcNow;

        // Update PO line received quantities if linked to a PO
        if (delivery.PurchaseOrderId.HasValue)
        {
            var purchaseOrder = await _context.PurchaseOrders
                .Include(po => po.Lines)
                .FirstOrDefaultAsync(po => po.Id == delivery.PurchaseOrderId);

            if (purchaseOrder != null)
            {
                foreach (var deliveryLine in delivery.Lines)
                {
                    if (deliveryLine.PurchaseOrderLineId.HasValue)
                    {
                        var poLine = purchaseOrder.Lines.FirstOrDefault(l => l.Id == deliveryLine.PurchaseOrderLineId);
                        if (poLine != null)
                        {
                            poLine.QuantityReceived += deliveryLine.QuantityReceived;
                        }
                    }
                }

                // Check if fully received
                var allReceived = purchaseOrder.Lines.All(l => l.QuantityReceived >= l.QuantityOrdered);
                purchaseOrder.Status = allReceived ? "received" : "partially_received";
                if (allReceived)
                {
                    purchaseOrder.ReceivedAt = DateTime.UtcNow;
                }
            }
        }

        await _context.SaveChangesAsync();

        var dto = MapToDto(delivery);
        dto.AddSelfLink($"/api/deliveries/{delivery.Id}");

        return Ok(dto);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<DeliveryDto>> Reject(Guid id, [FromBody] RejectDeliveryRequest request)
    {
        var delivery = await _context.Deliveries
            .Include(d => d.Supplier)
            .Include(d => d.PurchaseOrder)
            .Include(d => d.Lines)
            .Include(d => d.Discrepancies)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (delivery == null)
            return NotFound();

        if (delivery.Status != "pending")
            return BadRequest(new { message = "Delivery already processed" });

        delivery.Status = "rejected";
        delivery.RejectedAt = DateTime.UtcNow;
        delivery.RejectionReason = request.Reason;

        await _context.SaveChangesAsync();

        var dto = MapToDto(delivery);
        dto.AddSelfLink($"/api/deliveries/{delivery.Id}");

        return Ok(dto);
    }

    private static DeliveryDto MapToDto(Delivery delivery)
    {
        return new DeliveryDto
        {
            Id = delivery.Id,
            DeliveryNumber = delivery.DeliveryNumber,
            SupplierId = delivery.SupplierId,
            SupplierName = delivery.Supplier?.Name,
            PurchaseOrderId = delivery.PurchaseOrderId,
            PurchaseOrderNumber = delivery.PurchaseOrder?.OrderNumber,
            LocationId = delivery.LocationId,
            ReceivedByUserId = delivery.ReceivedByUserId,
            Status = delivery.Status,
            ReceivedAt = delivery.ReceivedAt,
            AcceptedAt = delivery.AcceptedAt,
            TotalValue = delivery.TotalValue,
            HasDiscrepancies = delivery.HasDiscrepancies,
            SupplierInvoiceNumber = delivery.SupplierInvoiceNumber,
            Notes = delivery.Notes,
            Lines = delivery.Lines.Select(l => new DeliveryLineDto
            {
                Id = l.Id,
                DeliveryId = l.DeliveryId,
                IngredientId = l.IngredientId,
                IngredientName = l.IngredientName,
                PurchaseOrderLineId = l.PurchaseOrderLineId,
                QuantityReceived = l.QuantityReceived,
                UnitCost = l.UnitCost,
                LineTotal = l.LineTotal,
                BatchNumber = l.BatchNumber,
                ExpiryDate = l.ExpiryDate,
                Notes = l.Notes
            }).ToList(),
            Discrepancies = delivery.Discrepancies.Select(d => new DeliveryDiscrepancyDto
            {
                Id = d.Id,
                DeliveryId = d.DeliveryId,
                DeliveryLineId = d.DeliveryLineId,
                DiscrepancyType = d.DiscrepancyType,
                QuantityAffected = d.QuantityAffected,
                PriceDifference = d.PriceDifference,
                Description = d.Description,
                ActionTaken = d.ActionTaken,
                ResolvedAt = d.ResolvedAt,
                ResolutionNotes = d.ResolutionNotes
            }).ToList()
        };
    }
}
