using DarkVelocity.Reporting.Api.Data;
using DarkVelocity.Reporting.Api.Dtos;
using DarkVelocity.Reporting.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Reporting.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/stock-consumptions")]
public class StockConsumptionsController : ControllerBase
{
    private readonly ReportingDbContext _context;

    public StockConsumptionsController(ReportingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<StockConsumptionDto>>> GetAll(
        Guid locationId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] Guid? orderId = null,
        [FromQuery] Guid? menuItemId = null,
        [FromQuery] Guid? ingredientId = null)
    {
        var query = _context.StockConsumptions
            .Where(c => c.LocationId == locationId);

        if (startDate.HasValue)
            query = query.Where(c => c.ConsumedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(c => c.ConsumedAt <= endDate.Value);

        if (orderId.HasValue)
            query = query.Where(c => c.OrderId == orderId.Value);

        if (menuItemId.HasValue)
            query = query.Where(c => c.MenuItemId == menuItemId.Value);

        if (ingredientId.HasValue)
            query = query.Where(c => c.IngredientId == ingredientId.Value);

        var consumptions = await query
            .OrderByDescending(c => c.ConsumedAt)
            .Take(1000) // Limit for performance
            .ToListAsync();

        return Ok(consumptions.Select(MapToDto).ToList());
    }

    [HttpGet("{consumptionId:guid}")]
    public async Task<ActionResult<StockConsumptionDto>> GetById(Guid locationId, Guid consumptionId)
    {
        var consumption = await _context.StockConsumptions
            .FirstOrDefaultAsync(c => c.LocationId == locationId && c.Id == consumptionId);

        if (consumption == null)
            return NotFound();

        var dto = MapToDto(consumption);
        dto.AddSelfLink($"/api/locations/{locationId}/stock-consumptions/{consumptionId}");

        return Ok(dto);
    }

    [HttpGet("by-order/{orderId:guid}")]
    public async Task<ActionResult<List<StockConsumptionDto>>> GetByOrder(Guid locationId, Guid orderId)
    {
        var consumptions = await _context.StockConsumptions
            .Where(c => c.LocationId == locationId && c.OrderId == orderId)
            .OrderBy(c => c.ConsumedAt)
            .ToListAsync();

        return Ok(consumptions.Select(MapToDto).ToList());
    }

    [HttpGet("summary")]
    public async Task<ActionResult<StockConsumptionSummaryDto>> GetSummary(
        Guid locationId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(-30);
        var end = endDate ?? DateTime.UtcNow;

        var consumptions = await _context.StockConsumptions
            .Where(c => c.LocationId == locationId
                && c.ConsumedAt >= start
                && c.ConsumedAt <= end)
            .ToListAsync();

        var summary = new StockConsumptionSummaryDto
        {
            StartDate = start,
            EndDate = end,
            TotalConsumptions = consumptions.Count,
            TotalCost = consumptions.Sum(c => c.TotalCost),
            UniqueOrders = consumptions.Select(c => c.OrderId).Distinct().Count(),
            UniqueIngredients = consumptions.Select(c => c.IngredientId).Distinct().Count(),
            ByIngredient = consumptions
                .GroupBy(c => new { c.IngredientId, c.IngredientName })
                .Select(g => new IngredientConsumptionDto
                {
                    IngredientId = g.Key.IngredientId,
                    IngredientName = g.Key.IngredientName,
                    TotalQuantity = g.Sum(c => c.QuantityConsumed),
                    TotalCost = g.Sum(c => c.TotalCost),
                    ConsumptionCount = g.Count()
                })
                .OrderByDescending(i => i.TotalCost)
                .ToList()
        };

        return Ok(summary);
    }

    [HttpPost]
    public async Task<ActionResult<StockConsumptionDto>> Record(
        Guid locationId,
        [FromBody] RecordConsumptionRequest request)
    {
        var consumption = new StockConsumption
        {
            LocationId = locationId,
            OrderId = request.OrderId,
            OrderLineId = request.OrderLineId,
            MenuItemId = request.MenuItemId,
            IngredientId = request.IngredientId,
            IngredientName = request.IngredientName,
            StockBatchId = request.StockBatchId,
            QuantityConsumed = request.QuantityConsumed,
            UnitCost = request.UnitCost,
            TotalCost = request.QuantityConsumed * request.UnitCost,
            ConsumedAt = DateTime.UtcNow
        };

        _context.StockConsumptions.Add(consumption);
        await _context.SaveChangesAsync();

        var dto = MapToDto(consumption);
        dto.AddSelfLink($"/api/locations/{locationId}/stock-consumptions/{consumption.Id}");

        return CreatedAtAction(nameof(GetById), new { locationId, consumptionId = consumption.Id }, dto);
    }

    [HttpPost("batch")]
    public async Task<ActionResult<List<StockConsumptionDto>>> RecordBatch(
        Guid locationId,
        [FromBody] List<RecordConsumptionRequest> requests)
    {
        var consumptions = requests.Select(request => new StockConsumption
        {
            LocationId = locationId,
            OrderId = request.OrderId,
            OrderLineId = request.OrderLineId,
            MenuItemId = request.MenuItemId,
            IngredientId = request.IngredientId,
            IngredientName = request.IngredientName,
            StockBatchId = request.StockBatchId,
            QuantityConsumed = request.QuantityConsumed,
            UnitCost = request.UnitCost,
            TotalCost = request.QuantityConsumed * request.UnitCost,
            ConsumedAt = DateTime.UtcNow
        }).ToList();

        _context.StockConsumptions.AddRange(consumptions);
        await _context.SaveChangesAsync();

        return Ok(consumptions.Select(MapToDto).ToList());
    }

    private static StockConsumptionDto MapToDto(StockConsumption consumption)
    {
        return new StockConsumptionDto
        {
            Id = consumption.Id,
            LocationId = consumption.LocationId,
            OrderId = consumption.OrderId,
            OrderLineId = consumption.OrderLineId,
            MenuItemId = consumption.MenuItemId,
            IngredientId = consumption.IngredientId,
            IngredientName = consumption.IngredientName,
            StockBatchId = consumption.StockBatchId,
            QuantityConsumed = consumption.QuantityConsumed,
            UnitCost = consumption.UnitCost,
            TotalCost = consumption.TotalCost,
            ConsumedAt = consumption.ConsumedAt
        };
    }
}

public class StockConsumptionSummaryDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalConsumptions { get; set; }
    public decimal TotalCost { get; set; }
    public int UniqueOrders { get; set; }
    public int UniqueIngredients { get; set; }
    public List<IngredientConsumptionDto> ByIngredient { get; set; } = new();
}

public class IngredientConsumptionDto
{
    public Guid IngredientId { get; set; }
    public string? IngredientName { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal TotalCost { get; set; }
    public int ConsumptionCount { get; set; }
}
