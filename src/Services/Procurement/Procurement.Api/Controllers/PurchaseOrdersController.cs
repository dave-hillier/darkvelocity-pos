using DarkVelocity.Procurement.Api.Data;
using DarkVelocity.Procurement.Api.Dtos;
using DarkVelocity.Procurement.Api.Entities;
using DarkVelocity.Procurement.Api.Services;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Procurement.Api.Controllers;

[ApiController]
[Route("api/purchase-orders")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly ProcurementDbContext _context;
    private readonly IPurchaseOrderNumberGenerator _orderNumberGenerator;

    public PurchaseOrdersController(
        ProcurementDbContext context,
        IPurchaseOrderNumberGenerator orderNumberGenerator)
    {
        _context = context;
        _orderNumberGenerator = orderNumberGenerator;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<PurchaseOrderDto>>> GetAll(
        [FromQuery] Guid? locationId = null,
        [FromQuery] Guid? supplierId = null,
        [FromQuery] string? status = null)
    {
        var query = _context.PurchaseOrders
            .Include(po => po.Supplier)
            .Include(po => po.Lines)
            .AsQueryable();

        if (locationId.HasValue)
            query = query.Where(po => po.LocationId == locationId.Value);
        if (supplierId.HasValue)
            query = query.Where(po => po.SupplierId == supplierId.Value);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(po => po.Status == status);

        var orders = await query
            .OrderByDescending(po => po.CreatedAt)
            .Take(100)
            .Select(po => new PurchaseOrderDto
            {
                Id = po.Id,
                OrderNumber = po.OrderNumber,
                SupplierId = po.SupplierId,
                SupplierName = po.Supplier!.Name,
                LocationId = po.LocationId,
                CreatedByUserId = po.CreatedByUserId,
                Status = po.Status,
                ExpectedDeliveryDate = po.ExpectedDeliveryDate,
                SubmittedAt = po.SubmittedAt,
                ReceivedAt = po.ReceivedAt,
                OrderTotal = po.OrderTotal,
                Notes = po.Notes,
                Lines = po.Lines.Select(l => new PurchaseOrderLineDto
                {
                    Id = l.Id,
                    PurchaseOrderId = l.PurchaseOrderId,
                    IngredientId = l.IngredientId,
                    IngredientName = l.IngredientName,
                    QuantityOrdered = l.QuantityOrdered,
                    QuantityReceived = l.QuantityReceived,
                    UnitPrice = l.UnitPrice,
                    LineTotal = l.LineTotal,
                    Notes = l.Notes
                }).ToList()
            })
            .ToListAsync();

        foreach (var order in orders)
        {
            order.AddSelfLink($"/api/purchase-orders/{order.Id}");
        }

        return Ok(HalCollection<PurchaseOrderDto>.Create(
            orders,
            "/api/purchase-orders",
            orders.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PurchaseOrderDto>> GetById(Guid id)
    {
        var order = await _context.PurchaseOrders
            .Include(po => po.Supplier)
            .Include(po => po.Lines)
            .FirstOrDefaultAsync(po => po.Id == id);

        if (order == null)
            return NotFound();

        var dto = MapToDto(order);
        dto.AddSelfLink($"/api/purchase-orders/{order.Id}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<PurchaseOrderDto>> Create([FromBody] CreatePurchaseOrderRequest request)
    {
        var supplier = await _context.Suppliers.FindAsync(request.SupplierId);
        if (supplier == null)
            return BadRequest(new { message = "Invalid supplier" });

        var orderNumber = await _orderNumberGenerator.GenerateAsync(request.LocationId);

        var order = new PurchaseOrder
        {
            OrderNumber = orderNumber,
            SupplierId = request.SupplierId,
            LocationId = request.LocationId,
            CreatedByUserId = request.CreatedByUserId,
            ExpectedDeliveryDate = request.ExpectedDeliveryDate,
            Notes = request.Notes
        };

        _context.PurchaseOrders.Add(order);
        await _context.SaveChangesAsync();

        // Reload with navigation
        await _context.Entry(order).Reference(o => o.Supplier).LoadAsync();

        var dto = MapToDto(order);
        dto.AddSelfLink($"/api/purchase-orders/{order.Id}");

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, dto);
    }

    [HttpPost("{id:guid}/lines")]
    public async Task<ActionResult<PurchaseOrderLineDto>> AddLine(Guid id, [FromBody] AddPurchaseOrderLineRequest request)
    {
        var order = await _context.PurchaseOrders
            .Include(po => po.Lines)
            .FirstOrDefaultAsync(po => po.Id == id);

        if (order == null)
            return NotFound();

        if (order.Status != "draft")
            return BadRequest(new { message = "Cannot add lines to a non-draft order" });

        var line = new PurchaseOrderLine
        {
            PurchaseOrderId = id,
            IngredientId = request.IngredientId,
            IngredientName = request.IngredientName,
            QuantityOrdered = request.QuantityOrdered,
            UnitPrice = request.UnitPrice,
            LineTotal = request.QuantityOrdered * request.UnitPrice,
            Notes = request.Notes
        };

        _context.PurchaseOrderLines.Add(line);
        await _context.SaveChangesAsync();

        // Reload lines for recalculation
        await _context.Entry(order).Collection(o => o.Lines).LoadAsync();
        order.OrderTotal = order.Lines.Sum(l => l.LineTotal);
        await _context.SaveChangesAsync();

        var dto = new PurchaseOrderLineDto
        {
            Id = line.Id,
            PurchaseOrderId = line.PurchaseOrderId,
            IngredientId = line.IngredientId,
            IngredientName = line.IngredientName,
            QuantityOrdered = line.QuantityOrdered,
            QuantityReceived = line.QuantityReceived,
            UnitPrice = line.UnitPrice,
            LineTotal = line.LineTotal,
            Notes = line.Notes
        };

        dto.AddSelfLink($"/api/purchase-orders/{id}/lines/{line.Id}");

        return Created($"/api/purchase-orders/{id}/lines/{line.Id}", dto);
    }

    [HttpPatch("{id:guid}/lines/{lineId:guid}")]
    public async Task<ActionResult<PurchaseOrderLineDto>> UpdateLine(
        Guid id,
        Guid lineId,
        [FromBody] UpdatePurchaseOrderLineRequest request)
    {
        var order = await _context.PurchaseOrders
            .Include(po => po.Lines)
            .FirstOrDefaultAsync(po => po.Id == id);

        if (order == null)
            return NotFound();

        if (order.Status != "draft")
            return BadRequest(new { message = "Cannot update lines on a non-draft order" });

        var line = order.Lines.FirstOrDefault(l => l.Id == lineId);
        if (line == null)
            return NotFound();

        if (request.QuantityOrdered.HasValue)
            line.QuantityOrdered = request.QuantityOrdered.Value;
        if (request.UnitPrice.HasValue)
            line.UnitPrice = request.UnitPrice.Value;
        if (request.Notes != null)
            line.Notes = request.Notes;

        line.LineTotal = line.QuantityOrdered * line.UnitPrice;

        // Recalculate order total
        order.OrderTotal = order.Lines.Sum(l => l.LineTotal);

        await _context.SaveChangesAsync();

        var dto = new PurchaseOrderLineDto
        {
            Id = line.Id,
            PurchaseOrderId = line.PurchaseOrderId,
            IngredientId = line.IngredientId,
            IngredientName = line.IngredientName,
            QuantityOrdered = line.QuantityOrdered,
            QuantityReceived = line.QuantityReceived,
            UnitPrice = line.UnitPrice,
            LineTotal = line.LineTotal,
            Notes = line.Notes
        };

        dto.AddSelfLink($"/api/purchase-orders/{id}/lines/{line.Id}");

        return Ok(dto);
    }

    [HttpDelete("{id:guid}/lines/{lineId:guid}")]
    public async Task<IActionResult> RemoveLine(Guid id, Guid lineId)
    {
        var order = await _context.PurchaseOrders
            .Include(po => po.Lines)
            .FirstOrDefaultAsync(po => po.Id == id);

        if (order == null)
            return NotFound();

        if (order.Status != "draft")
            return BadRequest(new { message = "Cannot remove lines from a non-draft order" });

        var line = order.Lines.FirstOrDefault(l => l.Id == lineId);
        if (line == null)
            return NotFound();

        _context.PurchaseOrderLines.Remove(line);

        // Recalculate order total
        order.OrderTotal = order.Lines.Where(l => l.Id != lineId).Sum(l => l.LineTotal);

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<PurchaseOrderDto>> Submit(Guid id, [FromBody] SubmitPurchaseOrderRequest request)
    {
        var order = await _context.PurchaseOrders
            .Include(po => po.Supplier)
            .Include(po => po.Lines)
            .FirstOrDefaultAsync(po => po.Id == id);

        if (order == null)
            return NotFound();

        if (order.Status != "draft")
            return BadRequest(new { message = "Only draft orders can be submitted" });

        if (!order.Lines.Any())
            return BadRequest(new { message = "Cannot submit an order with no lines" });

        order.Status = "submitted";
        order.SubmittedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(order);
        dto.AddSelfLink($"/api/purchase-orders/{order.Id}");

        return Ok(dto);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<PurchaseOrderDto>> Cancel(Guid id, [FromBody] CancelPurchaseOrderRequest request)
    {
        var order = await _context.PurchaseOrders
            .Include(po => po.Supplier)
            .Include(po => po.Lines)
            .FirstOrDefaultAsync(po => po.Id == id);

        if (order == null)
            return NotFound();

        if (order.Status == "received" || order.Status == "cancelled")
            return BadRequest(new { message = "Cannot cancel a received or already cancelled order" });

        order.Status = "cancelled";
        order.CancelledAt = DateTime.UtcNow;
        order.CancellationReason = request.Reason;

        await _context.SaveChangesAsync();

        var dto = MapToDto(order);
        dto.AddSelfLink($"/api/purchase-orders/{order.Id}");

        return Ok(dto);
    }

    private static PurchaseOrderDto MapToDto(PurchaseOrder order)
    {
        return new PurchaseOrderDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            SupplierId = order.SupplierId,
            SupplierName = order.Supplier?.Name,
            LocationId = order.LocationId,
            CreatedByUserId = order.CreatedByUserId,
            Status = order.Status,
            ExpectedDeliveryDate = order.ExpectedDeliveryDate,
            SubmittedAt = order.SubmittedAt,
            ReceivedAt = order.ReceivedAt,
            OrderTotal = order.OrderTotal,
            Notes = order.Notes,
            Lines = order.Lines.Select(l => new PurchaseOrderLineDto
            {
                Id = l.Id,
                PurchaseOrderId = l.PurchaseOrderId,
                IngredientId = l.IngredientId,
                IngredientName = l.IngredientName,
                QuantityOrdered = l.QuantityOrdered,
                QuantityReceived = l.QuantityReceived,
                UnitPrice = l.UnitPrice,
                LineTotal = l.LineTotal,
                Notes = l.Notes
            }).ToList()
        };
    }
}
