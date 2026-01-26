using DarkVelocity.Orders.Api.Data;
using DarkVelocity.Orders.Api.Dtos;
using DarkVelocity.Orders.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Orders.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/sales-periods")]
public class SalesPeriodsController : ControllerBase
{
    private readonly OrdersDbContext _context;

    public SalesPeriodsController(OrdersDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<SalesPeriodDto>>> GetAll(Guid locationId)
    {
        var periods = await _context.SalesPeriods
            .Include(p => p.Orders)
            .Where(p => p.LocationId == locationId)
            .OrderByDescending(p => p.OpenedAt)
            .Take(50)
            .Select(p => new SalesPeriodDto
            {
                Id = p.Id,
                LocationId = p.LocationId,
                OpenedByUserId = p.OpenedByUserId,
                ClosedByUserId = p.ClosedByUserId,
                OpenedAt = p.OpenedAt,
                ClosedAt = p.ClosedAt,
                OpeningCashAmount = p.OpeningCashAmount,
                ClosingCashAmount = p.ClosingCashAmount,
                ExpectedCashAmount = p.ExpectedCashAmount,
                Status = p.Status,
                OrderCount = p.Orders.Count
            })
            .ToListAsync();

        foreach (var period in periods)
        {
            period.AddSelfLink($"/api/locations/{locationId}/sales-periods/{period.Id}");
        }

        return Ok(HalCollection<SalesPeriodDto>.Create(
            periods,
            $"/api/locations/{locationId}/sales-periods",
            periods.Count
        ));
    }

    [HttpGet("current")]
    public async Task<ActionResult<SalesPeriodDto>> GetCurrent(Guid locationId)
    {
        var period = await _context.SalesPeriods
            .Include(p => p.Orders)
            .FirstOrDefaultAsync(p => p.LocationId == locationId && p.Status == "open");

        if (period == null)
            return NotFound(new { message = "No open sales period" });

        var dto = new SalesPeriodDto
        {
            Id = period.Id,
            LocationId = period.LocationId,
            OpenedByUserId = period.OpenedByUserId,
            ClosedByUserId = period.ClosedByUserId,
            OpenedAt = period.OpenedAt,
            ClosedAt = period.ClosedAt,
            OpeningCashAmount = period.OpeningCashAmount,
            ClosingCashAmount = period.ClosingCashAmount,
            ExpectedCashAmount = period.ExpectedCashAmount,
            Status = period.Status,
            OrderCount = period.Orders.Count
        };

        dto.AddSelfLink($"/api/locations/{locationId}/sales-periods/{period.Id}");

        return Ok(dto);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SalesPeriodDto>> GetById(Guid locationId, Guid id)
    {
        var period = await _context.SalesPeriods
            .Include(p => p.Orders)
            .FirstOrDefaultAsync(p => p.Id == id && p.LocationId == locationId);

        if (period == null)
            return NotFound();

        var dto = new SalesPeriodDto
        {
            Id = period.Id,
            LocationId = period.LocationId,
            OpenedByUserId = period.OpenedByUserId,
            ClosedByUserId = period.ClosedByUserId,
            OpenedAt = period.OpenedAt,
            ClosedAt = period.ClosedAt,
            OpeningCashAmount = period.OpeningCashAmount,
            ClosingCashAmount = period.ClosingCashAmount,
            ExpectedCashAmount = period.ExpectedCashAmount,
            Status = period.Status,
            OrderCount = period.Orders.Count
        };

        dto.AddSelfLink($"/api/locations/{locationId}/sales-periods/{period.Id}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<SalesPeriodDto>> Open(Guid locationId, [FromBody] OpenSalesPeriodRequest request)
    {
        var existingOpen = await _context.SalesPeriods
            .AnyAsync(p => p.LocationId == locationId && p.Status == "open");

        if (existingOpen)
            return Conflict(new { message = "A sales period is already open" });

        var period = new SalesPeriod
        {
            LocationId = locationId,
            OpenedByUserId = request.UserId,
            OpenedAt = DateTime.UtcNow,
            OpeningCashAmount = request.OpeningCashAmount,
            Status = "open"
        };

        _context.SalesPeriods.Add(period);
        await _context.SaveChangesAsync();

        var dto = new SalesPeriodDto
        {
            Id = period.Id,
            LocationId = period.LocationId,
            OpenedByUserId = period.OpenedByUserId,
            OpenedAt = period.OpenedAt,
            OpeningCashAmount = period.OpeningCashAmount,
            Status = period.Status,
            OrderCount = 0
        };

        dto.AddSelfLink($"/api/locations/{locationId}/sales-periods/{period.Id}");

        return CreatedAtAction(nameof(GetById), new { locationId, id = period.Id }, dto);
    }

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<SalesPeriodDto>> Close(Guid locationId, Guid id, [FromBody] CloseSalesPeriodRequest request)
    {
        var period = await _context.SalesPeriods
            .Include(p => p.Orders)
            .FirstOrDefaultAsync(p => p.Id == id && p.LocationId == locationId);

        if (period == null)
            return NotFound();

        if (period.Status != "open")
            return BadRequest(new { message = "Sales period is not open" });

        // Calculate expected cash (opening + cash payments - change given)
        var cashTotal = period.Orders
            .Where(o => o.Status == "completed")
            .Sum(o => o.GrandTotal);

        period.ClosedByUserId = request.UserId;
        period.ClosedAt = DateTime.UtcNow;
        period.ClosingCashAmount = request.ClosingCashAmount;
        period.ExpectedCashAmount = period.OpeningCashAmount + cashTotal;
        period.Status = "closed";

        await _context.SaveChangesAsync();

        var dto = new SalesPeriodDto
        {
            Id = period.Id,
            LocationId = period.LocationId,
            OpenedByUserId = period.OpenedByUserId,
            ClosedByUserId = period.ClosedByUserId,
            OpenedAt = period.OpenedAt,
            ClosedAt = period.ClosedAt,
            OpeningCashAmount = period.OpeningCashAmount,
            ClosingCashAmount = period.ClosingCashAmount,
            ExpectedCashAmount = period.ExpectedCashAmount,
            Status = period.Status,
            OrderCount = period.Orders.Count
        };

        dto.AddSelfLink($"/api/locations/{locationId}/sales-periods/{period.Id}");

        return Ok(dto);
    }
}
