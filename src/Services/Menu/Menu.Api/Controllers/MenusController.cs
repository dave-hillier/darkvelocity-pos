using DarkVelocity.Menu.Api.Data;
using DarkVelocity.Menu.Api.Dtos;
using DarkVelocity.Menu.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Menu.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/menus")]
public class MenusController : ControllerBase
{
    private readonly MenuDbContext _context;

    public MenusController(MenuDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<MenuDto>>> GetAll(Guid locationId)
    {
        var menus = await _context.Menus
            .Where(m => m.LocationId == locationId)
            .Select(m => new MenuDto
            {
                Id = m.Id,
                LocationId = m.LocationId,
                Name = m.Name,
                Description = m.Description,
                IsDefault = m.IsDefault,
                IsActive = m.IsActive
            })
            .ToListAsync();

        foreach (var menu in menus)
        {
            menu.AddSelfLink($"/api/locations/{locationId}/menus/{menu.Id}");
            menu.AddLink("screens", $"/api/locations/{locationId}/menus/{menu.Id}/screens");
        }

        return Ok(HalCollection<MenuDto>.Create(
            menus,
            $"/api/locations/{locationId}/menus",
            menus.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MenuDto>> GetById(Guid locationId, Guid id, [FromQuery] bool includeScreens = false)
    {
        var query = _context.Menus.Where(m => m.Id == id && m.LocationId == locationId);

        if (includeScreens)
        {
            query = query
                .Include(m => m.Screens)
                    .ThenInclude(s => s.Buttons)
                        .ThenInclude(b => b.Item)
                            .ThenInclude(i => i!.Category)
                .Include(m => m.Screens)
                    .ThenInclude(s => s.Buttons)
                        .ThenInclude(b => b.Item)
                            .ThenInclude(i => i!.AccountingGroup);
        }

        var menu = await query.FirstOrDefaultAsync();

        if (menu == null)
            return NotFound();

        var dto = new MenuDto
        {
            Id = menu.Id,
            LocationId = menu.LocationId,
            Name = menu.Name,
            Description = menu.Description,
            IsDefault = menu.IsDefault,
            IsActive = menu.IsActive
        };

        if (includeScreens)
        {
            dto.Screens = menu.Screens.OrderBy(s => s.Position).Select(s => new MenuScreenDto
            {
                Id = s.Id,
                MenuId = s.MenuId,
                Name = s.Name,
                Position = s.Position,
                Color = s.Color,
                Rows = s.Rows,
                Columns = s.Columns,
                Buttons = s.Buttons.Select(b => new MenuButtonDto
                {
                    Id = b.Id,
                    ScreenId = b.ScreenId,
                    ItemId = b.ItemId,
                    Row = b.Row,
                    Column = b.Column,
                    RowSpan = b.RowSpan,
                    ColumnSpan = b.ColumnSpan,
                    Label = b.Label,
                    Color = b.Color,
                    ButtonType = b.ButtonType,
                    Item = b.Item != null ? new MenuItemDto
                    {
                        Id = b.Item.Id,
                        LocationId = b.Item.LocationId,
                        CategoryId = b.Item.CategoryId,
                        AccountingGroupId = b.Item.AccountingGroupId,
                        Name = b.Item.Name,
                        Description = b.Item.Description,
                        Price = b.Item.Price,
                        IsActive = b.Item.IsActive,
                        CategoryName = b.Item.Category?.Name,
                        AccountingGroupName = b.Item.AccountingGroup?.Name,
                        TaxRate = b.Item.AccountingGroup?.TaxRate
                    } : null
                }).ToList()
            }).ToList();
        }

        dto.AddSelfLink($"/api/locations/{locationId}/menus/{menu.Id}");
        dto.AddLink("screens", $"/api/locations/{locationId}/menus/{menu.Id}/screens");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<MenuDto>> Create(Guid locationId, [FromBody] CreateMenuRequest request)
    {
        if (request.IsDefault)
        {
            var existingDefault = await _context.Menus
                .FirstOrDefaultAsync(m => m.LocationId == locationId && m.IsDefault);

            if (existingDefault != null)
            {
                existingDefault.IsDefault = false;
            }
        }

        var menu = new MenuDefinition
        {
            LocationId = locationId,
            Name = request.Name,
            Description = request.Description,
            IsDefault = request.IsDefault
        };

        _context.Menus.Add(menu);
        await _context.SaveChangesAsync();

        var dto = new MenuDto
        {
            Id = menu.Id,
            LocationId = menu.LocationId,
            Name = menu.Name,
            Description = menu.Description,
            IsDefault = menu.IsDefault,
            IsActive = menu.IsActive
        };

        dto.AddSelfLink($"/api/locations/{locationId}/menus/{menu.Id}");

        return CreatedAtAction(nameof(GetById), new { locationId, id = menu.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MenuDto>> Update(Guid locationId, Guid id, [FromBody] UpdateMenuRequest request)
    {
        var menu = await _context.Menus
            .FirstOrDefaultAsync(m => m.Id == id && m.LocationId == locationId);

        if (menu == null)
            return NotFound();

        if (request.Name != null)
            menu.Name = request.Name;

        if (request.Description != null)
            menu.Description = request.Description;

        if (request.IsDefault == true && !menu.IsDefault)
        {
            var existingDefault = await _context.Menus
                .FirstOrDefaultAsync(m => m.LocationId == locationId && m.IsDefault && m.Id != id);

            if (existingDefault != null)
            {
                existingDefault.IsDefault = false;
            }

            menu.IsDefault = true;
        }
        else if (request.IsDefault == false)
        {
            menu.IsDefault = false;
        }

        if (request.IsActive.HasValue)
            menu.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync();

        var dto = new MenuDto
        {
            Id = menu.Id,
            LocationId = menu.LocationId,
            Name = menu.Name,
            Description = menu.Description,
            IsDefault = menu.IsDefault,
            IsActive = menu.IsActive
        };

        dto.AddSelfLink($"/api/locations/{locationId}/menus/{menu.Id}");

        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid locationId, Guid id)
    {
        var menu = await _context.Menus
            .FirstOrDefaultAsync(m => m.Id == id && m.LocationId == locationId);

        if (menu == null)
            return NotFound();

        menu.IsActive = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:guid}/screens")]
    public async Task<ActionResult<MenuScreenDto>> AddScreen(Guid locationId, Guid id, [FromBody] CreateScreenRequest request)
    {
        var menu = await _context.Menus
            .FirstOrDefaultAsync(m => m.Id == id && m.LocationId == locationId);

        if (menu == null)
            return NotFound();

        var screen = new MenuScreen
        {
            MenuId = id,
            Name = request.Name,
            Position = request.Position,
            Color = request.Color,
            Rows = request.Rows,
            Columns = request.Columns
        };

        _context.Screens.Add(screen);
        await _context.SaveChangesAsync();

        var dto = new MenuScreenDto
        {
            Id = screen.Id,
            MenuId = screen.MenuId,
            Name = screen.Name,
            Position = screen.Position,
            Color = screen.Color,
            Rows = screen.Rows,
            Columns = screen.Columns
        };

        dto.AddSelfLink($"/api/locations/{locationId}/menus/{id}/screens/{screen.Id}");

        return Created($"/api/locations/{locationId}/menus/{id}/screens/{screen.Id}", dto);
    }

    [HttpPost("{menuId:guid}/screens/{screenId:guid}/buttons")]
    public async Task<ActionResult<MenuButtonDto>> AddButton(
        Guid locationId,
        Guid menuId,
        Guid screenId,
        [FromBody] CreateButtonRequest request)
    {
        var screen = await _context.Screens
            .Include(s => s.Menu)
            .FirstOrDefaultAsync(s => s.Id == screenId && s.MenuId == menuId && s.Menu!.LocationId == locationId);

        if (screen == null)
            return NotFound();

        if (request.ItemId.HasValue)
        {
            var item = await _context.Items
                .FirstOrDefaultAsync(i => i.Id == request.ItemId && i.LocationId == locationId);

            if (item == null)
                return BadRequest(new { message = "Invalid item" });
        }

        var button = new MenuButton
        {
            ScreenId = screenId,
            ItemId = request.ItemId,
            Row = request.Row,
            Column = request.Column,
            RowSpan = request.RowSpan,
            ColumnSpan = request.ColumnSpan,
            Label = request.Label,
            Color = request.Color,
            ButtonType = request.ButtonType
        };

        _context.Buttons.Add(button);
        await _context.SaveChangesAsync();

        var dto = new MenuButtonDto
        {
            Id = button.Id,
            ScreenId = button.ScreenId,
            ItemId = button.ItemId,
            Row = button.Row,
            Column = button.Column,
            RowSpan = button.RowSpan,
            ColumnSpan = button.ColumnSpan,
            Label = button.Label,
            Color = button.Color,
            ButtonType = button.ButtonType
        };

        return Created($"/api/locations/{locationId}/menus/{menuId}/screens/{screenId}/buttons/{button.Id}", dto);
    }
}
