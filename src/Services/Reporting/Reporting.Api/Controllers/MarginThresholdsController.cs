using DarkVelocity.Reporting.Api.Data;
using DarkVelocity.Reporting.Api.Dtos;
using DarkVelocity.Reporting.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Reporting.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/margin-thresholds")]
public class MarginThresholdsController : ControllerBase
{
    private readonly ReportingDbContext _context;

    public MarginThresholdsController(ReportingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<MarginThresholdDto>>> GetAll(
        Guid locationId,
        [FromQuery] string? thresholdType = null,
        [FromQuery] bool? isActive = null)
    {
        var query = _context.MarginThresholds
            .Where(t => t.LocationId == locationId);

        if (!string.IsNullOrEmpty(thresholdType))
            query = query.Where(t => t.ThresholdType == thresholdType);

        if (isActive.HasValue)
            query = query.Where(t => t.IsActive == isActive.Value);

        var thresholds = await query
            .OrderBy(t => t.ThresholdType)
            .ToListAsync();

        return Ok(thresholds.Select(MapToDto).ToList());
    }

    [HttpGet("{thresholdId:guid}")]
    public async Task<ActionResult<MarginThresholdDto>> GetById(Guid locationId, Guid thresholdId)
    {
        var threshold = await _context.MarginThresholds
            .FirstOrDefaultAsync(t => t.LocationId == locationId && t.Id == thresholdId);

        if (threshold == null)
            return NotFound();

        var dto = MapToDto(threshold);
        dto.AddSelfLink($"/api/locations/{locationId}/margin-thresholds/{thresholdId}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<MarginThresholdDto>> Create(
        Guid locationId,
        [FromBody] CreateMarginThresholdRequest request)
    {
        // Check for existing threshold of same type/scope
        var existing = await _context.MarginThresholds
            .FirstOrDefaultAsync(t => t.LocationId == locationId
                && t.ThresholdType == request.ThresholdType
                && t.CategoryId == request.CategoryId
                && t.MenuItemId == request.MenuItemId);

        if (existing != null)
            return Conflict(new { message = "A threshold with this scope already exists" });

        var threshold = new MarginThreshold
        {
            LocationId = locationId,
            ThresholdType = request.ThresholdType,
            CategoryId = request.CategoryId,
            MenuItemId = request.MenuItemId,
            MinimumMarginPercent = request.MinimumMarginPercent,
            WarningMarginPercent = request.WarningMarginPercent
        };

        _context.MarginThresholds.Add(threshold);
        await _context.SaveChangesAsync();

        var dto = MapToDto(threshold);
        dto.AddSelfLink($"/api/locations/{locationId}/margin-thresholds/{threshold.Id}");

        return CreatedAtAction(nameof(GetById), new { locationId, thresholdId = threshold.Id }, dto);
    }

    [HttpPut("{thresholdId:guid}")]
    public async Task<ActionResult<MarginThresholdDto>> Update(
        Guid locationId,
        Guid thresholdId,
        [FromBody] UpdateMarginThresholdRequest request)
    {
        var threshold = await _context.MarginThresholds
            .FirstOrDefaultAsync(t => t.LocationId == locationId && t.Id == thresholdId);

        if (threshold == null)
            return NotFound();

        if (request.MinimumMarginPercent.HasValue)
            threshold.MinimumMarginPercent = request.MinimumMarginPercent.Value;

        if (request.WarningMarginPercent.HasValue)
            threshold.WarningMarginPercent = request.WarningMarginPercent.Value;

        if (request.IsActive.HasValue)
            threshold.IsActive = request.IsActive.Value;

        threshold.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(threshold);
        dto.AddSelfLink($"/api/locations/{locationId}/margin-thresholds/{thresholdId}");

        return Ok(dto);
    }

    [HttpDelete("{thresholdId:guid}")]
    public async Task<IActionResult> Delete(Guid locationId, Guid thresholdId)
    {
        var threshold = await _context.MarginThresholds
            .FirstOrDefaultAsync(t => t.LocationId == locationId && t.Id == thresholdId);

        if (threshold == null)
            return NotFound();

        _context.MarginThresholds.Remove(threshold);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static MarginThresholdDto MapToDto(MarginThreshold threshold)
    {
        return new MarginThresholdDto
        {
            Id = threshold.Id,
            LocationId = threshold.LocationId,
            ThresholdType = threshold.ThresholdType,
            CategoryId = threshold.CategoryId,
            MenuItemId = threshold.MenuItemId,
            MinimumMarginPercent = threshold.MinimumMarginPercent,
            WarningMarginPercent = threshold.WarningMarginPercent,
            IsActive = threshold.IsActive
        };
    }
}
