using DarkVelocity.Costing.Api.Data;
using DarkVelocity.Costing.Api.Dtos;
using DarkVelocity.Costing.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Costing.Api.Controllers;

[ApiController]
[Route("api/recipes/{recipeId:guid}/ingredients")]
public class RecipeIngredientsController : ControllerBase
{
    private readonly CostingDbContext _context;

    public RecipeIngredientsController(CostingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<RecipeIngredientDto>>> GetAll(Guid recipeId)
    {
        var recipeExists = await _context.Recipes.AnyAsync(r => r.Id == recipeId);
        if (!recipeExists)
            return NotFound(new { message = "Recipe not found" });

        var ingredients = await _context.RecipeIngredients
            .Where(i => i.RecipeId == recipeId)
            .OrderBy(i => i.IngredientName)
            .ToListAsync();

        return Ok(ingredients.Select(MapToDto).ToList());
    }

    [HttpGet("{ingredientId:guid}")]
    public async Task<ActionResult<RecipeIngredientDto>> GetById(Guid recipeId, Guid ingredientId)
    {
        var ingredient = await _context.RecipeIngredients
            .FirstOrDefaultAsync(i => i.RecipeId == recipeId && i.IngredientId == ingredientId);

        if (ingredient == null)
            return NotFound();

        var dto = MapToDto(ingredient);
        dto.AddSelfLink($"/api/recipes/{recipeId}/ingredients/{ingredientId}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<RecipeIngredientDto>> Add(
        Guid recipeId,
        [FromBody] AddRecipeIngredientRequest request)
    {
        var recipe = await _context.Recipes.FindAsync(recipeId);
        if (recipe == null)
            return NotFound(new { message = "Recipe not found" });

        // Check for duplicate ingredient
        var existing = await _context.RecipeIngredients
            .AnyAsync(i => i.RecipeId == recipeId && i.IngredientId == request.IngredientId);

        if (existing)
            return Conflict(new { message = "This ingredient is already in the recipe" });

        // Get current price for ingredient
        var price = await _context.IngredientPrices
            .FirstOrDefaultAsync(p => p.IngredientId == request.IngredientId);

        var unitCost = price?.PricePerUnit ?? 0;
        var effectiveQty = request.Quantity * (1 + request.WastePercentage / 100);

        var ingredient = new RecipeIngredient
        {
            RecipeId = recipeId,
            IngredientId = request.IngredientId,
            IngredientName = request.IngredientName,
            Quantity = request.Quantity,
            UnitOfMeasure = request.UnitOfMeasure,
            WastePercentage = request.WastePercentage,
            CurrentUnitCost = unitCost,
            CurrentLineCost = effectiveQty * unitCost
        };

        _context.RecipeIngredients.Add(ingredient);
        await _context.SaveChangesAsync();

        // Update recipe cost
        await RecalculateRecipeCost(recipeId);

        var dto = MapToDto(ingredient);
        dto.AddSelfLink($"/api/recipes/{recipeId}/ingredients/{request.IngredientId}");

        return CreatedAtAction(nameof(GetById),
            new { recipeId, ingredientId = request.IngredientId }, dto);
    }

    [HttpPut("{ingredientId:guid}")]
    public async Task<ActionResult<RecipeIngredientDto>> Update(
        Guid recipeId,
        Guid ingredientId,
        [FromBody] UpdateRecipeIngredientRequest request)
    {
        var ingredient = await _context.RecipeIngredients
            .FirstOrDefaultAsync(i => i.RecipeId == recipeId && i.IngredientId == ingredientId);

        if (ingredient == null)
            return NotFound();

        if (request.Quantity.HasValue)
            ingredient.Quantity = request.Quantity.Value;

        if (request.WastePercentage.HasValue)
            ingredient.WastePercentage = request.WastePercentage.Value;

        // Recalculate line cost
        var effectiveQty = ingredient.Quantity * (1 + ingredient.WastePercentage / 100);
        ingredient.CurrentLineCost = effectiveQty * ingredient.CurrentUnitCost;
        ingredient.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Update recipe cost
        await RecalculateRecipeCost(recipeId);

        var dto = MapToDto(ingredient);
        dto.AddSelfLink($"/api/recipes/{recipeId}/ingredients/{ingredientId}");

        return Ok(dto);
    }

    [HttpDelete("{ingredientId:guid}")]
    public async Task<IActionResult> Remove(Guid recipeId, Guid ingredientId)
    {
        var ingredient = await _context.RecipeIngredients
            .FirstOrDefaultAsync(i => i.RecipeId == recipeId && i.IngredientId == ingredientId);

        if (ingredient == null)
            return NotFound();

        _context.RecipeIngredients.Remove(ingredient);
        await _context.SaveChangesAsync();

        // Update recipe cost
        await RecalculateRecipeCost(recipeId);

        return NoContent();
    }

    private async Task RecalculateRecipeCost(Guid recipeId)
    {
        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == recipeId);

        if (recipe == null) return;

        var totalCost = recipe.Ingredients.Sum(i => i.CurrentLineCost);
        recipe.CurrentCostPerPortion = recipe.PortionYield > 0 ? totalCost / recipe.PortionYield : totalCost;
        recipe.CostCalculatedAt = DateTime.UtcNow;
        recipe.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    private static RecipeIngredientDto MapToDto(RecipeIngredient ingredient)
    {
        return new RecipeIngredientDto
        {
            Id = ingredient.Id,
            RecipeId = ingredient.RecipeId,
            IngredientId = ingredient.IngredientId,
            IngredientName = ingredient.IngredientName,
            Quantity = ingredient.Quantity,
            UnitOfMeasure = ingredient.UnitOfMeasure,
            WastePercentage = ingredient.WastePercentage,
            CurrentUnitCost = ingredient.CurrentUnitCost,
            CurrentLineCost = ingredient.CurrentLineCost
        };
    }
}
