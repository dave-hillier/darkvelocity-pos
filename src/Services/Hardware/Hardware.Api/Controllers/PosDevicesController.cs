using DarkVelocity.Hardware.Api.Data;
using DarkVelocity.Hardware.Api.Dtos;
using DarkVelocity.Hardware.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Hardware.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/devices")]
public class PosDevicesController : ControllerBase
{
    private readonly HardwareDbContext _context;

    public PosDevicesController(HardwareDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<PosDeviceDto>>> GetAll(
        Guid locationId,
        [FromQuery] bool activeOnly = false,
        [FromQuery] bool onlineOnly = false)
    {
        var query = _context.PosDevices
            .Include(d => d.DefaultPrinter)
            .Include(d => d.DefaultCashDrawer)
            .Where(d => d.LocationId == locationId)
            .AsQueryable();

        if (activeOnly)
            query = query.Where(d => d.IsActive);
        if (onlineOnly)
            query = query.Where(d => d.IsOnline);

        var devices = await query
            .OrderBy(d => d.Name)
            .Select(d => new PosDeviceDto
            {
                Id = d.Id,
                LocationId = d.LocationId,
                Name = d.Name,
                DeviceId = d.DeviceId,
                DeviceType = d.DeviceType,
                Model = d.Model,
                OsVersion = d.OsVersion,
                AppVersion = d.AppVersion,
                DefaultPrinterId = d.DefaultPrinterId,
                DefaultPrinterName = d.DefaultPrinter != null ? d.DefaultPrinter.Name : null,
                DefaultCashDrawerId = d.DefaultCashDrawerId,
                DefaultCashDrawerName = d.DefaultCashDrawer != null ? d.DefaultCashDrawer.Name : null,
                AutoPrintReceipts = d.AutoPrintReceipts,
                OpenDrawerOnCash = d.OpenDrawerOnCash,
                IsActive = d.IsActive,
                IsOnline = d.IsOnline,
                LastSeenAt = d.LastSeenAt,
                RegisteredAt = d.RegisteredAt
            })
            .ToListAsync();

        foreach (var device in devices)
        {
            device.AddSelfLink($"/api/locations/{locationId}/devices/{device.Id}");
        }

        return Ok(HalCollection<PosDeviceDto>.Create(
            devices,
            $"/api/locations/{locationId}/devices",
            devices.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PosDeviceDto>> GetById(Guid locationId, Guid id)
    {
        var device = await _context.PosDevices
            .Include(d => d.DefaultPrinter)
            .Include(d => d.DefaultCashDrawer)
            .FirstOrDefaultAsync(d => d.LocationId == locationId && d.Id == id);

        if (device == null)
            return NotFound();

        var dto = MapToDto(device);
        dto.AddSelfLink($"/api/locations/{locationId}/devices/{device.Id}");

        return Ok(dto);
    }

    [HttpGet("by-device-id/{deviceId}")]
    public async Task<ActionResult<PosDeviceDto>> GetByDeviceId(Guid locationId, string deviceId)
    {
        var device = await _context.PosDevices
            .Include(d => d.DefaultPrinter)
            .Include(d => d.DefaultCashDrawer)
            .FirstOrDefaultAsync(d => d.LocationId == locationId && d.DeviceId == deviceId);

        if (device == null)
            return NotFound();

        var dto = MapToDto(device);
        dto.AddSelfLink($"/api/locations/{locationId}/devices/{device.Id}");

        return Ok(dto);
    }

    [HttpPost("register")]
    public async Task<ActionResult<PosDeviceDto>> Register(
        Guid locationId,
        [FromBody] RegisterPosDeviceRequest request)
    {
        // Check if device already exists
        var existing = await _context.PosDevices
            .Include(d => d.DefaultPrinter)
            .Include(d => d.DefaultCashDrawer)
            .FirstOrDefaultAsync(d => d.DeviceId == request.DeviceId);

        if (existing != null)
        {
            // Update location and mark as seen
            existing.LocationId = locationId;
            existing.Name = request.Name;
            existing.DeviceType = request.DeviceType ?? existing.DeviceType;
            existing.Model = request.Model ?? existing.Model;
            existing.OsVersion = request.OsVersion ?? existing.OsVersion;
            existing.AppVersion = request.AppVersion ?? existing.AppVersion;
            existing.IsOnline = true;
            existing.LastSeenAt = DateTime.UtcNow;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var existingDto = MapToDto(existing);
            existingDto.AddSelfLink($"/api/locations/{locationId}/devices/{existing.Id}");

            return Ok(existingDto);
        }

        // Check for duplicate name
        var nameExists = await _context.PosDevices
            .AnyAsync(d => d.LocationId == locationId && d.Name == request.Name);

        if (nameExists)
            return Conflict(new { message = "A device with this name already exists" });

        var device = new PosDevice
        {
            LocationId = locationId,
            Name = request.Name,
            DeviceId = request.DeviceId,
            DeviceType = request.DeviceType,
            Model = request.Model,
            OsVersion = request.OsVersion,
            AppVersion = request.AppVersion,
            IsOnline = true,
            LastSeenAt = DateTime.UtcNow,
            RegisteredAt = DateTime.UtcNow
        };

        _context.PosDevices.Add(device);
        await _context.SaveChangesAsync();

        var dto = MapToDto(device);
        dto.AddSelfLink($"/api/locations/{locationId}/devices/{device.Id}");

        return CreatedAtAction(nameof(GetById), new { locationId, id = device.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PosDeviceDto>> Update(
        Guid locationId,
        Guid id,
        [FromBody] UpdatePosDeviceRequest request)
    {
        var device = await _context.PosDevices
            .Include(d => d.DefaultPrinter)
            .Include(d => d.DefaultCashDrawer)
            .FirstOrDefaultAsync(d => d.LocationId == locationId && d.Id == id);

        if (device == null)
            return NotFound();

        if (request.Name != null)
        {
            var duplicate = await _context.PosDevices
                .AnyAsync(d => d.LocationId == locationId && d.Name == request.Name && d.Id != id);

            if (duplicate)
                return Conflict(new { message = "A device with this name already exists" });

            device.Name = request.Name;
        }

        if (request.AppVersion != null)
            device.AppVersion = request.AppVersion;

        if (request.DefaultPrinterId.HasValue)
        {
            var printer = await _context.Printers
                .FirstOrDefaultAsync(p => p.LocationId == locationId && p.Id == request.DefaultPrinterId.Value);

            if (printer == null)
                return BadRequest(new { message = "Invalid printer" });

            device.DefaultPrinterId = request.DefaultPrinterId.Value;
        }

        if (request.DefaultCashDrawerId.HasValue)
        {
            var drawer = await _context.CashDrawers
                .FirstOrDefaultAsync(d => d.LocationId == locationId && d.Id == request.DefaultCashDrawerId.Value);

            if (drawer == null)
                return BadRequest(new { message = "Invalid cash drawer" });

            device.DefaultCashDrawerId = request.DefaultCashDrawerId.Value;
        }

        if (request.AutoPrintReceipts.HasValue)
            device.AutoPrintReceipts = request.AutoPrintReceipts.Value;
        if (request.OpenDrawerOnCash.HasValue)
            device.OpenDrawerOnCash = request.OpenDrawerOnCash.Value;
        if (request.IsActive.HasValue)
            device.IsActive = request.IsActive.Value;

        device.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Reload references
        await _context.Entry(device).Reference(d => d.DefaultPrinter).LoadAsync();
        await _context.Entry(device).Reference(d => d.DefaultCashDrawer).LoadAsync();

        var dto = MapToDto(device);
        dto.AddSelfLink($"/api/locations/{locationId}/devices/{device.Id}");

        return Ok(dto);
    }

    [HttpPost("{id:guid}/heartbeat")]
    public async Task<ActionResult<HeartbeatResponse>> Heartbeat(
        Guid locationId,
        Guid id,
        [FromBody] HeartbeatRequest request)
    {
        var device = await _context.PosDevices
            .Include(d => d.DefaultPrinter)
            .Include(d => d.DefaultCashDrawer)
            .FirstOrDefaultAsync(d => d.LocationId == locationId && d.Id == id);

        if (device == null)
            return NotFound();

        device.IsOnline = true;
        device.LastSeenAt = DateTime.UtcNow;

        if (request.AppVersion != null)
            device.AppVersion = request.AppVersion;
        if (request.OsVersion != null)
            device.OsVersion = request.OsVersion;

        device.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = MapToDto(device);
        dto.AddSelfLink($"/api/locations/{locationId}/devices/{device.Id}");

        return Ok(new HeartbeatResponse
        {
            Success = true,
            ServerTime = DateTime.UtcNow,
            Device = dto
        });
    }

    [HttpPost("{id:guid}/offline")]
    public async Task<IActionResult> MarkOffline(Guid locationId, Guid id)
    {
        var device = await _context.PosDevices
            .FirstOrDefaultAsync(d => d.LocationId == locationId && d.Id == id);

        if (device == null)
            return NotFound();

        device.IsOnline = false;
        device.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid locationId, Guid id)
    {
        var device = await _context.PosDevices
            .FirstOrDefaultAsync(d => d.LocationId == locationId && d.Id == id);

        if (device == null)
            return NotFound();

        _context.PosDevices.Remove(device);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static PosDeviceDto MapToDto(PosDevice device)
    {
        return new PosDeviceDto
        {
            Id = device.Id,
            LocationId = device.LocationId,
            Name = device.Name,
            DeviceId = device.DeviceId,
            DeviceType = device.DeviceType,
            Model = device.Model,
            OsVersion = device.OsVersion,
            AppVersion = device.AppVersion,
            DefaultPrinterId = device.DefaultPrinterId,
            DefaultPrinterName = device.DefaultPrinter?.Name,
            DefaultCashDrawerId = device.DefaultCashDrawerId,
            DefaultCashDrawerName = device.DefaultCashDrawer?.Name,
            AutoPrintReceipts = device.AutoPrintReceipts,
            OpenDrawerOnCash = device.OpenDrawerOnCash,
            IsActive = device.IsActive,
            IsOnline = device.IsOnline,
            LastSeenAt = device.LastSeenAt,
            RegisteredAt = device.RegisteredAt
        };
    }
}
