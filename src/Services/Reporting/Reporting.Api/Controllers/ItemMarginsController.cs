using DarkVelocity.Reporting.Api.Data;
using DarkVelocity.Reporting.Api.Dtos;
using DarkVelocity.Reporting.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Reporting.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/reports/item-margins")]
public class ItemMarginsController : ControllerBase
{
    private readonly ReportingDbContext _context;

    public ItemMarginsController(ReportingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ItemMarginReportDto>> GetRange(
        Guid locationId,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? sortBy = "revenue") // revenue, margin, quantity, profit
    {
        var start = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _context.ItemSalesSummaries
            .Where(s => s.LocationId == locationId && s.Date >= start && s.Date <= end);

        if (categoryId.HasValue)
            query = query.Where(s => s.CategoryId == categoryId.Value);

        var summaries = await query.ToListAsync();

        // Aggregate by menu item
        var itemGroups = summaries
            .GroupBy(s => s.MenuItemId)
            .Select(g => new ItemMarginSummaryDto
            {
                MenuItemId = g.Key,
                MenuItemName = g.First().MenuItemName,
                CategoryName = g.First().CategoryName,
                QuantitySold = g.Sum(s => s.QuantitySold),
                NetRevenue = g.Sum(s => s.NetRevenue),
                TotalCOGS = g.Sum(s => s.TotalCOGS),
                GrossProfit = g.Sum(s => s.GrossProfit),
                GrossMarginPercent = g.Sum(s => s.NetRevenue) > 0
                    ? (g.Sum(s => s.GrossProfit) / g.Sum(s => s.NetRevenue)) * 100
                    : 0,
                ProfitPerUnit = g.Sum(s => s.QuantitySold) > 0
                    ? g.Sum(s => s.GrossProfit) / g.Sum(s => s.QuantitySold)
                    : 0
            });

        // Apply sorting
        itemGroups = sortBy switch
        {
            "margin" => itemGroups.OrderByDescending(i => i.GrossMarginPercent),
            "quantity" => itemGroups.OrderByDescending(i => i.QuantitySold),
            "profit" => itemGroups.OrderByDescending(i => i.GrossProfit),
            _ => itemGroups.OrderByDescending(i => i.NetRevenue)
        };

        var result = new ItemMarginReportDto
        {
            StartDate = start,
            EndDate = end,
            Items = itemGroups.ToList()
        };

        result.AddSelfLink($"/api/locations/{locationId}/reports/item-margins?startDate={start}&endDate={end}");

        return Ok(result);
    }

    [HttpGet("{menuItemId:guid}")]
    public async Task<ActionResult<ItemMarginReportDto>> GetByItem(
        Guid locationId,
        Guid menuItemId,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
    {
        var start = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var summaries = await _context.ItemSalesSummaries
            .Where(s => s.LocationId == locationId
                && s.MenuItemId == menuItemId
                && s.Date >= start
                && s.Date <= end)
            .OrderBy(s => s.Date)
            .ToListAsync();

        if (!summaries.Any())
            return NotFound();

        var totalRevenue = summaries.Sum(s => s.NetRevenue);
        var totalCOGS = summaries.Sum(s => s.TotalCOGS);
        var totalQuantity = summaries.Sum(s => s.QuantitySold);
        var totalProfit = summaries.Sum(s => s.GrossProfit);

        var itemSummary = new ItemMarginSummaryDto
        {
            MenuItemId = menuItemId,
            MenuItemName = summaries.First().MenuItemName,
            CategoryName = summaries.First().CategoryName,
            QuantitySold = totalQuantity,
            NetRevenue = totalRevenue,
            TotalCOGS = totalCOGS,
            GrossProfit = totalProfit,
            GrossMarginPercent = totalRevenue > 0 ? (totalProfit / totalRevenue) * 100 : 0,
            ProfitPerUnit = totalQuantity > 0 ? totalProfit / totalQuantity : 0
        };

        var result = new ItemMarginReportDto
        {
            StartDate = start,
            EndDate = end,
            Items = new List<ItemMarginSummaryDto> { itemSummary }
        };

        result.AddSelfLink($"/api/locations/{locationId}/reports/item-margins/{menuItemId}?startDate={start}&endDate={end}");

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ItemSalesSummaryDto>> Generate(
        Guid locationId,
        [FromBody] GenerateItemSummaryRequest request)
    {
        // Check if summary already exists
        var existing = await _context.ItemSalesSummaries
            .FirstOrDefaultAsync(s => s.LocationId == locationId
                && s.Date == request.Date
                && s.MenuItemId == request.MenuItemId);

        if (existing != null)
        {
            // Update existing
            existing.MenuItemName = request.MenuItemName;
            existing.CategoryId = request.CategoryId;
            existing.CategoryName = request.CategoryName;
            existing.QuantitySold = request.QuantitySold;
            existing.GrossRevenue = request.GrossRevenue;
            existing.DiscountTotal = request.DiscountTotal;
            existing.NetRevenue = request.GrossRevenue - request.DiscountTotal;
            existing.TotalCOGS = request.TotalCOGS;
            existing.GrossProfit = existing.NetRevenue - existing.TotalCOGS;
            existing.GrossMarginPercent = existing.NetRevenue > 0
                ? (existing.GrossProfit / existing.NetRevenue) * 100
                : 0;
            existing.AverageCostPerUnit = existing.QuantitySold > 0
                ? existing.TotalCOGS / existing.QuantitySold
                : 0;
            existing.ProfitPerUnit = existing.QuantitySold > 0
                ? existing.GrossProfit / existing.QuantitySold
                : 0;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(MapToDto(existing));
        }

        // Create new
        var netRevenue = request.GrossRevenue - request.DiscountTotal;
        var grossProfit = netRevenue - request.TotalCOGS;

        var summary = new ItemSalesSummary
        {
            LocationId = locationId,
            Date = request.Date,
            MenuItemId = request.MenuItemId,
            MenuItemName = request.MenuItemName,
            CategoryId = request.CategoryId,
            CategoryName = request.CategoryName,
            QuantitySold = request.QuantitySold,
            GrossRevenue = request.GrossRevenue,
            DiscountTotal = request.DiscountTotal,
            NetRevenue = netRevenue,
            TotalCOGS = request.TotalCOGS,
            GrossProfit = grossProfit,
            GrossMarginPercent = netRevenue > 0 ? (grossProfit / netRevenue) * 100 : 0,
            AverageCostPerUnit = request.QuantitySold > 0 ? request.TotalCOGS / request.QuantitySold : 0,
            ProfitPerUnit = request.QuantitySold > 0 ? grossProfit / request.QuantitySold : 0
        };

        _context.ItemSalesSummaries.Add(summary);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetByItem), new { locationId, menuItemId = request.MenuItemId }, MapToDto(summary));
    }

    private static ItemSalesSummaryDto MapToDto(ItemSalesSummary summary)
    {
        return new ItemSalesSummaryDto
        {
            Id = summary.Id,
            LocationId = summary.LocationId,
            Date = summary.Date,
            MenuItemId = summary.MenuItemId,
            MenuItemName = summary.MenuItemName,
            CategoryId = summary.CategoryId,
            CategoryName = summary.CategoryName,
            QuantitySold = summary.QuantitySold,
            GrossRevenue = summary.GrossRevenue,
            DiscountTotal = summary.DiscountTotal,
            NetRevenue = summary.NetRevenue,
            TotalCOGS = summary.TotalCOGS,
            AverageCostPerUnit = summary.AverageCostPerUnit,
            GrossProfit = summary.GrossProfit,
            GrossMarginPercent = summary.GrossMarginPercent,
            ProfitPerUnit = summary.ProfitPerUnit
        };
    }
}
