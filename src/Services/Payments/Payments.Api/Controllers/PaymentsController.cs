using System.Text.Json;
using DarkVelocity.Payments.Api.Data;
using DarkVelocity.Payments.Api.Dtos;
using DarkVelocity.Payments.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Payments.Api.Controllers;

[ApiController]
[Route("api/locations/{locationId:guid}/payments")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentsDbContext _context;

    public PaymentsController(PaymentsDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<PaymentDto>>> GetAll(
        Guid locationId,
        [FromQuery] Guid? orderId = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var query = _context.Payments
            .Include(p => p.PaymentMethod)
            .Where(p => p.LocationId == locationId)
            .AsQueryable();

        if (orderId.HasValue)
            query = query.Where(p => p.OrderId == orderId.Value);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(p => p.Status == status);
        if (fromDate.HasValue)
            query = query.Where(p => p.CreatedAt >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(p => p.CreatedAt <= toDate.Value);

        var payments = await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(100)
            .Select(p => new PaymentDto
            {
                Id = p.Id,
                LocationId = p.LocationId,
                OrderId = p.OrderId,
                UserId = p.UserId,
                PaymentMethodId = p.PaymentMethodId,
                PaymentMethodName = p.PaymentMethod != null ? p.PaymentMethod.Name : null,
                PaymentMethodType = p.PaymentMethod != null ? p.PaymentMethod.MethodType : null,
                Amount = p.Amount,
                TipAmount = p.TipAmount,
                TotalAmount = p.Amount + p.TipAmount,
                ReceivedAmount = p.ReceivedAmount,
                ChangeAmount = p.ChangeAmount,
                Status = p.Status,
                CardBrand = p.CardBrand,
                CardLastFour = p.CardLastFour,
                CreatedAt = p.CreatedAt,
                CompletedAt = p.CompletedAt
            })
            .ToListAsync();

        foreach (var payment in payments)
        {
            payment.AddSelfLink($"/api/locations/{locationId}/payments/{payment.Id}");
        }

        return Ok(HalCollection<PaymentDto>.Create(
            payments,
            $"/api/locations/{locationId}/payments",
            payments.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentDto>> GetById(Guid locationId, Guid id)
    {
        var payment = await _context.Payments
            .Include(p => p.PaymentMethod)
            .FirstOrDefaultAsync(p => p.LocationId == locationId && p.Id == id);

        if (payment == null)
            return NotFound();

        var dto = MapToDto(payment);
        dto.AddSelfLink($"/api/locations/{locationId}/payments/{payment.Id}");

        return Ok(dto);
    }

    [HttpGet("by-order/{orderId:guid}")]
    public async Task<ActionResult<HalCollection<PaymentDto>>> GetByOrder(Guid locationId, Guid orderId)
    {
        var payments = await _context.Payments
            .Include(p => p.PaymentMethod)
            .Where(p => p.LocationId == locationId && p.OrderId == orderId)
            .OrderBy(p => p.CreatedAt)
            .Select(p => MapToDto(p))
            .ToListAsync();

        foreach (var payment in payments)
        {
            payment.AddSelfLink($"/api/locations/{locationId}/payments/{payment.Id}");
        }

        return Ok(HalCollection<PaymentDto>.Create(
            payments,
            $"/api/locations/{locationId}/payments/by-order/{orderId}",
            payments.Count
        ));
    }

    [HttpPost("cash")]
    public async Task<ActionResult<PaymentDto>> CreateCashPayment(
        Guid locationId,
        [FromBody] CreateCashPaymentRequest request)
    {
        var paymentMethod = await _context.PaymentMethods
            .FirstOrDefaultAsync(pm => pm.Id == request.PaymentMethodId && pm.LocationId == locationId);

        if (paymentMethod == null)
            return BadRequest(new { message = "Invalid payment method" });

        if (paymentMethod.MethodType != "cash")
            return BadRequest(new { message = "Payment method is not a cash method" });

        var totalDue = request.Amount + request.TipAmount;
        var receivedAmount = request.ReceivedAmount > 0 ? request.ReceivedAmount : totalDue;

        if (receivedAmount < totalDue)
            return BadRequest(new { message = "Received amount is less than amount due" });

        var changeAmount = receivedAmount - totalDue;

        var payment = new Payment
        {
            LocationId = locationId,
            OrderId = request.OrderId,
            UserId = request.UserId,
            PaymentMethodId = request.PaymentMethodId,
            Amount = request.Amount,
            TipAmount = request.TipAmount,
            ReceivedAmount = receivedAmount,
            ChangeAmount = changeAmount,
            Status = "completed",
            CompletedAt = DateTime.UtcNow
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        await _context.Entry(payment).Reference(p => p.PaymentMethod).LoadAsync();

        var dto = MapToDto(payment);
        dto.AddSelfLink($"/api/locations/{locationId}/payments/{payment.Id}");

        return CreatedAtAction(nameof(GetById), new { locationId, id = payment.Id }, dto);
    }

    [HttpPost("card")]
    public async Task<ActionResult<PaymentDto>> CreateCardPayment(
        Guid locationId,
        [FromBody] CreateCardPaymentRequest request)
    {
        var paymentMethod = await _context.PaymentMethods
            .FirstOrDefaultAsync(pm => pm.Id == request.PaymentMethodId && pm.LocationId == locationId);

        if (paymentMethod == null)
            return BadRequest(new { message = "Invalid payment method" });

        if (paymentMethod.MethodType != "card")
            return BadRequest(new { message = "Payment method is not a card method" });

        var payment = new Payment
        {
            LocationId = locationId,
            OrderId = request.OrderId,
            UserId = request.UserId,
            PaymentMethodId = request.PaymentMethodId,
            Amount = request.Amount,
            TipAmount = request.TipAmount,
            ReceivedAmount = request.Amount + request.TipAmount,
            ChangeAmount = 0,
            Status = "pending"
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        await _context.Entry(payment).Reference(p => p.PaymentMethod).LoadAsync();

        var dto = MapToDto(payment);
        dto.AddSelfLink($"/api/locations/{locationId}/payments/{payment.Id}");

        return CreatedAtAction(nameof(GetById), new { locationId, id = payment.Id }, dto);
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<PaymentDto>> CompleteCardPayment(
        Guid locationId,
        Guid id,
        [FromBody] CompleteCardPaymentRequest request)
    {
        var payment = await _context.Payments
            .Include(p => p.PaymentMethod)
            .FirstOrDefaultAsync(p => p.LocationId == locationId && p.Id == id);

        if (payment == null)
            return NotFound();

        if (payment.Status != "pending")
            return BadRequest(new { message = "Payment is not in pending status" });

        payment.StripePaymentIntentId = request.StripePaymentIntentId;
        payment.CardBrand = request.CardBrand;
        payment.CardLastFour = request.CardLastFour;
        payment.Status = "completed";
        payment.CompletedAt = DateTime.UtcNow;
        payment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(payment);
        dto.AddSelfLink($"/api/locations/{locationId}/payments/{payment.Id}");

        return Ok(dto);
    }

    [HttpPost("{id:guid}/refund")]
    public async Task<ActionResult<PaymentDto>> Refund(
        Guid locationId,
        Guid id,
        [FromBody] RefundPaymentRequest request)
    {
        var payment = await _context.Payments
            .Include(p => p.PaymentMethod)
            .FirstOrDefaultAsync(p => p.LocationId == locationId && p.Id == id);

        if (payment == null)
            return NotFound();

        if (payment.Status != "completed")
            return BadRequest(new { message = "Can only refund completed payments" });

        payment.Status = "refunded";
        payment.RefundedAt = DateTime.UtcNow;
        payment.RefundReason = request.Reason;
        payment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(payment);
        dto.AddSelfLink($"/api/locations/{locationId}/payments/{payment.Id}");

        return Ok(dto);
    }

    [HttpPost("{id:guid}/void")]
    public async Task<ActionResult<PaymentDto>> Void(
        Guid locationId,
        Guid id,
        [FromBody] VoidPaymentRequest request)
    {
        var payment = await _context.Payments
            .Include(p => p.PaymentMethod)
            .FirstOrDefaultAsync(p => p.LocationId == locationId && p.Id == id);

        if (payment == null)
            return NotFound();

        if (payment.Status == "refunded" || payment.Status == "voided")
            return BadRequest(new { message = "Payment already refunded or voided" });

        payment.Status = "voided";
        payment.RefundReason = request.Reason;
        payment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(payment);
        dto.AddSelfLink($"/api/locations/{locationId}/payments/{payment.Id}");

        return Ok(dto);
    }

    private static PaymentDto MapToDto(Payment payment)
    {
        return new PaymentDto
        {
            Id = payment.Id,
            LocationId = payment.LocationId,
            OrderId = payment.OrderId,
            UserId = payment.UserId,
            PaymentMethodId = payment.PaymentMethodId,
            PaymentMethodName = payment.PaymentMethod?.Name,
            PaymentMethodType = payment.PaymentMethod?.MethodType,
            Amount = payment.Amount,
            TipAmount = payment.TipAmount,
            TotalAmount = payment.Amount + payment.TipAmount,
            ReceivedAmount = payment.ReceivedAmount,
            ChangeAmount = payment.ChangeAmount,
            Status = payment.Status,
            CardBrand = payment.CardBrand,
            CardLastFour = payment.CardLastFour,
            CreatedAt = payment.CreatedAt,
            CompletedAt = payment.CompletedAt
        };
    }
}
