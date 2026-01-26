using DarkVelocity.Inventory.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Inventory.Api.Services;

public interface IRecipeCostService
{
    Task<decimal?> CalculateCostAsync(Guid recipeId, Guid locationId);
    Task RecalculateAllAsync(Guid locationId);
}

public class RecipeCostService : IRecipeCostService
{
    private readonly InventoryDbContext _context;

    public RecipeCostService(InventoryDbContext context)
    {
        _context = context;
    }

    public async Task<decimal?> CalculateCostAsync(Guid recipeId, Guid locationId)
    {
        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
                .ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(r => r.Id == recipeId);

        if (recipe == null)
            return null;

        decimal totalCost = 0;

        foreach (var recipeIngredient in recipe.Ingredients)
        {
            // Get the average unit cost from active batches at this location
            var avgCost = await _context.StockBatches
                .Where(b => b.IngredientId == recipeIngredient.IngredientId
                    && b.LocationId == locationId
                    && b.Status == "active"
                    && b.RemainingQuantity > 0)
                .AverageAsync(b => (decimal?)b.UnitCost) ?? 0;

            // Calculate effective quantity accounting for waste
            var effectiveQuantity = recipeIngredient.Quantity * (1 + recipeIngredient.WastePercentage / 100);

            totalCost += effectiveQuantity * avgCost;
        }

        recipe.CalculatedCost = totalCost;
        recipe.CostCalculatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return totalCost;
    }

    public async Task RecalculateAllAsync(Guid locationId)
    {
        var recipes = await _context.Recipes
            .Where(r => r.IsActive)
            .ToListAsync();

        foreach (var recipe in recipes)
        {
            await CalculateCostAsync(recipe.Id, locationId);
        }
    }
}
