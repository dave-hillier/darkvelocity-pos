using DarkVelocity.Costing.Api.Data;
using DarkVelocity.Costing.Api.Dtos;
using DarkVelocity.Costing.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Costing.Api.Controllers;

[ApiController]
[Route("api/cost-alerts")]
public class CostAlertsController : ControllerBase
{
    private readonly CostingDbContext _context;

    public CostAlertsController(CostingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<CostAlertDto>>> GetAll(
        [FromQuery] bool? acknowledged = null,
        [FromQuery] string? alertType = null,
        [FromQuery] Guid? recipeId = null,
        [FromQuery] Guid? ingredientId = null)
    {
        var query = _context.CostAlerts.AsQueryable();

        if (acknowledged.HasValue)
            query = query.Where(a => a.IsAcknowledged == acknowledged.Value);

        if (!string.IsNullOrEmpty(alertType))
            query = query.Where(a => a.AlertType == alertType);

        if (recipeId.HasValue)
            query = query.Where(a => a.RecipeId == recipeId.Value);

        if (ingredientId.HasValue)
            query = query.Where(a => a.IngredientId == ingredientId.Value);

        var alerts = await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return Ok(alerts.Select(MapToDto).ToList());
    }

    [HttpGet("{alertId:guid}")]
    public async Task<ActionResult<CostAlertDto>> GetById(Guid alertId)
    {
        var alert = await _context.CostAlerts.FindAsync(alertId);

        if (alert == null)
            return NotFound();

        var dto = MapToDto(alert);
        dto.AddSelfLink($"/api/cost-alerts/{alertId}");

        return Ok(dto);
    }

    [HttpGet("unacknowledged/count")]
    public async Task<ActionResult<object>> GetUnacknowledgedCount()
    {
        var counts = await _context.CostAlerts
            .Where(a => !a.IsAcknowledged)
            .GroupBy(a => a.AlertType)
            .Select(g => new { AlertType = g.Key, Count = g.Count() })
            .ToListAsync();

        return Ok(new
        {
            Total = counts.Sum(c => c.Count),
            ByType = counts
        });
    }

    [HttpPost("{alertId:guid}/acknowledge")]
    public async Task<ActionResult<CostAlertDto>> Acknowledge(
        Guid alertId,
        [FromBody] AcknowledgeCostAlertRequest request)
    {
        var alert = await _context.CostAlerts.FindAsync(alertId);

        if (alert == null)
            return NotFound();

        alert.IsAcknowledged = true;
        alert.AcknowledgedAt = DateTime.UtcNow;
        alert.Notes = request.Notes;
        alert.ActionTaken = request.ActionTaken;
        alert.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(alert);
        dto.AddSelfLink($"/api/cost-alerts/{alertId}");

        return Ok(dto);
    }

    [HttpPost("acknowledge-bulk")]
    public async Task<ActionResult<object>> AcknowledgeBulk(
        [FromBody] List<Guid> alertIds,
        [FromQuery] string? actionTaken = null)
    {
        var alerts = await _context.CostAlerts
            .Where(a => alertIds.Contains(a.Id) && !a.IsAcknowledged)
            .ToListAsync();

        foreach (var alert in alerts)
        {
            alert.IsAcknowledged = true;
            alert.AcknowledgedAt = DateTime.UtcNow;
            alert.ActionTaken = actionTaken;
            alert.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(new { AcknowledgedCount = alerts.Count });
    }

    [HttpDelete("{alertId:guid}")]
    public async Task<IActionResult> Delete(Guid alertId)
    {
        var alert = await _context.CostAlerts.FindAsync(alertId);

        if (alert == null)
            return NotFound();

        _context.CostAlerts.Remove(alert);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // Internal method to create alerts (would typically be called by a service)
    [HttpPost]
    public async Task<ActionResult<CostAlertDto>> Create([FromBody] CreateCostAlertRequest request)
    {
        var alert = new CostAlert
        {
            AlertType = request.AlertType,
            RecipeId = request.RecipeId,
            RecipeName = request.RecipeName,
            IngredientId = request.IngredientId,
            IngredientName = request.IngredientName,
            MenuItemId = request.MenuItemId,
            MenuItemName = request.MenuItemName,
            PreviousValue = request.PreviousValue,
            CurrentValue = request.CurrentValue,
            ChangePercent = request.ChangePercent,
            ThresholdValue = request.ThresholdValue,
            ImpactDescription = request.ImpactDescription,
            AffectedRecipeCount = request.AffectedRecipeCount
        };

        _context.CostAlerts.Add(alert);
        await _context.SaveChangesAsync();

        var dto = MapToDto(alert);
        dto.AddSelfLink($"/api/cost-alerts/{alert.Id}");

        return CreatedAtAction(nameof(GetById), new { alertId = alert.Id }, dto);
    }

    private static CostAlertDto MapToDto(CostAlert alert)
    {
        return new CostAlertDto
        {
            Id = alert.Id,
            AlertType = alert.AlertType,
            RecipeId = alert.RecipeId,
            RecipeName = alert.RecipeName,
            IngredientId = alert.IngredientId,
            IngredientName = alert.IngredientName,
            MenuItemId = alert.MenuItemId,
            MenuItemName = alert.MenuItemName,
            PreviousValue = alert.PreviousValue,
            CurrentValue = alert.CurrentValue,
            ChangePercent = alert.ChangePercent,
            ThresholdValue = alert.ThresholdValue,
            ImpactDescription = alert.ImpactDescription,
            AffectedRecipeCount = alert.AffectedRecipeCount,
            IsAcknowledged = alert.IsAcknowledged,
            AcknowledgedAt = alert.AcknowledgedAt,
            Notes = alert.Notes,
            ActionTaken = alert.ActionTaken,
            CreatedAt = alert.CreatedAt
        };
    }
}

public record CreateCostAlertRequest(
    string AlertType,
    decimal PreviousValue,
    decimal CurrentValue,
    decimal ChangePercent,
    Guid? RecipeId = null,
    string? RecipeName = null,
    Guid? IngredientId = null,
    string? IngredientName = null,
    Guid? MenuItemId = null,
    string? MenuItemName = null,
    decimal? ThresholdValue = null,
    string? ImpactDescription = null,
    int AffectedRecipeCount = 0);
