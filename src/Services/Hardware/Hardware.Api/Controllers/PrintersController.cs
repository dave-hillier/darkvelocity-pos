using DarkVelocity.Hardware.Api.Data;
using DarkVelocity.Hardware.Api.Dtos;
using DarkVelocity.Hardware.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Hardware.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/printers")]
public class PrintersController : ControllerBase
{
    private readonly HardwareDbContext _context;

    public PrintersController(HardwareDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<PrinterDto>>> GetAll(
        Guid locationId,
        [FromQuery] bool activeOnly = false,
        [FromQuery] string? printerType = null)
    {
        var query = _context.Printers
            .Where(p => p.LocationId == locationId)
            .AsQueryable();

        if (activeOnly)
            query = query.Where(p => p.IsActive);
        if (!string.IsNullOrEmpty(printerType))
            query = query.Where(p => p.PrinterType == printerType);

        var printers = await query
            .OrderBy(p => p.Name)
            .Select(p => MapToDto(p))
            .ToListAsync();

        foreach (var printer in printers)
        {
            printer.AddSelfLink($"/api/locations/{locationId}/printers/{printer.Id}");
        }

        return Ok(HalCollection<PrinterDto>.Create(
            printers,
            $"/api/locations/{locationId}/printers",
            printers.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PrinterDto>> GetById(Guid locationId, Guid id)
    {
        var printer = await _context.Printers
            .FirstOrDefaultAsync(p => p.LocationId == locationId && p.Id == id);

        if (printer == null)
            return NotFound();

        var dto = MapToDto(printer);
        dto.AddSelfLink($"/api/locations/{locationId}/printers/{printer.Id}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<PrinterDto>> Create(
        Guid locationId,
        [FromBody] CreatePrinterRequest request)
    {
        // Check for duplicate name
        var exists = await _context.Printers
            .AnyAsync(p => p.LocationId == locationId && p.Name == request.Name);

        if (exists)
            return Conflict(new { message = "A printer with this name already exists" });

        var printer = new Printer
        {
            LocationId = locationId,
            Name = request.Name,
            PrinterType = request.PrinterType,
            ConnectionType = request.ConnectionType,
            IpAddress = request.IpAddress,
            Port = request.Port,
            MacAddress = request.MacAddress,
            UsbVendorId = request.UsbVendorId,
            UsbProductId = request.UsbProductId,
            PaperWidth = request.PaperWidth,
            IsDefault = request.IsDefault,
            CharacterSet = request.CharacterSet,
            SupportsCut = request.SupportsCut,
            SupportsCashDrawer = request.SupportsCashDrawer
        };

        // If this is set as default, unset other defaults of same type
        if (request.IsDefault)
        {
            var existingDefaults = await _context.Printers
                .Where(p => p.LocationId == locationId && p.PrinterType == request.PrinterType && p.IsDefault)
                .ToListAsync();

            foreach (var existing in existingDefaults)
            {
                existing.IsDefault = false;
            }
        }

        _context.Printers.Add(printer);
        await _context.SaveChangesAsync();

        var dto = MapToDto(printer);
        dto.AddSelfLink($"/api/locations/{locationId}/printers/{printer.Id}");

        return CreatedAtAction(nameof(GetById), new { locationId, id = printer.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PrinterDto>> Update(
        Guid locationId,
        Guid id,
        [FromBody] UpdatePrinterRequest request)
    {
        var printer = await _context.Printers
            .FirstOrDefaultAsync(p => p.LocationId == locationId && p.Id == id);

        if (printer == null)
            return NotFound();

        if (request.Name != null)
        {
            var duplicate = await _context.Printers
                .AnyAsync(p => p.LocationId == locationId && p.Name == request.Name && p.Id != id);

            if (duplicate)
                return Conflict(new { message = "A printer with this name already exists" });

            printer.Name = request.Name;
        }

        if (request.IpAddress != null)
            printer.IpAddress = request.IpAddress;
        if (request.Port.HasValue)
            printer.Port = request.Port.Value;
        if (request.MacAddress != null)
            printer.MacAddress = request.MacAddress;
        if (request.UsbVendorId != null)
            printer.UsbVendorId = request.UsbVendorId;
        if (request.UsbProductId != null)
            printer.UsbProductId = request.UsbProductId;
        if (request.PaperWidth.HasValue)
            printer.PaperWidth = request.PaperWidth.Value;
        if (request.IsActive.HasValue)
            printer.IsActive = request.IsActive.Value;
        if (request.CharacterSet != null)
            printer.CharacterSet = request.CharacterSet;
        if (request.SupportsCut.HasValue)
            printer.SupportsCut = request.SupportsCut.Value;
        if (request.SupportsCashDrawer.HasValue)
            printer.SupportsCashDrawer = request.SupportsCashDrawer.Value;

        if (request.IsDefault.HasValue && request.IsDefault.Value)
        {
            // Unset other defaults of same type
            var existingDefaults = await _context.Printers
                .Where(p => p.LocationId == locationId && p.PrinterType == printer.PrinterType && p.IsDefault && p.Id != id)
                .ToListAsync();

            foreach (var existing in existingDefaults)
            {
                existing.IsDefault = false;
            }

            printer.IsDefault = true;
        }
        else if (request.IsDefault.HasValue)
        {
            printer.IsDefault = false;
        }

        printer.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = MapToDto(printer);
        dto.AddSelfLink($"/api/locations/{locationId}/printers/{printer.Id}");

        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid locationId, Guid id)
    {
        var printer = await _context.Printers
            .FirstOrDefaultAsync(p => p.LocationId == locationId && p.Id == id);

        if (printer == null)
            return NotFound();

        // Check if used by devices or drawers
        var hasReferences = await _context.CashDrawers.AnyAsync(d => d.PrinterId == id)
            || await _context.PosDevices.AnyAsync(d => d.DefaultPrinterId == id);

        if (hasReferences)
        {
            // Soft delete
            printer.IsActive = false;
            printer.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _context.Printers.Remove(printer);
        }

        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static PrinterDto MapToDto(Printer printer)
    {
        return new PrinterDto
        {
            Id = printer.Id,
            LocationId = printer.LocationId,
            Name = printer.Name,
            PrinterType = printer.PrinterType,
            ConnectionType = printer.ConnectionType,
            IpAddress = printer.IpAddress,
            Port = printer.Port,
            MacAddress = printer.MacAddress,
            UsbVendorId = printer.UsbVendorId,
            UsbProductId = printer.UsbProductId,
            PaperWidth = printer.PaperWidth,
            IsDefault = printer.IsDefault,
            IsActive = printer.IsActive,
            CharacterSet = printer.CharacterSet,
            SupportsCut = printer.SupportsCut,
            SupportsCashDrawer = printer.SupportsCashDrawer
        };
    }
}
