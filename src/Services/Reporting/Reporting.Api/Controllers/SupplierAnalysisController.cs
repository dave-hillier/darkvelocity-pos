using DarkVelocity.Reporting.Api.Data;
using DarkVelocity.Reporting.Api.Dtos;
using DarkVelocity.Reporting.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Reporting.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/reports/supplier-analysis")]
public class SupplierAnalysisController : ControllerBase
{
    private readonly ReportingDbContext _context;

    public SupplierAnalysisController(ReportingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<SupplierAnalysisReportDto>> GetRange(
        Guid locationId,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        [FromQuery] string? sortBy = "spend") // spend, deliveries, onTime
    {
        var start = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var summaries = await _context.SupplierSpendSummaries
            .Where(s => s.LocationId == locationId
                && s.PeriodStart >= start
                && s.PeriodEnd <= end)
            .ToListAsync();

        // Apply sorting
        IEnumerable<SupplierSpendSummary> sorted = sortBy switch
        {
            "deliveries" => summaries.OrderByDescending(s => s.DeliveryCount),
            "onTime" => summaries.OrderByDescending(s => s.OnTimePercentage),
            _ => summaries.OrderByDescending(s => s.TotalSpend)
        };

        var dtos = sorted.Select(MapToDto).ToList();

        var result = new SupplierAnalysisReportDto
        {
            StartDate = start,
            EndDate = end,
            Suppliers = dtos,
            TotalSpend = summaries.Sum(s => s.TotalSpend)
        };

        result.AddSelfLink($"/api/locations/{locationId}/reports/supplier-analysis?startDate={start}&endDate={end}");

        return Ok(result);
    }

    [HttpGet("{supplierId:guid}")]
    public async Task<ActionResult<SupplierSpendSummaryDto>> GetBySupplier(
        Guid locationId,
        Guid supplierId,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
    {
        var start = startDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var summary = await _context.SupplierSpendSummaries
            .FirstOrDefaultAsync(s => s.LocationId == locationId
                && s.SupplierId == supplierId
                && s.PeriodStart >= start
                && s.PeriodEnd <= end);

        if (summary == null)
            return NotFound();

        var dto = MapToDto(summary);
        dto.AddSelfLink($"/api/locations/{locationId}/reports/supplier-analysis/{supplierId}?startDate={start}&endDate={end}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<SupplierSpendSummaryDto>> Generate(
        Guid locationId,
        [FromBody] GenerateSupplierSummaryRequest request)
    {
        // Check if summary already exists
        var existing = await _context.SupplierSpendSummaries
            .FirstOrDefaultAsync(s => s.LocationId == locationId
                && s.SupplierId == request.SupplierId
                && s.PeriodStart == request.PeriodStart
                && s.PeriodEnd == request.PeriodEnd);

        if (existing != null)
        {
            // Update existing
            existing.SupplierName = request.SupplierName;
            existing.TotalSpend = request.TotalSpend;
            existing.DeliveryCount = request.DeliveryCount;
            existing.AverageDeliveryValue = request.DeliveryCount > 0
                ? request.TotalSpend / request.DeliveryCount
                : 0;
            existing.OnTimeDeliveries = request.OnTimeDeliveries;
            existing.LateDeliveries = request.LateDeliveries;
            existing.OnTimePercentage = request.DeliveryCount > 0
                ? ((decimal)request.OnTimeDeliveries / request.DeliveryCount) * 100
                : 0;
            existing.DiscrepancyCount = request.DiscrepancyCount;
            existing.DiscrepancyValue = request.DiscrepancyValue;
            existing.DiscrepancyRate = request.DeliveryCount > 0
                ? ((decimal)request.DiscrepancyCount / request.DeliveryCount) * 100
                : 0;
            existing.UniqueProductsOrdered = request.UniqueProductsOrdered;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(MapToDto(existing));
        }

        // Create new
        var summary = new SupplierSpendSummary
        {
            LocationId = locationId,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            SupplierId = request.SupplierId,
            SupplierName = request.SupplierName,
            TotalSpend = request.TotalSpend,
            DeliveryCount = request.DeliveryCount,
            AverageDeliveryValue = request.DeliveryCount > 0 ? request.TotalSpend / request.DeliveryCount : 0,
            OnTimeDeliveries = request.OnTimeDeliveries,
            LateDeliveries = request.LateDeliveries,
            OnTimePercentage = request.DeliveryCount > 0 ? ((decimal)request.OnTimeDeliveries / request.DeliveryCount) * 100 : 0,
            DiscrepancyCount = request.DiscrepancyCount,
            DiscrepancyValue = request.DiscrepancyValue,
            DiscrepancyRate = request.DeliveryCount > 0 ? ((decimal)request.DiscrepancyCount / request.DeliveryCount) * 100 : 0,
            UniqueProductsOrdered = request.UniqueProductsOrdered
        };

        _context.SupplierSpendSummaries.Add(summary);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetBySupplier), new { locationId, supplierId = request.SupplierId }, MapToDto(summary));
    }

    private static SupplierSpendSummaryDto MapToDto(SupplierSpendSummary summary)
    {
        return new SupplierSpendSummaryDto
        {
            Id = summary.Id,
            LocationId = summary.LocationId,
            PeriodStart = summary.PeriodStart,
            PeriodEnd = summary.PeriodEnd,
            SupplierId = summary.SupplierId,
            SupplierName = summary.SupplierName,
            TotalSpend = summary.TotalSpend,
            DeliveryCount = summary.DeliveryCount,
            AverageDeliveryValue = summary.AverageDeliveryValue,
            OnTimeDeliveries = summary.OnTimeDeliveries,
            LateDeliveries = summary.LateDeliveries,
            OnTimePercentage = summary.OnTimePercentage,
            DiscrepancyCount = summary.DiscrepancyCount,
            DiscrepancyValue = summary.DiscrepancyValue,
            DiscrepancyRate = summary.DiscrepancyRate,
            UniqueProductsOrdered = summary.UniqueProductsOrdered
        };
    }
}
