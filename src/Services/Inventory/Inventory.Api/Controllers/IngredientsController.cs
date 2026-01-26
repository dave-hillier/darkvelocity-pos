using DarkVelocity.Inventory.Api.Data;
using DarkVelocity.Inventory.Api.Dtos;
using DarkVelocity.Inventory.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Inventory.Api.Controllers;

[ApiController]
[Route("api/ingredients")]
public class IngredientsController : ControllerBase
{
    private readonly InventoryDbContext _context;

    public IngredientsController(InventoryDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<IngredientDto>>> GetAll([FromQuery] string? category = null)
    {
        var query = _context.Ingredients.AsQueryable();

        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(i => i.Category == category);
        }

        var ingredients = await query
            .Select(i => new IngredientDto
            {
                Id = i.Id,
                Code = i.Code,
                Name = i.Name,
                UnitOfMeasure = i.UnitOfMeasure,
                Category = i.Category,
                StorageType = i.StorageType,
                ReorderLevel = i.ReorderLevel,
                ReorderQuantity = i.ReorderQuantity,
                CurrentStock = i.CurrentStock,
                IsActive = i.IsActive,
                IsLowStock = (i.CurrentStock ?? 0) <= i.ReorderLevel
            })
            .ToListAsync();

        foreach (var ingredient in ingredients)
        {
            ingredient.AddSelfLink($"/api/ingredients/{ingredient.Id}");
        }

        return Ok(HalCollection<IngredientDto>.Create(
            ingredients,
            "/api/ingredients",
            ingredients.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<IngredientDto>> GetById(Guid id)
    {
        var ingredient = await _context.Ingredients.FindAsync(id);

        if (ingredient == null)
            return NotFound();

        var dto = new IngredientDto
        {
            Id = ingredient.Id,
            Code = ingredient.Code,
            Name = ingredient.Name,
            UnitOfMeasure = ingredient.UnitOfMeasure,
            Category = ingredient.Category,
            StorageType = ingredient.StorageType,
            ReorderLevel = ingredient.ReorderLevel,
            ReorderQuantity = ingredient.ReorderQuantity,
            CurrentStock = ingredient.CurrentStock,
            IsActive = ingredient.IsActive,
            IsLowStock = (ingredient.CurrentStock ?? 0) <= ingredient.ReorderLevel
        };

        dto.AddSelfLink($"/api/ingredients/{ingredient.Id}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<IngredientDto>> Create([FromBody] CreateIngredientRequest request)
    {
        var existingCode = await _context.Ingredients
            .AnyAsync(i => i.Code == request.Code);

        if (existingCode)
            return Conflict(new { message = "Ingredient code already exists" });

        var ingredient = new Ingredient
        {
            Code = request.Code,
            Name = request.Name,
            UnitOfMeasure = request.UnitOfMeasure,
            Category = request.Category,
            StorageType = request.StorageType,
            ReorderLevel = request.ReorderLevel,
            ReorderQuantity = request.ReorderQuantity,
            CurrentStock = 0
        };

        _context.Ingredients.Add(ingredient);
        await _context.SaveChangesAsync();

        var dto = new IngredientDto
        {
            Id = ingredient.Id,
            Code = ingredient.Code,
            Name = ingredient.Name,
            UnitOfMeasure = ingredient.UnitOfMeasure,
            Category = ingredient.Category,
            StorageType = ingredient.StorageType,
            ReorderLevel = ingredient.ReorderLevel,
            ReorderQuantity = ingredient.ReorderQuantity,
            CurrentStock = ingredient.CurrentStock,
            IsActive = ingredient.IsActive,
            IsLowStock = (ingredient.CurrentStock ?? 0) <= ingredient.ReorderLevel
        };

        dto.AddSelfLink($"/api/ingredients/{ingredient.Id}");

        return CreatedAtAction(nameof(GetById), new { id = ingredient.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<IngredientDto>> Update(Guid id, [FromBody] UpdateIngredientRequest request)
    {
        var ingredient = await _context.Ingredients.FindAsync(id);

        if (ingredient == null)
            return NotFound();

        if (request.Name != null)
            ingredient.Name = request.Name;

        if (request.UnitOfMeasure != null)
            ingredient.UnitOfMeasure = request.UnitOfMeasure;

        if (request.Category != null)
            ingredient.Category = request.Category;

        if (request.StorageType != null)
            ingredient.StorageType = request.StorageType;

        if (request.ReorderLevel.HasValue)
            ingredient.ReorderLevel = request.ReorderLevel.Value;

        if (request.ReorderQuantity.HasValue)
            ingredient.ReorderQuantity = request.ReorderQuantity.Value;

        if (request.IsActive.HasValue)
            ingredient.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync();

        var dto = new IngredientDto
        {
            Id = ingredient.Id,
            Code = ingredient.Code,
            Name = ingredient.Name,
            UnitOfMeasure = ingredient.UnitOfMeasure,
            Category = ingredient.Category,
            StorageType = ingredient.StorageType,
            ReorderLevel = ingredient.ReorderLevel,
            ReorderQuantity = ingredient.ReorderQuantity,
            CurrentStock = ingredient.CurrentStock,
            IsActive = ingredient.IsActive,
            IsLowStock = (ingredient.CurrentStock ?? 0) <= ingredient.ReorderLevel
        };

        dto.AddSelfLink($"/api/ingredients/{ingredient.Id}");

        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ingredient = await _context.Ingredients.FindAsync(id);

        if (ingredient == null)
            return NotFound();

        ingredient.IsActive = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
