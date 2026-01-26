using DarkVelocity.Costing.Api.Data;
using DarkVelocity.Costing.Api.Dtos;
using DarkVelocity.Costing.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Costing.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/costing-settings")]
public class CostingSettingsController : ControllerBase
{
    private readonly CostingDbContext _context;

    public CostingSettingsController(CostingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<CostingSettingsDto>> Get(Guid locationId)
    {
        var settings = await _context.CostingSettings
            .FirstOrDefaultAsync(s => s.LocationId == locationId);

        if (settings == null)
        {
            // Create default settings
            settings = new CostingSettings { LocationId = locationId };
            _context.CostingSettings.Add(settings);
            await _context.SaveChangesAsync();
        }

        var dto = MapToDto(settings);
        dto.AddSelfLink($"/api/locations/{locationId}/costing-settings");

        return Ok(dto);
    }

    [HttpPut]
    public async Task<ActionResult<CostingSettingsDto>> Update(
        Guid locationId,
        [FromBody] UpdateCostingSettingsRequest request)
    {
        var settings = await _context.CostingSettings
            .FirstOrDefaultAsync(s => s.LocationId == locationId);

        if (settings == null)
        {
            settings = new CostingSettings { LocationId = locationId };
            _context.CostingSettings.Add(settings);
        }

        if (request.TargetFoodCostPercent.HasValue)
            settings.TargetFoodCostPercent = request.TargetFoodCostPercent.Value;

        if (request.TargetBeverageCostPercent.HasValue)
            settings.TargetBeverageCostPercent = request.TargetBeverageCostPercent.Value;

        if (request.MinimumMarginPercent.HasValue)
            settings.MinimumMarginPercent = request.MinimumMarginPercent.Value;

        if (request.WarningMarginPercent.HasValue)
            settings.WarningMarginPercent = request.WarningMarginPercent.Value;

        if (request.PriceChangeAlertThreshold.HasValue)
            settings.PriceChangeAlertThreshold = request.PriceChangeAlertThreshold.Value;

        if (request.CostIncreaseAlertThreshold.HasValue)
            settings.CostIncreaseAlertThreshold = request.CostIncreaseAlertThreshold.Value;

        if (request.AutoRecalculateCosts.HasValue)
            settings.AutoRecalculateCosts = request.AutoRecalculateCosts.Value;

        if (request.AutoCreateSnapshots.HasValue)
            settings.AutoCreateSnapshots = request.AutoCreateSnapshots.Value;

        if (request.SnapshotFrequencyDays.HasValue)
            settings.SnapshotFrequencyDays = request.SnapshotFrequencyDays.Value;

        settings.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(settings);
        dto.AddSelfLink($"/api/locations/{locationId}/costing-settings");

        return Ok(dto);
    }

    private static CostingSettingsDto MapToDto(CostingSettings settings)
    {
        return new CostingSettingsDto
        {
            Id = settings.Id,
            LocationId = settings.LocationId,
            TargetFoodCostPercent = settings.TargetFoodCostPercent,
            TargetBeverageCostPercent = settings.TargetBeverageCostPercent,
            MinimumMarginPercent = settings.MinimumMarginPercent,
            WarningMarginPercent = settings.WarningMarginPercent,
            PriceChangeAlertThreshold = settings.PriceChangeAlertThreshold,
            CostIncreaseAlertThreshold = settings.CostIncreaseAlertThreshold,
            AutoRecalculateCosts = settings.AutoRecalculateCosts,
            AutoCreateSnapshots = settings.AutoCreateSnapshots,
            SnapshotFrequencyDays = settings.SnapshotFrequencyDays
        };
    }
}
