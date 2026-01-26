using DarkVelocity.Reporting.Api.Data;
using DarkVelocity.Reporting.Api.Dtos;
using DarkVelocity.Reporting.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Reporting.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/reports/daily-sales")]
public class DailySalesController : ControllerBase
{
    private readonly ReportingDbContext _context;

    public DailySalesController(ReportingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<DailySalesRangeDto>> GetRange(
        Guid locationId,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
    {
        var start = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var summaries = await _context.DailySalesSummaries
            .Where(s => s.LocationId == locationId && s.Date >= start && s.Date <= end)
            .OrderBy(s => s.Date)
            .ToListAsync();

        var days = summaries.Select(MapToDto).ToList();

        var totals = new DailySalesTotalsDto
        {
            GrossRevenue = summaries.Sum(s => s.GrossRevenue),
            NetRevenue = summaries.Sum(s => s.NetRevenue),
            TotalCOGS = summaries.Sum(s => s.TotalCOGS),
            GrossProfit = summaries.Sum(s => s.GrossProfit),
            OrderCount = summaries.Sum(s => s.OrderCount),
            ItemsSold = summaries.Sum(s => s.ItemsSold)
        };

        if (totals.NetRevenue > 0)
        {
            totals.GrossMarginPercent = (totals.GrossProfit / totals.NetRevenue) * 100;
        }

        var result = new DailySalesRangeDto
        {
            StartDate = start,
            EndDate = end,
            Days = days,
            Totals = totals
        };

        result.AddSelfLink($"/api/locations/{locationId}/reports/daily-sales?startDate={start}&endDate={end}");

        return Ok(result);
    }

    [HttpGet("{date}")]
    public async Task<ActionResult<DailySalesSummaryDto>> GetByDate(Guid locationId, DateOnly date)
    {
        var summary = await _context.DailySalesSummaries
            .FirstOrDefaultAsync(s => s.LocationId == locationId && s.Date == date);

        if (summary == null)
            return NotFound();

        var dto = MapToDto(summary);
        dto.AddSelfLink($"/api/locations/{locationId}/reports/daily-sales/{date}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<DailySalesSummaryDto>> Generate(
        Guid locationId,
        [FromBody] GenerateDailySummaryRequest request)
    {
        // Check if summary already exists
        var existing = await _context.DailySalesSummaries
            .FirstOrDefaultAsync(s => s.LocationId == locationId && s.Date == request.Date);

        if (existing != null)
        {
            // Update existing
            existing.GrossRevenue = request.GrossRevenue;
            existing.DiscountTotal = request.DiscountTotal;
            existing.NetRevenue = request.GrossRevenue - request.DiscountTotal;
            existing.TaxCollected = request.TaxCollected;
            existing.TotalCOGS = request.TotalCOGS;
            existing.OrderCount = request.OrderCount;
            existing.ItemsSold = request.ItemsSold;
            existing.TipsCollected = request.TipsCollected;
            existing.CashTotal = request.CashTotal;
            existing.CardTotal = request.CardTotal;
            existing.OtherPaymentTotal = request.OtherPaymentTotal;
            existing.RefundCount = request.RefundCount;
            existing.RefundTotal = request.RefundTotal;

            // Calculate margins
            existing.GrossProfit = existing.NetRevenue - existing.TotalCOGS;
            existing.GrossMarginPercent = existing.NetRevenue > 0
                ? (existing.GrossProfit / existing.NetRevenue) * 100
                : 0;
            existing.AverageOrderValue = existing.OrderCount > 0
                ? existing.NetRevenue / existing.OrderCount
                : 0;

            existing.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var existingDto = MapToDto(existing);
            existingDto.AddSelfLink($"/api/locations/{locationId}/reports/daily-sales/{request.Date}");
            return Ok(existingDto);
        }

        // Create new
        var netRevenue = request.GrossRevenue - request.DiscountTotal;
        var grossProfit = netRevenue - request.TotalCOGS;

        var summary = new DailySalesSummary
        {
            LocationId = locationId,
            Date = request.Date,
            GrossRevenue = request.GrossRevenue,
            DiscountTotal = request.DiscountTotal,
            NetRevenue = netRevenue,
            TaxCollected = request.TaxCollected,
            TotalCOGS = request.TotalCOGS,
            GrossProfit = grossProfit,
            GrossMarginPercent = netRevenue > 0 ? (grossProfit / netRevenue) * 100 : 0,
            OrderCount = request.OrderCount,
            ItemsSold = request.ItemsSold,
            AverageOrderValue = request.OrderCount > 0 ? netRevenue / request.OrderCount : 0,
            TipsCollected = request.TipsCollected,
            CashTotal = request.CashTotal,
            CardTotal = request.CardTotal,
            OtherPaymentTotal = request.OtherPaymentTotal,
            RefundCount = request.RefundCount,
            RefundTotal = request.RefundTotal
        };

        _context.DailySalesSummaries.Add(summary);
        await _context.SaveChangesAsync();

        var dto = MapToDto(summary);
        dto.AddSelfLink($"/api/locations/{locationId}/reports/daily-sales/{request.Date}");

        return CreatedAtAction(nameof(GetByDate), new { locationId, date = request.Date }, dto);
    }

    private static DailySalesSummaryDto MapToDto(DailySalesSummary summary)
    {
        return new DailySalesSummaryDto
        {
            Id = summary.Id,
            LocationId = summary.LocationId,
            Date = summary.Date,
            GrossRevenue = summary.GrossRevenue,
            DiscountTotal = summary.DiscountTotal,
            NetRevenue = summary.NetRevenue,
            TaxCollected = summary.TaxCollected,
            TotalCOGS = summary.TotalCOGS,
            GrossProfit = summary.GrossProfit,
            GrossMarginPercent = summary.GrossMarginPercent,
            OrderCount = summary.OrderCount,
            ItemsSold = summary.ItemsSold,
            AverageOrderValue = summary.AverageOrderValue,
            TipsCollected = summary.TipsCollected,
            CashTotal = summary.CashTotal,
            CardTotal = summary.CardTotal,
            OtherPaymentTotal = summary.OtherPaymentTotal,
            RefundCount = summary.RefundCount,
            RefundTotal = summary.RefundTotal
        };
    }
}
