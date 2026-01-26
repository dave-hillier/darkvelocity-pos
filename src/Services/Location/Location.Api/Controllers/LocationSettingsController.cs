using DarkVelocity.Location.Api.Data;
using DarkVelocity.Location.Api.Dtos;
using DarkVelocity.Location.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Location.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/settings")]
public class LocationSettingsController : ControllerBase
{
    private readonly LocationDbContext _context;

    public LocationSettingsController(LocationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<LocationSettingsDto>> Get(Guid locationId)
    {
        var settings = await _context.LocationSettings
            .FirstOrDefaultAsync(s => s.LocationId == locationId);

        if (settings == null)
        {
            // Check if location exists
            var locationExists = await _context.Locations.AnyAsync(l => l.Id == locationId);
            if (!locationExists)
                return NotFound(new { message = "Location not found" });

            // Create default settings
            settings = new LocationSettings { LocationId = locationId };
            _context.LocationSettings.Add(settings);
            await _context.SaveChangesAsync();
        }

        var dto = MapToDto(settings);
        dto.AddSelfLink($"/api/locations/{locationId}/settings");

        return Ok(dto);
    }

    [HttpPut]
    public async Task<ActionResult<LocationSettingsDto>> Update(
        Guid locationId,
        [FromBody] UpdateLocationSettingsRequest request)
    {
        var settings = await _context.LocationSettings
            .FirstOrDefaultAsync(s => s.LocationId == locationId);

        if (settings == null)
        {
            var locationExists = await _context.Locations.AnyAsync(l => l.Id == locationId);
            if (!locationExists)
                return NotFound(new { message = "Location not found" });

            // Create settings if they don't exist
            settings = new LocationSettings { LocationId = locationId };
            _context.LocationSettings.Add(settings);
        }

        // Update fields
        if (request.DefaultTaxRate.HasValue)
            settings.DefaultTaxRate = request.DefaultTaxRate.Value;

        if (request.TaxIncludedInPrices.HasValue)
            settings.TaxIncludedInPrices = request.TaxIncludedInPrices.Value;

        if (request.ReceiptHeader != null)
            settings.ReceiptHeader = request.ReceiptHeader;

        if (request.ReceiptFooter != null)
            settings.ReceiptFooter = request.ReceiptFooter;

        if (request.PrintReceiptByDefault.HasValue)
            settings.PrintReceiptByDefault = request.PrintReceiptByDefault.Value;

        if (request.ShowTaxBreakdown.HasValue)
            settings.ShowTaxBreakdown = request.ShowTaxBreakdown.Value;

        if (request.RequireTableForDineIn.HasValue)
            settings.RequireTableForDineIn = request.RequireTableForDineIn.Value;

        if (request.AutoPrintKitchenTickets.HasValue)
            settings.AutoPrintKitchenTickets = request.AutoPrintKitchenTickets.Value;

        if (request.OrderNumberResetHour.HasValue)
            settings.OrderNumberResetHour = request.OrderNumberResetHour.Value;

        if (request.OrderNumberPrefix != null)
            settings.OrderNumberPrefix = request.OrderNumberPrefix;

        if (request.AllowCashPayments.HasValue)
            settings.AllowCashPayments = request.AllowCashPayments.Value;

        if (request.AllowCardPayments.HasValue)
            settings.AllowCardPayments = request.AllowCardPayments.Value;

        if (request.TipsEnabled.HasValue)
            settings.TipsEnabled = request.TipsEnabled.Value;

        if (request.TipSuggestions != null)
            settings.TipSuggestions = request.TipSuggestions;

        if (request.TrackInventory.HasValue)
            settings.TrackInventory = request.TrackInventory.Value;

        if (request.WarnOnLowStock.HasValue)
            settings.WarnOnLowStock = request.WarnOnLowStock.Value;

        if (request.AllowNegativeStock.HasValue)
            settings.AllowNegativeStock = request.AllowNegativeStock.Value;

        settings.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(settings);
        dto.AddSelfLink($"/api/locations/{locationId}/settings");

        return Ok(dto);
    }

    private static LocationSettingsDto MapToDto(LocationSettings settings)
    {
        return new LocationSettingsDto
        {
            Id = settings.Id,
            LocationId = settings.LocationId,
            DefaultTaxRate = settings.DefaultTaxRate,
            TaxIncludedInPrices = settings.TaxIncludedInPrices,
            ReceiptHeader = settings.ReceiptHeader,
            ReceiptFooter = settings.ReceiptFooter,
            PrintReceiptByDefault = settings.PrintReceiptByDefault,
            ShowTaxBreakdown = settings.ShowTaxBreakdown,
            RequireTableForDineIn = settings.RequireTableForDineIn,
            AutoPrintKitchenTickets = settings.AutoPrintKitchenTickets,
            OrderNumberResetHour = settings.OrderNumberResetHour,
            OrderNumberPrefix = settings.OrderNumberPrefix,
            AllowCashPayments = settings.AllowCashPayments,
            AllowCardPayments = settings.AllowCardPayments,
            TipsEnabled = settings.TipsEnabled,
            TipSuggestions = settings.TipSuggestions,
            TrackInventory = settings.TrackInventory,
            WarnOnLowStock = settings.WarnOnLowStock,
            AllowNegativeStock = settings.AllowNegativeStock
        };
    }
}
