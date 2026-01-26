using DarkVelocity.Costing.Api.Data;
using DarkVelocity.Costing.Api.Dtos;
using DarkVelocity.Costing.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Costing.Api.Controllers;

[ApiController]
[Route("api/ingredient-prices")]
public class IngredientPricesController : ControllerBase
{
    private readonly CostingDbContext _context;

    public IngredientPricesController(CostingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<IngredientPriceDto>>> GetAll(
        [FromQuery] bool? isActive = null,
        [FromQuery] bool? hasRecentChange = null)
    {
        var query = _context.IngredientPrices.AsQueryable();

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        if (hasRecentChange == true)
        {
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            query = query.Where(p => p.PriceChangedAt >= thirtyDaysAgo);
        }

        var prices = await query
            .OrderBy(p => p.IngredientName)
            .ToListAsync();

        return Ok(prices.Select(MapToDto).ToList());
    }

    [HttpGet("{ingredientId:guid}")]
    public async Task<ActionResult<IngredientPriceDto>> GetByIngredient(Guid ingredientId)
    {
        var price = await _context.IngredientPrices
            .FirstOrDefaultAsync(p => p.IngredientId == ingredientId);

        if (price == null)
            return NotFound();

        var dto = MapToDto(price);
        dto.AddSelfLink($"/api/ingredient-prices/{ingredientId}");
        dto.AddLink("recipes", $"/api/ingredient-prices/{ingredientId}/recipes");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<IngredientPriceDto>> Create(
        [FromBody] CreateIngredientPriceRequest request)
    {
        var existing = await _context.IngredientPrices
            .AnyAsync(p => p.IngredientId == request.IngredientId);

        if (existing)
            return Conflict(new { message = "A price entry already exists for this ingredient" });

        var pricePerUnit = request.PackSize > 0 ? request.CurrentPrice / request.PackSize : request.CurrentPrice;

        var price = new IngredientPrice
        {
            IngredientId = request.IngredientId,
            IngredientName = request.IngredientName,
            CurrentPrice = request.CurrentPrice,
            UnitOfMeasure = request.UnitOfMeasure,
            PackSize = request.PackSize,
            PricePerUnit = pricePerUnit,
            PreferredSupplierId = request.PreferredSupplierId,
            PreferredSupplierName = request.PreferredSupplierName
        };

        _context.IngredientPrices.Add(price);
        await _context.SaveChangesAsync();

        var dto = MapToDto(price);
        dto.AddSelfLink($"/api/ingredient-prices/{request.IngredientId}");

        return CreatedAtAction(nameof(GetByIngredient),
            new { ingredientId = request.IngredientId }, dto);
    }

    [HttpPut("{ingredientId:guid}")]
    public async Task<ActionResult<IngredientPriceDto>> Update(
        Guid ingredientId,
        [FromBody] UpdateIngredientPriceRequest request)
    {
        var price = await _context.IngredientPrices
            .FirstOrDefaultAsync(p => p.IngredientId == ingredientId);

        if (price == null)
            return NotFound();

        // Track price change
        var previousPrice = price.CurrentPrice;
        var previousPricePerUnit = price.PricePerUnit;

        price.CurrentPrice = request.CurrentPrice;
        price.PackSize = request.PackSize;
        price.PricePerUnit = request.PackSize > 0 ? request.CurrentPrice / request.PackSize : request.CurrentPrice;

        if (request.PreferredSupplierId.HasValue)
            price.PreferredSupplierId = request.PreferredSupplierId;

        if (request.PreferredSupplierName != null)
            price.PreferredSupplierName = request.PreferredSupplierName;

        // Calculate change
        if (previousPrice != price.CurrentPrice)
        {
            price.PreviousPrice = previousPrice;
            price.PriceChangedAt = DateTime.UtcNow;
            price.PriceChangePercent = previousPrice > 0
                ? ((price.CurrentPrice - previousPrice) / previousPrice) * 100
                : 0;

            // Update all recipes using this ingredient
            await UpdateRecipeIngredientCosts(ingredientId, price.PricePerUnit);
        }

        price.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(price);
        dto.AddSelfLink($"/api/ingredient-prices/{ingredientId}");

        return Ok(dto);
    }

    [HttpGet("{ingredientId:guid}/recipes")]
    public async Task<ActionResult<List<RecipeSummaryDto>>> GetAffectedRecipes(Guid ingredientId)
    {
        var recipes = await _context.RecipeIngredients
            .Where(ri => ri.IngredientId == ingredientId)
            .Include(ri => ri.Recipe)
            .Select(ri => ri.Recipe!)
            .Distinct()
            .ToListAsync();

        return Ok(recipes.Select(r => new RecipeSummaryDto
        {
            Id = r.Id,
            MenuItemId = r.MenuItemId,
            MenuItemName = r.MenuItemName,
            Code = r.Code,
            CurrentCostPerPortion = r.CurrentCostPerPortion,
            IngredientCount = r.Ingredients.Count,
            IsActive = r.IsActive
        }).ToList());
    }

    [HttpPost("{ingredientId:guid}/recalculate-recipes")]
    public async Task<ActionResult<object>> RecalculateAffectedRecipes(Guid ingredientId)
    {
        var price = await _context.IngredientPrices
            .FirstOrDefaultAsync(p => p.IngredientId == ingredientId);

        if (price == null)
            return NotFound();

        var affectedCount = await UpdateRecipeIngredientCosts(ingredientId, price.PricePerUnit);

        return Ok(new
        {
            IngredientId = ingredientId,
            IngredientName = price.IngredientName,
            NewPricePerUnit = price.PricePerUnit,
            AffectedRecipeCount = affectedCount
        });
    }

    [HttpDelete("{ingredientId:guid}")]
    public async Task<IActionResult> Delete(Guid ingredientId)
    {
        var price = await _context.IngredientPrices
            .FirstOrDefaultAsync(p => p.IngredientId == ingredientId);

        if (price == null)
            return NotFound();

        // Soft delete
        price.IsActive = false;
        price.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    private async Task<int> UpdateRecipeIngredientCosts(Guid ingredientId, decimal newPricePerUnit)
    {
        var ingredients = await _context.RecipeIngredients
            .Where(ri => ri.IngredientId == ingredientId)
            .Include(ri => ri.Recipe)
            .ToListAsync();

        foreach (var ingredient in ingredients)
        {
            ingredient.CurrentUnitCost = newPricePerUnit;
            var effectiveQty = ingredient.Quantity * (1 + ingredient.WastePercentage / 100);
            ingredient.CurrentLineCost = effectiveQty * newPricePerUnit;
            ingredient.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        // Update recipe totals
        var recipeIds = ingredients.Select(i => i.RecipeId).Distinct();
        foreach (var recipeId in recipeIds)
        {
            var recipe = await _context.Recipes
                .Include(r => r.Ingredients)
                .FirstOrDefaultAsync(r => r.Id == recipeId);

            if (recipe != null)
            {
                var totalCost = recipe.Ingredients.Sum(i => i.CurrentLineCost);
                recipe.CurrentCostPerPortion = recipe.PortionYield > 0 ? totalCost / recipe.PortionYield : totalCost;
                recipe.CostCalculatedAt = DateTime.UtcNow;
                recipe.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();

        return ingredients.Count;
    }

    private static IngredientPriceDto MapToDto(IngredientPrice price)
    {
        return new IngredientPriceDto
        {
            Id = price.Id,
            IngredientId = price.IngredientId,
            IngredientName = price.IngredientName,
            CurrentPrice = price.CurrentPrice,
            UnitOfMeasure = price.UnitOfMeasure,
            PackSize = price.PackSize,
            PricePerUnit = price.PricePerUnit,
            PreferredSupplierId = price.PreferredSupplierId,
            PreferredSupplierName = price.PreferredSupplierName,
            PreviousPrice = price.PreviousPrice,
            PriceChangedAt = price.PriceChangedAt,
            PriceChangePercent = price.PriceChangePercent,
            IsActive = price.IsActive
        };
    }
}
