using DarkVelocity.Hardware.Api.Data;
using DarkVelocity.Hardware.Api.Dtos;
using DarkVelocity.Hardware.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Hardware.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/cash-drawers")]
public class CashDrawersController : ControllerBase
{
    private readonly HardwareDbContext _context;

    public CashDrawersController(HardwareDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<CashDrawerDto>>> GetAll(
        Guid locationId,
        [FromQuery] bool activeOnly = false)
    {
        var query = _context.CashDrawers
            .Include(d => d.Printer)
            .Where(d => d.LocationId == locationId)
            .AsQueryable();

        if (activeOnly)
            query = query.Where(d => d.IsActive);

        var drawers = await query
            .OrderBy(d => d.Name)
            .Select(d => new CashDrawerDto
            {
                Id = d.Id,
                LocationId = d.LocationId,
                Name = d.Name,
                PrinterId = d.PrinterId,
                PrinterName = d.Printer != null ? d.Printer.Name : null,
                ConnectionType = d.ConnectionType,
                IpAddress = d.IpAddress,
                Port = d.Port,
                IsActive = d.IsActive,
                KickPulsePin = d.KickPulsePin,
                KickPulseOnTime = d.KickPulseOnTime,
                KickPulseOffTime = d.KickPulseOffTime
            })
            .ToListAsync();

        foreach (var drawer in drawers)
        {
            drawer.AddSelfLink($"/api/locations/{locationId}/cash-drawers/{drawer.Id}");
        }

        return Ok(HalCollection<CashDrawerDto>.Create(
            drawers,
            $"/api/locations/{locationId}/cash-drawers",
            drawers.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CashDrawerDto>> GetById(Guid locationId, Guid id)
    {
        var drawer = await _context.CashDrawers
            .Include(d => d.Printer)
            .FirstOrDefaultAsync(d => d.LocationId == locationId && d.Id == id);

        if (drawer == null)
            return NotFound();

        var dto = MapToDto(drawer);
        dto.AddSelfLink($"/api/locations/{locationId}/cash-drawers/{drawer.Id}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<CashDrawerDto>> Create(
        Guid locationId,
        [FromBody] CreateCashDrawerRequest request)
    {
        // Check for duplicate name
        var exists = await _context.CashDrawers
            .AnyAsync(d => d.LocationId == locationId && d.Name == request.Name);

        if (exists)
            return Conflict(new { message = "A cash drawer with this name already exists" });

        // Validate printer if provided
        if (request.PrinterId.HasValue)
        {
            var printer = await _context.Printers
                .FirstOrDefaultAsync(p => p.LocationId == locationId && p.Id == request.PrinterId.Value);

            if (printer == null)
                return BadRequest(new { message = "Invalid printer" });

            if (!printer.SupportsCashDrawer)
                return BadRequest(new { message = "Printer does not support cash drawer" });
        }

        var drawer = new CashDrawer
        {
            LocationId = locationId,
            Name = request.Name,
            PrinterId = request.PrinterId,
            ConnectionType = request.PrinterId.HasValue ? "printer" : request.ConnectionType,
            IpAddress = request.IpAddress,
            Port = request.Port,
            KickPulsePin = request.KickPulsePin,
            KickPulseOnTime = request.KickPulseOnTime,
            KickPulseOffTime = request.KickPulseOffTime
        };

        _context.CashDrawers.Add(drawer);
        await _context.SaveChangesAsync();

        // Reload with printer
        if (drawer.PrinterId.HasValue)
        {
            await _context.Entry(drawer).Reference(d => d.Printer).LoadAsync();
        }

        var dto = MapToDto(drawer);
        dto.AddSelfLink($"/api/locations/{locationId}/cash-drawers/{drawer.Id}");

        return CreatedAtAction(nameof(GetById), new { locationId, id = drawer.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CashDrawerDto>> Update(
        Guid locationId,
        Guid id,
        [FromBody] UpdateCashDrawerRequest request)
    {
        var drawer = await _context.CashDrawers
            .Include(d => d.Printer)
            .FirstOrDefaultAsync(d => d.LocationId == locationId && d.Id == id);

        if (drawer == null)
            return NotFound();

        if (request.Name != null)
        {
            var duplicate = await _context.CashDrawers
                .AnyAsync(d => d.LocationId == locationId && d.Name == request.Name && d.Id != id);

            if (duplicate)
                return Conflict(new { message = "A cash drawer with this name already exists" });

            drawer.Name = request.Name;
        }

        if (request.PrinterId.HasValue)
        {
            var printer = await _context.Printers
                .FirstOrDefaultAsync(p => p.LocationId == locationId && p.Id == request.PrinterId.Value);

            if (printer == null)
                return BadRequest(new { message = "Invalid printer" });

            if (!printer.SupportsCashDrawer)
                return BadRequest(new { message = "Printer does not support cash drawer" });

            drawer.PrinterId = request.PrinterId.Value;
            drawer.ConnectionType = "printer";
        }

        if (request.ConnectionType != null && !request.PrinterId.HasValue)
        {
            drawer.ConnectionType = request.ConnectionType;
            drawer.PrinterId = null;
        }

        if (request.IpAddress != null)
            drawer.IpAddress = request.IpAddress;
        if (request.Port.HasValue)
            drawer.Port = request.Port.Value;
        if (request.IsActive.HasValue)
            drawer.IsActive = request.IsActive.Value;
        if (request.KickPulsePin.HasValue)
            drawer.KickPulsePin = request.KickPulsePin.Value;
        if (request.KickPulseOnTime.HasValue)
            drawer.KickPulseOnTime = request.KickPulseOnTime.Value;
        if (request.KickPulseOffTime.HasValue)
            drawer.KickPulseOffTime = request.KickPulseOffTime.Value;

        drawer.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Reload printer reference
        await _context.Entry(drawer).Reference(d => d.Printer).LoadAsync();

        var dto = MapToDto(drawer);
        dto.AddSelfLink($"/api/locations/{locationId}/cash-drawers/{drawer.Id}");

        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid locationId, Guid id)
    {
        var drawer = await _context.CashDrawers
            .FirstOrDefaultAsync(d => d.LocationId == locationId && d.Id == id);

        if (drawer == null)
            return NotFound();

        // Check if used by devices
        var hasReferences = await _context.PosDevices.AnyAsync(d => d.DefaultCashDrawerId == id);

        if (hasReferences)
        {
            // Soft delete
            drawer.IsActive = false;
            drawer.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _context.CashDrawers.Remove(drawer);
        }

        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static CashDrawerDto MapToDto(CashDrawer drawer)
    {
        return new CashDrawerDto
        {
            Id = drawer.Id,
            LocationId = drawer.LocationId,
            Name = drawer.Name,
            PrinterId = drawer.PrinterId,
            PrinterName = drawer.Printer?.Name,
            ConnectionType = drawer.ConnectionType,
            IpAddress = drawer.IpAddress,
            Port = drawer.Port,
            IsActive = drawer.IsActive,
            KickPulsePin = drawer.KickPulsePin,
            KickPulseOnTime = drawer.KickPulseOnTime,
            KickPulseOffTime = drawer.KickPulseOffTime
        };
    }
}
