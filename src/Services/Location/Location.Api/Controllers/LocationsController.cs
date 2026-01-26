using DarkVelocity.Location.Api.Data;
using DarkVelocity.Location.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Location.Api.Controllers;

[ApiController]
[Route("api/locations")]
public class LocationsController : ControllerBase
{
    private readonly LocationDbContext _context;

    public LocationsController(LocationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<LocationSummaryDto>>> GetAll(
        [FromQuery] bool? isActive = null)
    {
        var query = _context.Locations.AsQueryable();

        if (isActive.HasValue)
            query = query.Where(l => l.IsActive == isActive.Value);

        var locations = await query
            .OrderBy(l => l.Name)
            .ToListAsync();

        return Ok(locations.Select(MapToSummaryDto).ToList());
    }

    [HttpGet("{locationId:guid}")]
    public async Task<ActionResult<LocationDto>> GetById(Guid locationId)
    {
        var location = await _context.Locations
            .Include(l => l.Settings)
            .Include(l => l.OperatingHours)
            .FirstOrDefaultAsync(l => l.Id == locationId);

        if (location == null)
            return NotFound();

        var dto = MapToDto(location);
        dto.AddSelfLink($"/api/locations/{locationId}");
        dto.AddLink("settings", $"/api/locations/{locationId}/settings");
        dto.AddLink("hours", $"/api/locations/{locationId}/hours");

        return Ok(dto);
    }

    [HttpGet("by-code/{code}")]
    public async Task<ActionResult<LocationDto>> GetByCode(string code)
    {
        var location = await _context.Locations
            .Include(l => l.Settings)
            .Include(l => l.OperatingHours)
            .FirstOrDefaultAsync(l => l.Code == code);

        if (location == null)
            return NotFound();

        var dto = MapToDto(location);
        dto.AddSelfLink($"/api/locations/{location.Id}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<LocationDto>> Create([FromBody] CreateLocationRequest request)
    {
        // Check for duplicate code
        var existingCode = await _context.Locations
            .AnyAsync(l => l.Code == request.Code);

        if (existingCode)
            return Conflict(new { message = "A location with this code already exists" });

        var location = new Entities.Location
        {
            Name = request.Name,
            Code = request.Code,
            Timezone = request.Timezone,
            CurrencyCode = request.CurrencyCode,
            CurrencySymbol = request.CurrencySymbol ?? GetCurrencySymbol(request.CurrencyCode),
            Phone = request.Phone,
            Email = request.Email,
            Website = request.Website,
            AddressLine1 = request.AddressLine1,
            AddressLine2 = request.AddressLine2,
            City = request.City,
            State = request.State,
            PostalCode = request.PostalCode,
            Country = request.Country,
            TaxNumber = request.TaxNumber,
            BusinessName = request.BusinessName
        };

        _context.Locations.Add(location);
        await _context.SaveChangesAsync();

        // Create default settings
        var settings = new Entities.LocationSettings
        {
            LocationId = location.Id
        };
        _context.LocationSettings.Add(settings);
        await _context.SaveChangesAsync();

        // Reload with settings
        await _context.Entry(location).Reference(l => l.Settings).LoadAsync();

        var dto = MapToDto(location);
        dto.AddSelfLink($"/api/locations/{location.Id}");

        return CreatedAtAction(nameof(GetById), new { locationId = location.Id }, dto);
    }

    [HttpPut("{locationId:guid}")]
    public async Task<ActionResult<LocationDto>> Update(
        Guid locationId,
        [FromBody] UpdateLocationRequest request)
    {
        var location = await _context.Locations
            .Include(l => l.Settings)
            .FirstOrDefaultAsync(l => l.Id == locationId);

        if (location == null)
            return NotFound();

        // Check for duplicate code if changing
        if (request.Code != null && request.Code != location.Code)
        {
            var existingCode = await _context.Locations
                .AnyAsync(l => l.Code == request.Code && l.Id != locationId);

            if (existingCode)
                return Conflict(new { message = "A location with this code already exists" });

            location.Code = request.Code;
        }

        if (request.Name != null) location.Name = request.Name;
        if (request.Timezone != null) location.Timezone = request.Timezone;
        if (request.CurrencyCode != null) location.CurrencyCode = request.CurrencyCode;
        if (request.CurrencySymbol != null) location.CurrencySymbol = request.CurrencySymbol;
        if (request.Phone != null) location.Phone = request.Phone;
        if (request.Email != null) location.Email = request.Email;
        if (request.Website != null) location.Website = request.Website;
        if (request.AddressLine1 != null) location.AddressLine1 = request.AddressLine1;
        if (request.AddressLine2 != null) location.AddressLine2 = request.AddressLine2;
        if (request.City != null) location.City = request.City;
        if (request.State != null) location.State = request.State;
        if (request.PostalCode != null) location.PostalCode = request.PostalCode;
        if (request.Country != null) location.Country = request.Country;
        if (request.TaxNumber != null) location.TaxNumber = request.TaxNumber;
        if (request.BusinessName != null) location.BusinessName = request.BusinessName;
        if (request.IsActive.HasValue) location.IsActive = request.IsActive.Value;
        if (request.IsOpen.HasValue) location.IsOpen = request.IsOpen.Value;

        location.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(location);
        dto.AddSelfLink($"/api/locations/{locationId}");

        return Ok(dto);
    }

    [HttpPost("{locationId:guid}/open")]
    public async Task<ActionResult<LocationDto>> Open(Guid locationId)
    {
        var location = await _context.Locations.FindAsync(locationId);

        if (location == null)
            return NotFound();

        location.IsOpen = true;
        location.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(location);
        dto.AddSelfLink($"/api/locations/{locationId}");

        return Ok(dto);
    }

    [HttpPost("{locationId:guid}/close")]
    public async Task<ActionResult<LocationDto>> Close(Guid locationId)
    {
        var location = await _context.Locations.FindAsync(locationId);

        if (location == null)
            return NotFound();

        location.IsOpen = false;
        location.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(location);
        dto.AddSelfLink($"/api/locations/{locationId}");

        return Ok(dto);
    }

    [HttpDelete("{locationId:guid}")]
    public async Task<IActionResult> Delete(Guid locationId)
    {
        var location = await _context.Locations.FindAsync(locationId);

        if (location == null)
            return NotFound();

        // Soft delete by deactivating
        location.IsActive = false;
        location.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static string GetCurrencySymbol(string currencyCode)
    {
        return currencyCode.ToUpper() switch
        {
            "USD" => "$",
            "GBP" => "£",
            "EUR" => "€",
            "JPY" => "¥",
            "AUD" => "A$",
            "CAD" => "C$",
            _ => currencyCode
        };
    }

    private static LocationDto MapToDto(Entities.Location location)
    {
        return new LocationDto
        {
            Id = location.Id,
            Name = location.Name,
            Code = location.Code,
            Timezone = location.Timezone,
            CurrencyCode = location.CurrencyCode,
            CurrencySymbol = location.CurrencySymbol,
            Phone = location.Phone,
            Email = location.Email,
            Website = location.Website,
            Address = new AddressDto
            {
                Line1 = location.AddressLine1,
                Line2 = location.AddressLine2,
                City = location.City,
                State = location.State,
                PostalCode = location.PostalCode,
                Country = location.Country
            },
            TaxNumber = location.TaxNumber,
            BusinessName = location.BusinessName,
            IsActive = location.IsActive,
            IsOpen = location.IsOpen,
            Settings = location.Settings != null ? MapSettingsToDto(location.Settings) : null,
            OperatingHours = location.OperatingHours?.Select(MapHoursToDto).OrderBy(h => h.DayOfWeek).ToList()
        };
    }

    private static LocationSummaryDto MapToSummaryDto(Entities.Location location)
    {
        return new LocationSummaryDto
        {
            Id = location.Id,
            Name = location.Name,
            Code = location.Code,
            City = location.City,
            IsActive = location.IsActive,
            IsOpen = location.IsOpen
        };
    }

    private static LocationSettingsDto MapSettingsToDto(Entities.LocationSettings settings)
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

    private static OperatingHoursDto MapHoursToDto(Entities.OperatingHours hours)
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
