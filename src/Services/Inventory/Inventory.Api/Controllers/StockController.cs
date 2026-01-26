using DarkVelocity.Inventory.Api.Data;
using DarkVelocity.Inventory.Api.Dtos;
using DarkVelocity.Inventory.Api.Entities;
using DarkVelocity.Inventory.Api.Services;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Inventory.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/stock")]
public class StockController : ControllerBase
{
    private readonly InventoryDbContext _context;
    private readonly IFifoConsumptionService _consumptionService;

    public StockController(InventoryDbContext context, IFifoConsumptionService consumptionService)
    {
        _context = context;
        _consumptionService = consumptionService;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<StockLevelDto>>> GetStockLevels(Guid locationId)
    {
        var stockLevels = await _context.Ingredients
            .Where(i => i.IsActive)
            .Select(i => new StockLevelDto
            {
                IngredientId = i.Id,
                IngredientCode = i.Code,
                IngredientName = i.Name,
                UnitOfMeasure = i.UnitOfMeasure,
                TotalStock = i.StockBatches
                    .Where(b => b.LocationId == locationId && b.Status == "active")
                    .Sum(b => b.RemainingQuantity),
                ReorderLevel = i.ReorderLevel,
                IsLowStock = i.StockBatches
                    .Where(b => b.LocationId == locationId && b.Status == "active")
                    .Sum(b => b.RemainingQuantity) <= i.ReorderLevel,
                ActiveBatchCount = i.StockBatches
                    .Count(b => b.LocationId == locationId && b.Status == "active" && b.RemainingQuantity > 0),
                AverageUnitCost = i.StockBatches
                    .Where(b => b.LocationId == locationId && b.Status == "active" && b.RemainingQuantity > 0)
                    .Average(b => (decimal?)b.UnitCost) ?? 0,
                TotalValue = i.StockBatches
                    .Where(b => b.LocationId == locationId && b.Status == "active")
                    .Sum(b => b.RemainingQuantity * b.UnitCost)
            })
            .ToListAsync();

        foreach (var level in stockLevels)
        {
            level.AddSelfLink($"/api/locations/{locationId}/stock/{level.IngredientId}");
            level.AddLink("batches", $"/api/locations/{locationId}/stock/{level.IngredientId}/batches");
        }

        return Ok(HalCollection<StockLevelDto>.Create(
            stockLevels,
            $"/api/locations/{locationId}/stock",
            stockLevels.Count
        ));
    }

    [HttpGet("low-stock")]
    public async Task<ActionResult<HalCollection<StockLevelDto>>> GetLowStock(Guid locationId)
    {
        var lowStockItems = await _context.Ingredients
            .Where(i => i.IsActive)
            .Select(i => new
            {
                Ingredient = i,
                TotalStock = i.StockBatches
                    .Where(b => b.LocationId == locationId && b.Status == "active")
                    .Sum(b => b.RemainingQuantity)
            })
            .Where(x => x.TotalStock <= x.Ingredient.ReorderLevel)
            .Select(x => new StockLevelDto
            {
                IngredientId = x.Ingredient.Id,
                IngredientCode = x.Ingredient.Code,
                IngredientName = x.Ingredient.Name,
                UnitOfMeasure = x.Ingredient.UnitOfMeasure,
                TotalStock = x.TotalStock,
                ReorderLevel = x.Ingredient.ReorderLevel,
                IsLowStock = true,
                ActiveBatchCount = x.Ingredient.StockBatches
                    .Count(b => b.LocationId == locationId && b.Status == "active" && b.RemainingQuantity > 0),
                AverageUnitCost = x.Ingredient.StockBatches
                    .Where(b => b.LocationId == locationId && b.Status == "active" && b.RemainingQuantity > 0)
                    .Average(b => (decimal?)b.UnitCost) ?? 0,
                TotalValue = x.Ingredient.StockBatches
                    .Where(b => b.LocationId == locationId && b.Status == "active")
                    .Sum(b => b.RemainingQuantity * b.UnitCost)
            })
            .ToListAsync();

        foreach (var level in lowStockItems)
        {
            level.AddSelfLink($"/api/locations/{locationId}/stock/{level.IngredientId}");
        }

        return Ok(HalCollection<StockLevelDto>.Create(
            lowStockItems,
            $"/api/locations/{locationId}/stock/low-stock",
            lowStockItems.Count
        ));
    }

    [HttpGet("{ingredientId:guid}/batches")]
    public async Task<ActionResult<HalCollection<StockBatchDto>>> GetBatches(Guid locationId, Guid ingredientId)
    {
        var batches = await _context.StockBatches
            .Include(b => b.Ingredient)
            .Where(b => b.LocationId == locationId && b.IngredientId == ingredientId)
            .OrderBy(b => b.ReceivedAt)
            .Select(b => new StockBatchDto
            {
                Id = b.Id,
                IngredientId = b.IngredientId,
                LocationId = b.LocationId,
                DeliveryId = b.DeliveryId,
                IngredientName = b.Ingredient!.Name,
                InitialQuantity = b.InitialQuantity,
                RemainingQuantity = b.RemainingQuantity,
                UnitCost = b.UnitCost,
                ReceivedAt = b.ReceivedAt,
                ExpiryDate = b.ExpiryDate,
                BatchNumber = b.BatchNumber,
                Status = b.Status
            })
            .ToListAsync();

        foreach (var batch in batches)
        {
            batch.AddSelfLink($"/api/locations/{locationId}/stock/{ingredientId}/batches/{batch.Id}");
        }

        return Ok(HalCollection<StockBatchDto>.Create(
            batches,
            $"/api/locations/{locationId}/stock/{ingredientId}/batches",
            batches.Count
        ));
    }

    [HttpPost("batches")]
    public async Task<ActionResult<StockBatchDto>> CreateBatch(Guid locationId, [FromBody] CreateStockBatchRequest request)
    {
        var ingredient = await _context.Ingredients.FindAsync(request.IngredientId);
        if (ingredient == null)
            return BadRequest(new { message = "Invalid ingredient" });

        var batch = new StockBatch
        {
            IngredientId = request.IngredientId,
            LocationId = locationId,
            DeliveryId = request.DeliveryId,
            InitialQuantity = request.Quantity,
            RemainingQuantity = request.Quantity,
            UnitCost = request.UnitCost,
            ReceivedAt = DateTime.UtcNow,
            ExpiryDate = request.ExpiryDate,
            BatchNumber = request.BatchNumber,
            Status = "active"
        };

        _context.StockBatches.Add(batch);

        // Update ingredient current stock
        ingredient.CurrentStock = await _context.StockBatches
            .Where(b => b.IngredientId == request.IngredientId && b.LocationId == locationId && b.Status == "active")
            .SumAsync(b => b.RemainingQuantity) + request.Quantity;

        await _context.SaveChangesAsync();

        var dto = new StockBatchDto
        {
            Id = batch.Id,
            IngredientId = batch.IngredientId,
            LocationId = batch.LocationId,
            DeliveryId = batch.DeliveryId,
            IngredientName = ingredient.Name,
            InitialQuantity = batch.InitialQuantity,
            RemainingQuantity = batch.RemainingQuantity,
            UnitCost = batch.UnitCost,
            ReceivedAt = batch.ReceivedAt,
            ExpiryDate = batch.ExpiryDate,
            BatchNumber = batch.BatchNumber,
            Status = batch.Status
        };

        dto.AddSelfLink($"/api/locations/{locationId}/stock/{request.IngredientId}/batches/{batch.Id}");

        return Created($"/api/locations/{locationId}/stock/{request.IngredientId}/batches/{batch.Id}", dto);
    }

    [HttpPost("consume")]
    public async Task<ActionResult<ConsumptionResultDto>> ConsumeStock(Guid locationId, [FromBody] ConsumeStockRequest request)
    {
        var ingredient = await _context.Ingredients.FindAsync(request.IngredientId);
        if (ingredient == null)
            return BadRequest(new { message = "Invalid ingredient" });

        var result = await _consumptionService.ConsumeAsync(
            locationId,
            request.IngredientId,
            request.Quantity,
            request.OrderId,
            request.RecipeId,
            request.ConsumptionType);

        if (result.TotalQuantityConsumed < request.Quantity)
        {
            return Ok(new
            {
                result,
                warning = $"Insufficient stock. Requested {request.Quantity}, consumed {result.TotalQuantityConsumed}"
            });
        }

        return Ok(result);
    }
}
