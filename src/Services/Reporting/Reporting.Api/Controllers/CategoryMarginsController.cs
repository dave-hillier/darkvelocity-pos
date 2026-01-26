using DarkVelocity.Reporting.Api.Data;
using DarkVelocity.Reporting.Api.Dtos;
using DarkVelocity.Reporting.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Reporting.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/reports/category-margins")]
public class CategoryMarginsController : ControllerBase
{
    private readonly ReportingDbContext _context;

    public CategoryMarginsController(ReportingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<CategoryMarginReportDto>> GetRange(
        Guid locationId,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        [FromQuery] string? sortBy = "revenue") // revenue, margin, itemsSold
    {
        var start = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var summaries = await _context.CategorySalesSummaries
            .Where(s => s.LocationId == locationId && s.Date >= start && s.Date <= end)
            .ToListAsync();

        // Calculate total revenue for percentage calculation
        var totalRevenue = summaries.Sum(s => s.NetRevenue);

        // Aggregate by category
        var categoryGroups = summaries
            .GroupBy(s => s.CategoryId)
            .Select(g => new CategoryMarginSummaryDto
            {
                CategoryId = g.Key,
                CategoryName = g.First().CategoryName,
                ItemsSold = g.Sum(s => s.ItemsSold),
                NetRevenue = g.Sum(s => s.NetRevenue),
                TotalCOGS = g.Sum(s => s.TotalCOGS),
                GrossProfit = g.Sum(s => s.GrossProfit),
                GrossMarginPercent = g.Sum(s => s.NetRevenue) > 0
                    ? (g.Sum(s => s.GrossProfit) / g.Sum(s => s.NetRevenue)) * 100
                    : 0,
                RevenuePercentOfTotal = totalRevenue > 0
                    ? (g.Sum(s => s.NetRevenue) / totalRevenue) * 100
                    : 0
            });

        // Apply sorting
        categoryGroups = sortBy switch
        {
            "margin" => categoryGroups.OrderByDescending(c => c.GrossMarginPercent),
            "itemsSold" => categoryGroups.OrderByDescending(c => c.ItemsSold),
            _ => categoryGroups.OrderByDescending(c => c.NetRevenue)
        };

        var result = new CategoryMarginReportDto
        {
            StartDate = start,
            EndDate = end,
            Categories = categoryGroups.ToList()
        };

        result.AddSelfLink($"/api/locations/{locationId}/reports/category-margins?startDate={start}&endDate={end}");

        return Ok(result);
    }

    [HttpGet("{categoryId:guid}")]
    public async Task<ActionResult<CategoryMarginReportDto>> GetByCategory(
        Guid locationId,
        Guid categoryId,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
    {
        var start = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var summaries = await _context.CategorySalesSummaries
            .Where(s => s.LocationId == locationId
                && s.CategoryId == categoryId
                && s.Date >= start
                && s.Date <= end)
            .OrderBy(s => s.Date)
            .ToListAsync();

        if (!summaries.Any())
            return NotFound();

        var totalRevenue = summaries.Sum(s => s.NetRevenue);
        var totalCOGS = summaries.Sum(s => s.TotalCOGS);
        var totalItems = summaries.Sum(s => s.ItemsSold);
        var totalProfit = summaries.Sum(s => s.GrossProfit);

        var categorySummary = new CategoryMarginSummaryDto
        {
            CategoryId = categoryId,
            CategoryName = summaries.First().CategoryName,
            ItemsSold = totalItems,
            NetRevenue = totalRevenue,
            TotalCOGS = totalCOGS,
            GrossProfit = totalProfit,
            GrossMarginPercent = totalRevenue > 0 ? (totalProfit / totalRevenue) * 100 : 0,
            RevenuePercentOfTotal = 100 // Single category view
        };

        var result = new CategoryMarginReportDto
        {
            StartDate = start,
            EndDate = end,
            Categories = new List<CategoryMarginSummaryDto> { categorySummary }
        };

        result.AddSelfLink($"/api/locations/{locationId}/reports/category-margins/{categoryId}?startDate={start}&endDate={end}");

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CategorySalesSummaryDto>> Generate(
        Guid locationId,
        [FromBody] GenerateCategorySummaryRequest request)
    {
        // Check if summary already exists
        var existing = await _context.CategorySalesSummaries
            .FirstOrDefaultAsync(s => s.LocationId == locationId
                && s.Date == request.Date
                && s.CategoryId == request.CategoryId);

        if (existing != null)
        {
            // Update existing
            existing.CategoryName = request.CategoryName;
            existing.ItemsSold = request.ItemsSold;
            existing.GrossRevenue = request.GrossRevenue;
            existing.DiscountTotal = request.DiscountTotal;
            existing.NetRevenue = request.GrossRevenue - request.DiscountTotal;
            existing.TotalCOGS = request.TotalCOGS;
            existing.GrossProfit = existing.NetRevenue - existing.TotalCOGS;
            existing.GrossMarginPercent = existing.NetRevenue > 0
                ? (existing.GrossProfit / existing.NetRevenue) * 100
                : 0;
            existing.RevenuePercentOfTotal = request.TotalDayRevenue > 0
                ? (existing.NetRevenue / request.TotalDayRevenue) * 100
                : 0;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(MapToDto(existing));
        }

        // Create new
        var netRevenue = request.GrossRevenue - request.DiscountTotal;
        var grossProfit = netRevenue - request.TotalCOGS;

        var summary = new CategorySalesSummary
        {
            LocationId = locationId,
            Date = request.Date,
            CategoryId = request.CategoryId,
            CategoryName = request.CategoryName,
            ItemsSold = request.ItemsSold,
            GrossRevenue = request.GrossRevenue,
            DiscountTotal = request.DiscountTotal,
            NetRevenue = netRevenue,
            TotalCOGS = request.TotalCOGS,
            GrossProfit = grossProfit,
            GrossMarginPercent = netRevenue > 0 ? (grossProfit / netRevenue) * 100 : 0,
            RevenuePercentOfTotal = request.TotalDayRevenue > 0 ? (netRevenue / request.TotalDayRevenue) * 100 : 0
        };

        _context.CategorySalesSummaries.Add(summary);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetByCategory), new { locationId, categoryId = request.CategoryId }, MapToDto(summary));
    }

    private static CategorySalesSummaryDto MapToDto(CategorySalesSummary summary)
    {
        return new CategorySalesSummaryDto
        {
            Id = summary.Id,
            LocationId = summary.LocationId,
            Date = summary.Date,
            CategoryId = summary.CategoryId,
            CategoryName = summary.CategoryName,
            ItemsSold = summary.ItemsSold,
            GrossRevenue = summary.GrossRevenue,
            DiscountTotal = summary.DiscountTotal,
            NetRevenue = summary.NetRevenue,
            TotalCOGS = summary.TotalCOGS,
            GrossProfit = summary.GrossProfit,
            GrossMarginPercent = summary.GrossMarginPercent,
            RevenuePercentOfTotal = summary.RevenuePercentOfTotal
        };
    }
}
