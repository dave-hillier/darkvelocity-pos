using DarkVelocity.Menu.Api.Data;
using DarkVelocity.Menu.Api.Dtos;
using DarkVelocity.Menu.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Menu.Api.Controllers;

[ApiController]
[Route("api/accounting-groups")]
public class AccountingGroupsController : ControllerBase
{
    private readonly MenuDbContext _context;

    public AccountingGroupsController(MenuDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<AccountingGroupDto>>> GetAll()
    {
        var groups = await _context.AccountingGroups
            .Select(g => new AccountingGroupDto
            {
                Id = g.Id,
                Name = g.Name,
                Description = g.Description,
                TaxRate = g.TaxRate,
                IsActive = g.IsActive
            })
            .ToListAsync();

        foreach (var group in groups)
        {
            group.AddSelfLink($"/api/accounting-groups/{group.Id}");
        }

        return Ok(HalCollection<AccountingGroupDto>.Create(
            groups,
            "/api/accounting-groups",
            groups.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccountingGroupDto>> GetById(Guid id)
    {
        var group = await _context.AccountingGroups.FindAsync(id);

        if (group == null)
            return NotFound();

        var dto = new AccountingGroupDto
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            TaxRate = group.TaxRate,
            IsActive = group.IsActive
        };

        dto.AddSelfLink($"/api/accounting-groups/{group.Id}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<AccountingGroupDto>> Create([FromBody] CreateAccountingGroupRequest request)
    {
        var group = new AccountingGroup
        {
            Name = request.Name,
            Description = request.Description,
            TaxRate = request.TaxRate
        };

        _context.AccountingGroups.Add(group);
        await _context.SaveChangesAsync();

        var dto = new AccountingGroupDto
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            TaxRate = group.TaxRate,
            IsActive = group.IsActive
        };

        dto.AddSelfLink($"/api/accounting-groups/{group.Id}");

        return CreatedAtAction(nameof(GetById), new { id = group.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AccountingGroupDto>> Update(Guid id, [FromBody] UpdateAccountingGroupRequest request)
    {
        var group = await _context.AccountingGroups.FindAsync(id);

        if (group == null)
            return NotFound();

        if (request.Name != null)
            group.Name = request.Name;

        if (request.Description != null)
            group.Description = request.Description;

        if (request.TaxRate.HasValue)
            group.TaxRate = request.TaxRate.Value;

        if (request.IsActive.HasValue)
            group.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync();

        var dto = new AccountingGroupDto
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            TaxRate = group.TaxRate,
            IsActive = group.IsActive
        };

        dto.AddSelfLink($"/api/accounting-groups/{group.Id}");

        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var group = await _context.AccountingGroups.FindAsync(id);

        if (group == null)
            return NotFound();

        group.IsActive = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
