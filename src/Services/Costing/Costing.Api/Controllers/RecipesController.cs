using DarkVelocity.Costing.Api.Data;
using DarkVelocity.Costing.Api.Dtos;
using DarkVelocity.Costing.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Costing.Api.Controllers;

[ApiController]
[Route("api/recipes")]
public class RecipesController : ControllerBase
{
    private readonly CostingDbContext _context;

    public RecipesController(CostingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<RecipeSummaryDto>>> GetAll(
        [FromQuery] bool? isActive = null,
        [FromQuery] Guid? categoryId = null)
    {
        var query = _context.Recipes
            .Include(r => r.Ingredients)
            .AsQueryable();

        if (isActive.HasValue)
            query = query.Where(r => r.IsActive == isActive.Value);

        if (categoryId.HasValue)
            query = query.Where(r => r.CategoryId == categoryId.Value);

        var recipes = await query
            .OrderBy(r => r.MenuItemName)
            .ToListAsync();

        return Ok(recipes.Select(MapToSummaryDto).ToList());
    }

    [HttpGet("{recipeId:guid}")]
    public async Task<ActionResult<RecipeDto>> GetById(Guid recipeId)
    {
        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == recipeId);

        if (recipe == null)
            return NotFound();

        var dto = MapToDto(recipe);
        dto.AddSelfLink($"/api/recipes/{recipeId}");
        dto.AddLink("cost", $"/api/recipes/{recipeId}/cost");
        dto.AddLink("snapshots", $"/api/recipes/{recipeId}/snapshots");

        return Ok(dto);
    }

    [HttpGet("by-menu-item/{menuItemId:guid}")]
    public async Task<ActionResult<RecipeDto>> GetByMenuItem(Guid menuItemId)
    {
        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.MenuItemId == menuItemId);

        if (recipe == null)
            return NotFound();

        var dto = MapToDto(recipe);
        dto.AddSelfLink($"/api/recipes/{recipe.Id}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<RecipeDto>> Create([FromBody] CreateRecipeRequest request)
    {
        // Check for duplicate menu item
        var existingMenuItem = await _context.Recipes
            .AnyAsync(r => r.MenuItemId == request.MenuItemId);

        if (existingMenuItem)
            return Conflict(new { message = "A recipe already exists for this menu item" });

        // Check for duplicate code
        var existingCode = await _context.Recipes
            .AnyAsync(r => r.Code == request.Code);

        if (existingCode)
            return Conflict(new { message = "A recipe with this code already exists" });

        var recipe = new Recipe
        {
            MenuItemId = request.MenuItemId,
            MenuItemName = request.MenuItemName,
            Code = request.Code,
            CategoryId = request.CategoryId,
            CategoryName = request.CategoryName,
            Description = request.Description,
            PortionYield = request.PortionYield,
            PrepInstructions = request.PrepInstructions
        };

        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();

        var dto = MapToDto(recipe);
        dto.AddSelfLink($"/api/recipes/{recipe.Id}");

        return CreatedAtAction(nameof(GetById), new { recipeId = recipe.Id }, dto);
    }

    [HttpPut("{recipeId:guid}")]
    public async Task<ActionResult<RecipeDto>> Update(
        Guid recipeId,
        [FromBody] UpdateRecipeRequest request)
    {
        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == recipeId);

        if (recipe == null)
            return NotFound();

        if (request.Code != null && request.Code != recipe.Code)
        {
            var existingCode = await _context.Recipes
                .AnyAsync(r => r.Code == request.Code && r.Id != recipeId);

            if (existingCode)
                return Conflict(new { message = "A recipe with this code already exists" });

            recipe.Code = request.Code;
        }

        if (request.MenuItemName != null) recipe.MenuItemName = request.MenuItemName;
        if (request.CategoryId.HasValue) recipe.CategoryId = request.CategoryId;
        if (request.CategoryName != null) recipe.CategoryName = request.CategoryName;
        if (request.Description != null) recipe.Description = request.Description;
        if (request.PortionYield.HasValue) recipe.PortionYield = request.PortionYield.Value;
        if (request.PrepInstructions != null) recipe.PrepInstructions = request.PrepInstructions;
        if (request.IsActive.HasValue) recipe.IsActive = request.IsActive.Value;

        recipe.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(recipe);
        dto.AddSelfLink($"/api/recipes/{recipeId}");

        return Ok(dto);
    }

    [HttpDelete("{recipeId:guid}")]
    public async Task<IActionResult> Delete(Guid recipeId)
    {
        var recipe = await _context.Recipes.FindAsync(recipeId);

        if (recipe == null)
            return NotFound();

        // Soft delete
        recipe.IsActive = false;
        recipe.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{recipeId:guid}/cost")]
    public async Task<ActionResult<RecipeCostCalculationDto>> CalculateCost(
        Guid recipeId,
        [FromQuery] decimal? menuPrice = null)
    {
        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == recipeId);

        if (recipe == null)
            return NotFound();

        var ingredientCosts = new List<IngredientCostLineDto>();
        decimal totalCost = 0;

        foreach (var ingredient in recipe.Ingredients)
        {
            // Get current price for ingredient
            var price = await _context.IngredientPrices
                .FirstOrDefaultAsync(p => p.IngredientId == ingredient.IngredientId);

            var unitCost = price?.PricePerUnit ?? ingredient.CurrentUnitCost;
            var effectiveQty = ingredient.Quantity * (1 + ingredient.WastePercentage / 100);
            var lineCost = effectiveQty * unitCost;
            totalCost += lineCost;

            ingredientCosts.Add(new IngredientCostLineDto
            {
                IngredientId = ingredient.IngredientId,
                IngredientName = ingredient.IngredientName,
                Quantity = ingredient.Quantity,
                UnitOfMeasure = ingredient.UnitOfMeasure,
                WastePercentage = ingredient.WastePercentage,
                EffectiveQuantity = effectiveQty,
                UnitCost = unitCost,
                LineCost = lineCost
            });
        }

        // Calculate percentages
        foreach (var line in ingredientCosts)
        {
            line.CostPercentOfTotal = totalCost > 0 ? (line.LineCost / totalCost) * 100 : 0;
        }

        var costPerPortion = recipe.PortionYield > 0 ? totalCost / recipe.PortionYield : totalCost;

        var result = new RecipeCostCalculationDto
        {
            RecipeId = recipe.Id,
            RecipeName = recipe.MenuItemName,
            TotalIngredientCost = totalCost,
            CostPerPortion = costPerPortion,
            PortionYield = recipe.PortionYield,
            MenuPrice = menuPrice,
            IngredientCosts = ingredientCosts.OrderByDescending(i => i.LineCost).ToList()
        };

        if (menuPrice.HasValue && menuPrice.Value > 0)
        {
            result.CostPercentage = (costPerPortion / menuPrice.Value) * 100;
            result.GrossMarginPercent = 100 - result.CostPercentage;
        }

        return Ok(result);
    }

    [HttpPost("{recipeId:guid}/recalculate")]
    public async Task<ActionResult<RecipeDto>> Recalculate(Guid recipeId)
    {
        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == recipeId);

        if (recipe == null)
            return NotFound();

        decimal totalCost = 0;

        foreach (var ingredient in recipe.Ingredients)
        {
            var price = await _context.IngredientPrices
                .FirstOrDefaultAsync(p => p.IngredientId == ingredient.IngredientId);

            if (price != null)
            {
                ingredient.CurrentUnitCost = price.PricePerUnit;
            }

            var effectiveQty = ingredient.Quantity * (1 + ingredient.WastePercentage / 100);
            ingredient.CurrentLineCost = effectiveQty * ingredient.CurrentUnitCost;
            totalCost += ingredient.CurrentLineCost;
        }

        recipe.CurrentCostPerPortion = recipe.PortionYield > 0 ? totalCost / recipe.PortionYield : totalCost;
        recipe.CostCalculatedAt = DateTime.UtcNow;
        recipe.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(recipe);
        dto.AddSelfLink($"/api/recipes/{recipeId}");

        return Ok(dto);
    }

    private static RecipeDto MapToDto(Recipe recipe)
    {
        return new RecipeDto
        {
            Id = recipe.Id,
            MenuItemId = recipe.MenuItemId,
            MenuItemName = recipe.MenuItemName,
            CategoryId = recipe.CategoryId,
            CategoryName = recipe.CategoryName,
            Code = recipe.Code,
            Description = recipe.Description,
            PortionYield = recipe.PortionYield,
            PrepInstructions = recipe.PrepInstructions,
            CurrentCostPerPortion = recipe.CurrentCostPerPortion,
            CostCalculatedAt = recipe.CostCalculatedAt,
            IsActive = recipe.IsActive,
            Ingredients = recipe.Ingredients.Select(i => new RecipeIngredientDto
            {
                Id = i.Id,
                RecipeId = i.RecipeId,
                IngredientId = i.IngredientId,
                IngredientName = i.IngredientName,
                Quantity = i.Quantity,
                UnitOfMeasure = i.UnitOfMeasure,
                WastePercentage = i.WastePercentage,
                CurrentUnitCost = i.CurrentUnitCost,
                CurrentLineCost = i.CurrentLineCost
            }).ToList()
        };
    }

    private static RecipeSummaryDto MapToSummaryDto(Recipe recipe)
    {
        return new RecipeSummaryDto
        {
            Id = recipe.Id,
            MenuItemId = recipe.MenuItemId,
            MenuItemName = recipe.MenuItemName,
            Code = recipe.Code,
            CurrentCostPerPortion = recipe.CurrentCostPerPortion,
            IngredientCount = recipe.Ingredients.Count,
            IsActive = recipe.IsActive
        };
    }
}
