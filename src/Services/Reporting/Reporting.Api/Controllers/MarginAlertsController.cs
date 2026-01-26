using DarkVelocity.Reporting.Api.Data;
using DarkVelocity.Reporting.Api.Dtos;
using DarkVelocity.Reporting.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Reporting.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/margin-alerts")]
public class MarginAlertsController : ControllerBase
{
    private readonly ReportingDbContext _context;

    public MarginAlertsController(ReportingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<MarginAlertDto>>> GetAll(
        Guid locationId,
        [FromQuery] bool? acknowledged = null,
        [FromQuery] string? alertType = null,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
    {
        var query = _context.MarginAlerts
            .Where(a => a.LocationId == locationId);

        if (acknowledged.HasValue)
            query = query.Where(a => a.IsAcknowledged == acknowledged.Value);

        if (!string.IsNullOrEmpty(alertType))
            query = query.Where(a => a.AlertType == alertType);

        if (startDate.HasValue)
            query = query.Where(a => a.ReportDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(a => a.ReportDate <= endDate.Value);

        var alerts = await query
            .OrderByDescending(a => a.ReportDate)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync();

        return Ok(alerts.Select(MapToDto).ToList());
    }

    [HttpGet("{alertId:guid}")]
    public async Task<ActionResult<MarginAlertDto>> GetById(Guid locationId, Guid alertId)
    {
        var alert = await _context.MarginAlerts
            .FirstOrDefaultAsync(a => a.LocationId == locationId && a.Id == alertId);

        if (alert == null)
            return NotFound();

        var dto = MapToDto(alert);
        dto.AddSelfLink($"/api/locations/{locationId}/margin-alerts/{alertId}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<MarginAlertDto>> Create(
        Guid locationId,
        [FromBody] CreateMarginAlertRequest request)
    {
        var alert = new MarginAlert
        {
            LocationId = locationId,
            AlertType = request.AlertType,
            MenuItemId = request.MenuItemId,
            MenuItemName = request.MenuItemName,
            CategoryId = request.CategoryId,
            CategoryName = request.CategoryName,
            CurrentMargin = request.CurrentMargin,
            ThresholdMargin = request.ThresholdMargin,
            Variance = request.CurrentMargin - request.ThresholdMargin,
            ReportDate = request.ReportDate
        };

        _context.MarginAlerts.Add(alert);
        await _context.SaveChangesAsync();

        var dto = MapToDto(alert);
        dto.AddSelfLink($"/api/locations/{locationId}/margin-alerts/{alert.Id}");

        return CreatedAtAction(nameof(GetById), new { locationId, alertId = alert.Id }, dto);
    }

    [HttpPost("{alertId:guid}/acknowledge")]
    public async Task<ActionResult<MarginAlertDto>> Acknowledge(
        Guid locationId,
        Guid alertId,
        [FromBody] AcknowledgeAlertRequest request)
    {
        var alert = await _context.MarginAlerts
            .FirstOrDefaultAsync(a => a.LocationId == locationId && a.Id == alertId);

        if (alert == null)
            return NotFound();

        alert.IsAcknowledged = true;
        alert.AcknowledgedAt = DateTime.UtcNow;
        alert.Notes = request.Notes;
        alert.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(alert);
        dto.AddSelfLink($"/api/locations/{locationId}/margin-alerts/{alertId}");

        return Ok(dto);
    }

    [HttpDelete("{alertId:guid}")]
    public async Task<IActionResult> Delete(Guid locationId, Guid alertId)
    {
        var alert = await _context.MarginAlerts
            .FirstOrDefaultAsync(a => a.LocationId == locationId && a.Id == alertId);

        if (alert == null)
            return NotFound();

        _context.MarginAlerts.Remove(alert);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static MarginAlertDto MapToDto(MarginAlert alert)
    {
        return new MarginAlertDto
        {
            Id = alert.Id,
            LocationId = alert.LocationId,
            AlertType = alert.AlertType,
            MenuItemId = alert.MenuItemId,
            MenuItemName = alert.MenuItemName,
            CategoryId = alert.CategoryId,
            CategoryName = alert.CategoryName,
            CurrentMargin = alert.CurrentMargin,
            ThresholdMargin = alert.ThresholdMargin,
            Variance = alert.Variance,
            ReportDate = alert.ReportDate,
            IsAcknowledged = alert.IsAcknowledged,
            AcknowledgedAt = alert.AcknowledgedAt,
            Notes = alert.Notes
        };
    }
}

public record CreateMarginAlertRequest(
    string AlertType,
    DateOnly ReportDate,
    decimal CurrentMargin,
    decimal ThresholdMargin,
    Guid? MenuItemId = null,
    string? MenuItemName = null,
    Guid? CategoryId = null,
    string? CategoryName = null);
