using DarkVelocity.Menu.Api.Data;
using DarkVelocity.Menu.Api.Dtos;
using DarkVelocity.Menu.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Menu.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/categories")]
public class CategoriesController : ControllerBase
{
    private readonly MenuDbContext _context;

    public CategoriesController(MenuDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<CategoryDto>>> GetAll(Guid locationId)
    {
        var categories = await _context.Categories
            .Where(c => c.LocationId == locationId)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                LocationId = c.LocationId,
                Name = c.Name,
                Description = c.Description,
                DisplayOrder = c.DisplayOrder,
                Color = c.Color,
                IsActive = c.IsActive,
                ItemCount = c.Items.Count(i => i.IsActive)
            })
            .ToListAsync();

        foreach (var category in categories)
        {
            category.AddSelfLink($"/api/locations/{locationId}/categories/{category.Id}");
            category.AddLink("items", $"/api/locations/{locationId}/categories/{category.Id}/items");
        }

        return Ok(HalCollection<CategoryDto>.Create(
            categories,
            $"/api/locations/{locationId}/categories",
            categories.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CategoryDto>> GetById(Guid locationId, Guid id)
    {
        var category = await _context.Categories
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == id && c.LocationId == locationId);

        if (category == null)
            return NotFound();

        var dto = new CategoryDto
        {
            Id = category.Id,
            LocationId = category.LocationId,
            Name = category.Name,
            Description = category.Description,
            DisplayOrder = category.DisplayOrder,
            Color = category.Color,
            IsActive = category.IsActive,
            ItemCount = category.Items.Count(i => i.IsActive)
        };

        dto.AddSelfLink($"/api/locations/{locationId}/categories/{category.Id}");
        dto.AddLink("items", $"/api/locations/{locationId}/categories/{category.Id}/items");
        dto.AddLink("location", $"/api/locations/{locationId}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(Guid locationId, [FromBody] CreateCategoryRequest request)
    {
        var category = new MenuCategory
        {
            LocationId = locationId,
            Name = request.Name,
            Description = request.Description,
            DisplayOrder = request.DisplayOrder,
            Color = request.Color
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        var dto = new CategoryDto
        {
            Id = category.Id,
            LocationId = category.LocationId,
            Name = category.Name,
            Description = category.Description,
            DisplayOrder = category.DisplayOrder,
            Color = category.Color,
            IsActive = category.IsActive,
            ItemCount = 0
        };

        dto.AddSelfLink($"/api/locations/{locationId}/categories/{category.Id}");

        return CreatedAtAction(nameof(GetById), new { locationId, id = category.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CategoryDto>> Update(Guid locationId, Guid id, [FromBody] UpdateCategoryRequest request)
    {
        var category = await _context.Categories
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == id && c.LocationId == locationId);

        if (category == null)
            return NotFound();

        if (request.Name != null)
            category.Name = request.Name;

        if (request.Description != null)
            category.Description = request.Description;

        if (request.DisplayOrder.HasValue)
            category.DisplayOrder = request.DisplayOrder.Value;

        if (request.Color != null)
            category.Color = request.Color;

        if (request.IsActive.HasValue)
            category.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync();

        var dto = new CategoryDto
        {
            Id = category.Id,
            LocationId = category.LocationId,
            Name = category.Name,
            Description = category.Description,
            DisplayOrder = category.DisplayOrder,
            Color = category.Color,
            IsActive = category.IsActive,
            ItemCount = category.Items.Count(i => i.IsActive)
        };

        dto.AddSelfLink($"/api/locations/{locationId}/categories/{category.Id}");

        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid locationId, Guid id)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.LocationId == locationId);

        if (category == null)
            return NotFound();

        category.IsActive = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
