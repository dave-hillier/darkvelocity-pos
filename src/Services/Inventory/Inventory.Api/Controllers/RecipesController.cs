using DarkVelocity.Inventory.Api.Data;
using DarkVelocity.Inventory.Api.Dtos;
using DarkVelocity.Inventory.Api.Entities;
using DarkVelocity.Inventory.Api.Services;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Inventory.Api.Controllers;

[ApiController]
[Route("api/recipes")]
public class RecipesController : ControllerBase
{
    private readonly InventoryDbContext _context;
    private readonly IRecipeCostService _recipeCostService;

    public RecipesController(InventoryDbContext context, IRecipeCostService recipeCostService)
    {
        _context = context;
        _recipeCostService = recipeCostService;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<RecipeDto>>> GetAll()
    {
        var recipes = await _context.Recipes
            .Include(r => r.Ingredients)
                .ThenInclude(ri => ri.Ingredient)
            .Select(r => new RecipeDto
            {
                Id = r.Id,
                Code = r.Code,
                Name = r.Name,
                MenuItemId = r.MenuItemId,
                PortionYield = r.PortionYield,
                Instructions = r.Instructions,
                CalculatedCost = r.CalculatedCost,
                CostPerPortion = r.PortionYield > 0 ? r.CalculatedCost / r.PortionYield : null,
                CostCalculatedAt = r.CostCalculatedAt,
                IsActive = r.IsActive,
                Ingredients = r.Ingredients.Select(ri => new RecipeIngredientDto
                {
                    Id = ri.Id,
                    RecipeId = ri.RecipeId,
                    IngredientId = ri.IngredientId,
                    IngredientName = ri.Ingredient!.Name,
                    IngredientCode = ri.Ingredient.Code,
                    Quantity = ri.Quantity,
                    UnitOfMeasure = ri.UnitOfMeasure ?? ri.Ingredient.UnitOfMeasure,
                    WastePercentage = ri.WastePercentage,
                    EffectiveQuantity = ri.Quantity * (1 + ri.WastePercentage / 100)
                }).ToList()
            })
            .ToListAsync();

        foreach (var recipe in recipes)
        {
            recipe.AddSelfLink($"/api/recipes/{recipe.Id}");
        }

        return Ok(HalCollection<RecipeDto>.Create(
            recipes,
            "/api/recipes",
            recipes.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RecipeDto>> GetById(Guid id)
    {
        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
                .ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (recipe == null)
            return NotFound();

        var dto = new RecipeDto
        {
            Id = recipe.Id,
            Code = recipe.Code,
            Name = recipe.Name,
            MenuItemId = recipe.MenuItemId,
            PortionYield = recipe.PortionYield,
            Instructions = recipe.Instructions,
            CalculatedCost = recipe.CalculatedCost,
            CostPerPortion = recipe.PortionYield > 0 ? recipe.CalculatedCost / recipe.PortionYield : null,
            CostCalculatedAt = recipe.CostCalculatedAt,
            IsActive = recipe.IsActive,
            Ingredients = recipe.Ingredients.Select(ri => new RecipeIngredientDto
            {
                Id = ri.Id,
                RecipeId = ri.RecipeId,
                IngredientId = ri.IngredientId,
                IngredientName = ri.Ingredient!.Name,
                IngredientCode = ri.Ingredient.Code,
                Quantity = ri.Quantity,
                UnitOfMeasure = ri.UnitOfMeasure ?? ri.Ingredient.UnitOfMeasure,
                WastePercentage = ri.WastePercentage,
                EffectiveQuantity = ri.Quantity * (1 + ri.WastePercentage / 100)
            }).ToList()
        };

        dto.AddSelfLink($"/api/recipes/{recipe.Id}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<RecipeDto>> Create([FromBody] CreateRecipeRequest request)
    {
        var existingCode = await _context.Recipes
            .AnyAsync(r => r.Code == request.Code);

        if (existingCode)
            return Conflict(new { message = "Recipe code already exists" });

        var recipe = new Recipe
        {
            Code = request.Code,
            Name = request.Name,
            MenuItemId = request.MenuItemId,
            PortionYield = request.PortionYield,
            Instructions = request.Instructions
        };

        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();

        var dto = new RecipeDto
        {
            Id = recipe.Id,
            Code = recipe.Code,
            Name = recipe.Name,
            MenuItemId = recipe.MenuItemId,
            PortionYield = recipe.PortionYield,
            Instructions = recipe.Instructions,
            IsActive = recipe.IsActive
        };

        dto.AddSelfLink($"/api/recipes/{recipe.Id}");

        return CreatedAtAction(nameof(GetById), new { id = recipe.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RecipeDto>> Update(Guid id, [FromBody] UpdateRecipeRequest request)
    {
        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
                .ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (recipe == null)
            return NotFound();

        if (request.Name != null)
            recipe.Name = request.Name;

        if (request.MenuItemId.HasValue)
            recipe.MenuItemId = request.MenuItemId.Value;

        if (request.PortionYield.HasValue)
            recipe.PortionYield = request.PortionYield.Value;

        if (request.Instructions != null)
            recipe.Instructions = request.Instructions;

        if (request.IsActive.HasValue)
            recipe.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync();

        var dto = new RecipeDto
        {
            Id = recipe.Id,
            Code = recipe.Code,
            Name = recipe.Name,
            MenuItemId = recipe.MenuItemId,
            PortionYield = recipe.PortionYield,
            Instructions = recipe.Instructions,
            CalculatedCost = recipe.CalculatedCost,
            CostPerPortion = recipe.PortionYield > 0 ? recipe.CalculatedCost / recipe.PortionYield : null,
            CostCalculatedAt = recipe.CostCalculatedAt,
            IsActive = recipe.IsActive,
            Ingredients = recipe.Ingredients.Select(ri => new RecipeIngredientDto
            {
                Id = ri.Id,
                RecipeId = ri.RecipeId,
                IngredientId = ri.IngredientId,
                IngredientName = ri.Ingredient!.Name,
                IngredientCode = ri.Ingredient.Code,
                Quantity = ri.Quantity,
                UnitOfMeasure = ri.UnitOfMeasure ?? ri.Ingredient.UnitOfMeasure,
                WastePercentage = ri.WastePercentage,
                EffectiveQuantity = ri.Quantity * (1 + ri.WastePercentage / 100)
            }).ToList()
        };

        dto.AddSelfLink($"/api/recipes/{recipe.Id}");

        return Ok(dto);
    }

    [HttpPost("{id:guid}/ingredients")]
    public async Task<ActionResult<RecipeIngredientDto>> AddIngredient(Guid id, [FromBody] AddRecipeIngredientRequest request)
    {
        var recipe = await _context.Recipes.FindAsync(id);
        if (recipe == null)
            return NotFound();

        var ingredient = await _context.Ingredients.FindAsync(request.IngredientId);
        if (ingredient == null)
            return BadRequest(new { message = "Invalid ingredient" });

        var recipeIngredient = new RecipeIngredient
        {
            RecipeId = id,
            IngredientId = request.IngredientId,
            Quantity = request.Quantity,
            UnitOfMeasure = request.UnitOfMeasure,
            WastePercentage = request.WastePercentage
        };

        _context.RecipeIngredients.Add(recipeIngredient);
        await _context.SaveChangesAsync();

        var dto = new RecipeIngredientDto
        {
            Id = recipeIngredient.Id,
            RecipeId = recipeIngredient.RecipeId,
            IngredientId = recipeIngredient.IngredientId,
            IngredientName = ingredient.Name,
            IngredientCode = ingredient.Code,
            Quantity = recipeIngredient.Quantity,
            UnitOfMeasure = recipeIngredient.UnitOfMeasure ?? ingredient.UnitOfMeasure,
            WastePercentage = recipeIngredient.WastePercentage,
            EffectiveQuantity = recipeIngredient.Quantity * (1 + recipeIngredient.WastePercentage / 100)
        };

        return Created($"/api/recipes/{id}/ingredients/{recipeIngredient.Id}", dto);
    }

    [HttpDelete("{recipeId:guid}/ingredients/{ingredientId:guid}")]
    public async Task<IActionResult> RemoveIngredient(Guid recipeId, Guid ingredientId)
    {
        var recipeIngredient = await _context.RecipeIngredients
            .FirstOrDefaultAsync(ri => ri.RecipeId == recipeId && ri.Id == ingredientId);

        if (recipeIngredient == null)
            return NotFound();

        _context.RecipeIngredients.Remove(recipeIngredient);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:guid}/calculate-cost")]
    public async Task<ActionResult<RecipeDto>> CalculateCost(Guid id, [FromQuery] Guid locationId)
    {
        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
                .ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (recipe == null)
            return NotFound();

        await _recipeCostService.CalculateCostAsync(id, locationId);

        // Reload to get updated cost
        await _context.Entry(recipe).ReloadAsync();

        var dto = new RecipeDto
        {
            Id = recipe.Id,
            Code = recipe.Code,
            Name = recipe.Name,
            MenuItemId = recipe.MenuItemId,
            PortionYield = recipe.PortionYield,
            Instructions = recipe.Instructions,
            CalculatedCost = recipe.CalculatedCost,
            CostPerPortion = recipe.PortionYield > 0 ? recipe.CalculatedCost / recipe.PortionYield : null,
            CostCalculatedAt = recipe.CostCalculatedAt,
            IsActive = recipe.IsActive
        };

        dto.AddSelfLink($"/api/recipes/{recipe.Id}");

        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var recipe = await _context.Recipes.FindAsync(id);

        if (recipe == null)
            return NotFound();

        recipe.IsActive = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
