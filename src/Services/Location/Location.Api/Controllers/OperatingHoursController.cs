using DarkVelocity.Location.Api.Data;
using DarkVelocity.Location.Api.Dtos;
using DarkVelocity.Location.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Location.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/hours")]
public class OperatingHoursController : ControllerBase
{
    private readonly LocationDbContext _context;

    public OperatingHoursController(LocationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<OperatingHoursDto>>> GetAll(Guid locationId)
    {
        var locationExists = await _context.Locations.AnyAsync(l => l.Id == locationId);
        if (!locationExists)
            return NotFound(new { message = "Location not found" });

        var hours = await _context.OperatingHours
            .Where(h => h.LocationId == locationId)
            .OrderBy(h => h.DayOfWeek)
            .ToListAsync();

        return Ok(hours.Select(MapToDto).ToList());
    }

    [HttpGet("{dayOfWeek}")]
    public async Task<ActionResult<OperatingHoursDto>> GetByDay(Guid locationId, DayOfWeek dayOfWeek)
    {
        var hours = await _context.OperatingHours
            .FirstOrDefaultAsync(h => h.LocationId == locationId && h.DayOfWeek == dayOfWeek);

        if (hours == null)
        {
            var locationExists = await _context.Locations.AnyAsync(l => l.Id == locationId);
            if (!locationExists)
                return NotFound(new { message = "Location not found" });

            return NotFound(new { message = "Operating hours not set for this day" });
        }

        var dto = MapToDto(hours);
        dto.AddSelfLink($"/api/locations/{locationId}/hours/{dayOfWeek}");

        return Ok(dto);
    }

    [HttpPut("{dayOfWeek}")]
    public async Task<ActionResult<OperatingHoursDto>> Set(
        Guid locationId,
        DayOfWeek dayOfWeek,
        [FromBody] SetOperatingHoursRequest request)
    {
        var locationExists = await _context.Locations.AnyAsync(l => l.Id == locationId);
        if (!locationExists)
            return NotFound(new { message = "Location not found" });

        var hours = await _context.OperatingHours
            .FirstOrDefaultAsync(h => h.LocationId == locationId && h.DayOfWeek == dayOfWeek);

        if (hours == null)
        {
            hours = new OperatingHours
            {
                LocationId = locationId,
                DayOfWeek = dayOfWeek
            };
            _context.OperatingHours.Add(hours);
        }

        hours.OpenTime = request.OpenTime;
        hours.CloseTime = request.CloseTime;
        hours.IsClosed = request.IsClosed;
        hours.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(hours);
        dto.AddSelfLink($"/api/locations/{locationId}/hours/{dayOfWeek}");

        return Ok(dto);
    }

    [HttpPost("bulk")]
    public async Task<ActionResult<List<OperatingHoursDto>>> SetBulk(
        Guid locationId,
        [FromBody] List<SetOperatingHoursRequest> requests)
    {
        var locationExists = await _context.Locations.AnyAsync(l => l.Id == locationId);
        if (!locationExists)
            return NotFound(new { message = "Location not found" });

        var results = new List<OperatingHours>();

        foreach (var request in requests)
        {
            var hours = await _context.OperatingHours
                .FirstOrDefaultAsync(h => h.LocationId == locationId && h.DayOfWeek == request.DayOfWeek);

            if (hours == null)
            {
                hours = new OperatingHours
                {
                    LocationId = locationId,
                    DayOfWeek = request.DayOfWeek
                };
                _context.OperatingHours.Add(hours);
            }

            hours.OpenTime = request.OpenTime;
            hours.CloseTime = request.CloseTime;
            hours.IsClosed = request.IsClosed;
            hours.UpdatedAt = DateTime.UtcNow;

            results.Add(hours);
        }

        await _context.SaveChangesAsync();

        return Ok(results.Select(MapToDto).OrderBy(h => h.DayOfWeek).ToList());
    }

    [HttpDelete("{dayOfWeek}")]
    public async Task<IActionResult> Delete(Guid locationId, DayOfWeek dayOfWeek)
    {
        var hours = await _context.OperatingHours
            .FirstOrDefaultAsync(h => h.LocationId == locationId && h.DayOfWeek == dayOfWeek);

        if (hours == null)
            return NotFound();

        _context.OperatingHours.Remove(hours);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("today")]
    public async Task<ActionResult<OperatingHoursDto>> GetToday(Guid locationId)
    {
        var location = await _context.Locations.FindAsync(locationId);
        if (location == null)
            return NotFound(new { message = "Location not found" });

        // Get current day in location's timezone
        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(location.Timezone);
        }
        catch
        {
            tz = TimeZoneInfo.Utc;
        }

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var today = localNow.DayOfWeek;

        var hours = await _context.OperatingHours
            .FirstOrDefaultAsync(h => h.LocationId == locationId && h.DayOfWeek == today);

        if (hours == null)
            return NotFound(new { message = "Operating hours not set for today" });

        var dto = MapToDto(hours);
        dto.AddSelfLink($"/api/locations/{locationId}/hours/today");

        return Ok(dto);
    }

    private static OperatingHoursDto MapToDto(OperatingHours hours)
    {
        return new OperatingHoursDto
        {
            Id = hours.Id,
            LocationId = hours.LocationId,
            DayOfWeek = hours.DayOfWeek,
            OpenTime = hours.OpenTime,
            CloseTime = hours.CloseTime,
            IsClosed = hours.IsClosed
        };
    }
}
