using DarkVelocity.Inventory.Api.Data;
using DarkVelocity.Inventory.Api.Dtos;
using DarkVelocity.Inventory.Api.Entities;
using DarkVelocity.Inventory.Api.Services;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Inventory.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/waste")]
public class WasteController : ControllerBase
{
    private readonly InventoryDbContext _context;
    private readonly IFifoConsumptionService _consumptionService;

    public WasteController(InventoryDbContext context, IFifoConsumptionService consumptionService)
    {
        _context = context;
        _consumptionService = consumptionService;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<WasteRecordDto>>> GetAll(
        Guid locationId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var query = _context.WasteRecords
            .Include(w => w.Ingredient)
            .Where(w => w.LocationId == locationId);

        if (from.HasValue)
            query = query.Where(w => w.RecordedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(w => w.RecordedAt <= to.Value);

        var records = await query
            .OrderByDescending(w => w.RecordedAt)
            .Take(100)
            .Select(w => new WasteRecordDto
            {
                Id = w.Id,
                LocationId = w.LocationId,
                IngredientId = w.IngredientId,
                IngredientName = w.Ingredient!.Name,
                StockBatchId = w.StockBatchId,
                RecordedByUserId = w.RecordedByUserId,
                Quantity = w.Quantity,
                EstimatedCost = w.EstimatedCost,
                Reason = w.Reason,
                Notes = w.Notes,
                RecordedAt = w.RecordedAt
            })
            .ToListAsync();

        foreach (var record in records)
        {
            record.AddSelfLink($"/api/locations/{locationId}/waste/{record.Id}");
        }

        return Ok(HalCollection<WasteRecordDto>.Create(
            records,
            $"/api/locations/{locationId}/waste",
            records.Count
        ));
    }

    [HttpPost]
    public async Task<ActionResult<WasteRecordDto>> RecordWaste(Guid locationId, [FromBody] RecordWasteRequest request)
    {
        var ingredient = await _context.Ingredients.FindAsync(request.IngredientId);
        if (ingredient == null)
            return BadRequest(new { message = "Invalid ingredient" });

        // Consume stock via FIFO to get accurate cost
        var consumptionResult = await _consumptionService.ConsumeAsync(
            locationId,
            request.IngredientId,
            request.Quantity,
            consumptionType: "waste");

        var wasteRecord = new WasteRecord
        {
            LocationId = locationId,
            IngredientId = request.IngredientId,
            StockBatchId = request.StockBatchId,
            RecordedByUserId = request.RecordedByUserId,
            Quantity = request.Quantity,
            EstimatedCost = consumptionResult.TotalCost,
            Reason = request.Reason,
            Notes = request.Notes,
            RecordedAt = DateTime.UtcNow
        };

        _context.WasteRecords.Add(wasteRecord);
        await _context.SaveChangesAsync();

        var dto = new WasteRecordDto
        {
            Id = wasteRecord.Id,
            LocationId = wasteRecord.LocationId,
            IngredientId = wasteRecord.IngredientId,
            IngredientName = ingredient.Name,
            StockBatchId = wasteRecord.StockBatchId,
            RecordedByUserId = wasteRecord.RecordedByUserId,
            Quantity = wasteRecord.Quantity,
            EstimatedCost = wasteRecord.EstimatedCost,
            Reason = wasteRecord.Reason,
            Notes = wasteRecord.Notes,
            RecordedAt = wasteRecord.RecordedAt
        };

        dto.AddSelfLink($"/api/locations/{locationId}/waste/{wasteRecord.Id}");

        return Created($"/api/locations/{locationId}/waste/{wasteRecord.Id}", dto);
    }
}
