using DarkVelocity.Inventory.Api.Data;
using DarkVelocity.Inventory.Api.Dtos;
using DarkVelocity.Inventory.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Inventory.Api.Services;

public interface IFifoConsumptionService
{
    Task<ConsumptionResultDto> ConsumeAsync(
        Guid locationId,
        Guid ingredientId,
        decimal quantityNeeded,
        Guid? orderId = null,
        Guid? recipeId = null,
        string consumptionType = "sale");
}

public class FifoConsumptionService : IFifoConsumptionService
{
    private readonly InventoryDbContext _context;

    public FifoConsumptionService(InventoryDbContext context)
    {
        _context = context;
    }

    public async Task<ConsumptionResultDto> ConsumeAsync(
        Guid locationId,
        Guid ingredientId,
        decimal quantityNeeded,
        Guid? orderId = null,
        Guid? recipeId = null,
        string consumptionType = "sale")
    {
        var result = new ConsumptionResultDto();
        var remainingNeeded = quantityNeeded;

        // Get active batches ordered by received date (oldest first) - FIFO
        var batches = await _context.StockBatches
            .Where(b => b.IngredientId == ingredientId
                && b.LocationId == locationId
                && b.Status == "active"
                && b.RemainingQuantity > 0)
            .OrderBy(b => b.ReceivedAt)
            .ThenBy(b => b.Id)
            .ToListAsync();

        foreach (var batch in batches)
        {
            if (remainingNeeded <= 0)
                break;

            var toConsume = Math.Min(remainingNeeded, batch.RemainingQuantity);
            batch.RemainingQuantity -= toConsume;

            // Mark batch as exhausted if empty
            if (batch.RemainingQuantity <= 0)
            {
                batch.Status = "exhausted";
            }

            // Record consumption
            var consumption = new StockConsumption
            {
                LocationId = locationId,
                StockBatchId = batch.Id,
                IngredientId = ingredientId,
                OrderId = orderId,
                RecipeId = recipeId,
                Quantity = toConsume,
                UnitCost = batch.UnitCost,
                TotalCost = toConsume * batch.UnitCost,
                ConsumptionType = consumptionType,
                ConsumedAt = DateTime.UtcNow
            };

            _context.StockConsumptions.Add(consumption);

            result.BatchConsumptions.Add(new BatchConsumptionDto
            {
                BatchId = batch.Id,
                QuantityConsumed = toConsume,
                UnitCost = batch.UnitCost,
                Cost = toConsume * batch.UnitCost
            });

            result.TotalQuantityConsumed += toConsume;
            result.TotalCost += toConsume * batch.UnitCost;
            remainingNeeded -= toConsume;
        }

        // Update ingredient current stock
        var ingredient = await _context.Ingredients.FindAsync(ingredientId);
        if (ingredient != null)
        {
            ingredient.CurrentStock = await _context.StockBatches
                .Where(b => b.IngredientId == ingredientId && b.Status == "active")
                .SumAsync(b => b.RemainingQuantity);
        }

        await _context.SaveChangesAsync();

        return result;
    }
}
