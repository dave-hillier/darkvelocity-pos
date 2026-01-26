using DarkVelocity.Payments.Api.Data;
using DarkVelocity.Payments.Api.Dtos;
using DarkVelocity.Payments.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Payments.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/payment-methods")]
public class PaymentMethodsController : ControllerBase
{
    private readonly PaymentsDbContext _context;

    public PaymentMethodsController(PaymentsDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<PaymentMethodDto>>> GetAll(
        Guid locationId,
        [FromQuery] bool activeOnly = false)
    {
        var query = _context.PaymentMethods
            .Where(pm => pm.LocationId == locationId)
            .AsQueryable();

        if (activeOnly)
            query = query.Where(pm => pm.IsActive);

        var methods = await query
            .OrderBy(pm => pm.DisplayOrder)
            .ThenBy(pm => pm.Name)
            .Select(pm => new PaymentMethodDto
            {
                Id = pm.Id,
                LocationId = pm.LocationId,
                Name = pm.Name,
                MethodType = pm.MethodType,
                IsActive = pm.IsActive,
                RequiresTip = pm.RequiresTip,
                OpensDrawer = pm.OpensDrawer,
                DisplayOrder = pm.DisplayOrder,
                RequiresExternalTerminal = pm.RequiresExternalTerminal
            })
            .ToListAsync();

        foreach (var method in methods)
        {
            method.AddSelfLink($"/api/locations/{locationId}/payment-methods/{method.Id}");
        }

        return Ok(HalCollection<PaymentMethodDto>.Create(
            methods,
            $"/api/locations/{locationId}/payment-methods",
            methods.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentMethodDto>> GetById(Guid locationId, Guid id)
    {
        var method = await _context.PaymentMethods
            .FirstOrDefaultAsync(pm => pm.LocationId == locationId && pm.Id == id);

        if (method == null)
            return NotFound();

        var dto = MapToDto(method);
        dto.AddSelfLink($"/api/locations/{locationId}/payment-methods/{method.Id}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<PaymentMethodDto>> Create(
        Guid locationId,
        [FromBody] CreatePaymentMethodRequest request)
    {
        // Check for duplicate name
        var exists = await _context.PaymentMethods
            .AnyAsync(pm => pm.LocationId == locationId && pm.Name == request.Name);

        if (exists)
            return Conflict(new { message = "A payment method with this name already exists" });

        var method = new PaymentMethod
        {
            LocationId = locationId,
            Name = request.Name,
            MethodType = request.MethodType,
            RequiresTip = request.RequiresTip,
            OpensDrawer = request.OpensDrawer,
            DisplayOrder = request.DisplayOrder,
            RequiresExternalTerminal = request.RequiresExternalTerminal
        };

        _context.PaymentMethods.Add(method);
        await _context.SaveChangesAsync();

        var dto = MapToDto(method);
        dto.AddSelfLink($"/api/locations/{locationId}/payment-methods/{method.Id}");

        return CreatedAtAction(nameof(GetById), new { locationId, id = method.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PaymentMethodDto>> Update(
        Guid locationId,
        Guid id,
        [FromBody] UpdatePaymentMethodRequest request)
    {
        var method = await _context.PaymentMethods
            .FirstOrDefaultAsync(pm => pm.LocationId == locationId && pm.Id == id);

        if (method == null)
            return NotFound();

        if (request.Name != null)
        {
            // Check for duplicate name (excluding self)
            var duplicate = await _context.PaymentMethods
                .AnyAsync(pm => pm.LocationId == locationId && pm.Name == request.Name && pm.Id != id);

            if (duplicate)
                return Conflict(new { message = "A payment method with this name already exists" });

            method.Name = request.Name;
        }

        if (request.IsActive.HasValue)
            method.IsActive = request.IsActive.Value;
        if (request.RequiresTip.HasValue)
            method.RequiresTip = request.RequiresTip.Value;
        if (request.OpensDrawer.HasValue)
            method.OpensDrawer = request.OpensDrawer.Value;
        if (request.DisplayOrder.HasValue)
            method.DisplayOrder = request.DisplayOrder.Value;
        if (request.RequiresExternalTerminal.HasValue)
            method.RequiresExternalTerminal = request.RequiresExternalTerminal.Value;

        method.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = MapToDto(method);
        dto.AddSelfLink($"/api/locations/{locationId}/payment-methods/{method.Id}");

        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid locationId, Guid id)
    {
        var method = await _context.PaymentMethods
            .FirstOrDefaultAsync(pm => pm.LocationId == locationId && pm.Id == id);

        if (method == null)
            return NotFound();

        // Check if payment method has been used
        var hasPayments = await _context.Payments.AnyAsync(p => p.PaymentMethodId == id);
        if (hasPayments)
        {
            // Soft delete by deactivating
            method.IsActive = false;
            method.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
        else
        {
            // Hard delete if never used
            _context.PaymentMethods.Remove(method);
            await _context.SaveChangesAsync();
        }

        return NoContent();
    }

    private static PaymentMethodDto MapToDto(PaymentMethod method)
    {
        return new PaymentMethodDto
        {
            Id = method.Id,
            LocationId = method.LocationId,
            Name = method.Name,
            MethodType = method.MethodType,
            IsActive = method.IsActive,
            RequiresTip = method.RequiresTip,
            OpensDrawer = method.OpensDrawer,
            DisplayOrder = method.DisplayOrder,
            RequiresExternalTerminal = method.RequiresExternalTerminal
        };
    }
}
