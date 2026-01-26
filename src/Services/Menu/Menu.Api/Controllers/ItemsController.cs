using DarkVelocity.Menu.Api.Data;
using DarkVelocity.Menu.Api.Dtos;
using DarkVelocity.Menu.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Menu.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/items")]
public class ItemsController : ControllerBase
{
    private readonly MenuDbContext _context;

    public ItemsController(MenuDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<MenuItemDto>>> GetAll(Guid locationId, [FromQuery] Guid? categoryId = null)
    {
        var query = _context.Items
            .Include(i => i.Category)
            .Include(i => i.AccountingGroup)
            .Where(i => i.LocationId == locationId);

        if (categoryId.HasValue)
        {
            query = query.Where(i => i.CategoryId == categoryId.Value);
        }

        var items = await query
            .Select(i => new MenuItemDto
            {
                Id = i.Id,
                LocationId = i.LocationId,
                CategoryId = i.CategoryId,
                AccountingGroupId = i.AccountingGroupId,
                RecipeId = i.RecipeId,
                Name = i.Name,
                Description = i.Description,
                Price = i.Price,
                ImageUrl = i.ImageUrl,
                Sku = i.Sku,
                IsActive = i.IsActive,
                TrackInventory = i.TrackInventory,
                CategoryName = i.Category!.Name,
                AccountingGroupName = i.AccountingGroup!.Name,
                TaxRate = i.AccountingGroup.TaxRate
            })
            .ToListAsync();

        foreach (var item in items)
        {
            item.AddSelfLink($"/api/locations/{locationId}/items/{item.Id}");
            item.AddLink("category", $"/api/locations/{locationId}/categories/{item.CategoryId}");
        }

        return Ok(HalCollection<MenuItemDto>.Create(
            items,
            $"/api/locations/{locationId}/items",
            items.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MenuItemDto>> GetById(Guid locationId, Guid id)
    {
        var item = await _context.Items
            .Include(i => i.Category)
            .Include(i => i.AccountingGroup)
            .FirstOrDefaultAsync(i => i.Id == id && i.LocationId == locationId);

        if (item == null)
            return NotFound();

        var dto = new MenuItemDto
        {
            Id = item.Id,
            LocationId = item.LocationId,
            CategoryId = item.CategoryId,
            AccountingGroupId = item.AccountingGroupId,
            RecipeId = item.RecipeId,
            Name = item.Name,
            Description = item.Description,
            Price = item.Price,
            ImageUrl = item.ImageUrl,
            Sku = item.Sku,
            IsActive = item.IsActive,
            TrackInventory = item.TrackInventory,
            CategoryName = item.Category!.Name,
            AccountingGroupName = item.AccountingGroup!.Name,
            TaxRate = item.AccountingGroup.TaxRate
        };

        dto.AddSelfLink($"/api/locations/{locationId}/items/{item.Id}");
        dto.AddLink("category", $"/api/locations/{locationId}/categories/{item.CategoryId}");
        dto.AddLink("accounting-group", $"/api/accounting-groups/{item.AccountingGroupId}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<MenuItemDto>> Create(Guid locationId, [FromBody] CreateMenuItemRequest request)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.LocationId == locationId);

        if (category == null)
            return BadRequest(new { message = "Invalid category" });

        var accountingGroup = await _context.AccountingGroups.FindAsync(request.AccountingGroupId);
        if (accountingGroup == null)
            return BadRequest(new { message = "Invalid accounting group" });

        if (!string.IsNullOrEmpty(request.Sku))
        {
            var existingSku = await _context.Items
                .AnyAsync(i => i.LocationId == locationId && i.Sku == request.Sku);

            if (existingSku)
                return Conflict(new { message = "SKU already exists" });
        }

        var item = new MenuItem
        {
            LocationId = locationId,
            CategoryId = request.CategoryId,
            AccountingGroupId = request.AccountingGroupId,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            ImageUrl = request.ImageUrl,
            Sku = request.Sku,
            RecipeId = request.RecipeId,
            TrackInventory = request.TrackInventory
        };

        _context.Items.Add(item);
        await _context.SaveChangesAsync();

        var dto = new MenuItemDto
        {
            Id = item.Id,
            LocationId = item.LocationId,
            CategoryId = item.CategoryId,
            AccountingGroupId = item.AccountingGroupId,
            RecipeId = item.RecipeId,
            Name = item.Name,
            Description = item.Description,
            Price = item.Price,
            ImageUrl = item.ImageUrl,
            Sku = item.Sku,
            IsActive = item.IsActive,
            TrackInventory = item.TrackInventory,
            CategoryName = category.Name,
            AccountingGroupName = accountingGroup.Name,
            TaxRate = accountingGroup.TaxRate
        };

        dto.AddSelfLink($"/api/locations/{locationId}/items/{item.Id}");

        return CreatedAtAction(nameof(GetById), new { locationId, id = item.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MenuItemDto>> Update(Guid locationId, Guid id, [FromBody] UpdateMenuItemRequest request)
    {
        var item = await _context.Items
            .Include(i => i.Category)
            .Include(i => i.AccountingGroup)
            .FirstOrDefaultAsync(i => i.Id == id && i.LocationId == locationId);

        if (item == null)
            return NotFound();

        if (request.Name != null)
            item.Name = request.Name;

        if (request.Description != null)
            item.Description = request.Description;

        if (request.Price.HasValue)
            item.Price = request.Price.Value;

        if (request.ImageUrl != null)
            item.ImageUrl = request.ImageUrl;

        if (request.Sku != null)
        {
            var existingSku = await _context.Items
                .AnyAsync(i => i.LocationId == locationId && i.Sku == request.Sku && i.Id != id);

            if (existingSku)
                return Conflict(new { message = "SKU already exists" });

            item.Sku = request.Sku;
        }

        if (request.CategoryId.HasValue)
        {
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId.Value && c.LocationId == locationId);

            if (category == null)
                return BadRequest(new { message = "Invalid category" });

            item.CategoryId = request.CategoryId.Value;
        }

        if (request.AccountingGroupId.HasValue)
        {
            var accountingGroup = await _context.AccountingGroups.FindAsync(request.AccountingGroupId.Value);
            if (accountingGroup == null)
                return BadRequest(new { message = "Invalid accounting group" });

            item.AccountingGroupId = request.AccountingGroupId.Value;
        }

        if (request.RecipeId.HasValue)
            item.RecipeId = request.RecipeId.Value;

        if (request.TrackInventory.HasValue)
            item.TrackInventory = request.TrackInventory.Value;

        if (request.IsActive.HasValue)
            item.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync();

        await _context.Entry(item).Reference(i => i.Category).LoadAsync();
        await _context.Entry(item).Reference(i => i.AccountingGroup).LoadAsync();

        var dto = new MenuItemDto
        {
            Id = item.Id,
            LocationId = item.LocationId,
            CategoryId = item.CategoryId,
            AccountingGroupId = item.AccountingGroupId,
            RecipeId = item.RecipeId,
            Name = item.Name,
            Description = item.Description,
            Price = item.Price,
            ImageUrl = item.ImageUrl,
            Sku = item.Sku,
            IsActive = item.IsActive,
            TrackInventory = item.TrackInventory,
            CategoryName = item.Category!.Name,
            AccountingGroupName = item.AccountingGroup!.Name,
            TaxRate = item.AccountingGroup.TaxRate
        };

        dto.AddSelfLink($"/api/locations/{locationId}/items/{item.Id}");

        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid locationId, Guid id)
    {
        var item = await _context.Items
            .FirstOrDefaultAsync(i => i.Id == id && i.LocationId == locationId);

        if (item == null)
            return NotFound();

        item.IsActive = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

[ApiController]
[Route("api/locations/{locationId:guid}/categories/{categoryId:guid}/items")]
public class CategoryItemsController : ControllerBase
{
    private readonly MenuDbContext _context;

    public CategoryItemsController(MenuDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<MenuItemDto>>> GetCategoryItems(Guid locationId, Guid categoryId)
    {
        var items = await _context.Items
            .Include(i => i.Category)
            .Include(i => i.AccountingGroup)
            .Where(i => i.LocationId == locationId && i.CategoryId == categoryId)
            .Select(i => new MenuItemDto
            {
                Id = i.Id,
                LocationId = i.LocationId,
                CategoryId = i.CategoryId,
                AccountingGroupId = i.AccountingGroupId,
                RecipeId = i.RecipeId,
                Name = i.Name,
                Description = i.Description,
                Price = i.Price,
                ImageUrl = i.ImageUrl,
                Sku = i.Sku,
                IsActive = i.IsActive,
                TrackInventory = i.TrackInventory,
                CategoryName = i.Category!.Name,
                AccountingGroupName = i.AccountingGroup!.Name,
                TaxRate = i.AccountingGroup.TaxRate
            })
            .ToListAsync();

        foreach (var item in items)
        {
            item.AddSelfLink($"/api/locations/{locationId}/items/{item.Id}");
        }

        return Ok(HalCollection<MenuItemDto>.Create(
            items,
            $"/api/locations/{locationId}/categories/{categoryId}/items",
            items.Count
        ));
    }
}
