using System.Text.Json;
using DarkVelocity.PaymentGateway.Api.Data;
using DarkVelocity.PaymentGateway.Api.Dtos;
using DarkVelocity.PaymentGateway.Api.Entities;
using DarkVelocity.PaymentGateway.Api.Services;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.PaymentGateway.Api.Controllers;

[ApiController]
[Route("api/v1/refunds")]
public class RefundsController : ControllerBase
{
    private readonly PaymentGatewayDbContext _context;
    private readonly KeyGenerationService _keyService;
    private readonly WebhookService _webhookService;

    public RefundsController(
        PaymentGatewayDbContext context,
        KeyGenerationService keyService,
        WebhookService webhookService)
    {
        _context = context;
        _keyService = keyService;
        _webhookService = webhookService;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<RefundDto>>> GetAll(
        [FromQuery] Guid? payment_intent = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? created_gte = null,
        [FromQuery] DateTime? created_lte = null,
        [FromQuery] int limit = 20,
        [FromQuery] string? starting_after = null)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var query = _context.Refunds
            .Where(r => r.MerchantId == merchantId)
            .AsQueryable();

        if (payment_intent.HasValue)
            query = query.Where(r => r.PaymentIntentId == payment_intent.Value);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status == status);
        if (created_gte.HasValue)
            query = query.Where(r => r.CreatedAt >= created_gte.Value);
        if (created_lte.HasValue)
            query = query.Where(r => r.CreatedAt <= created_lte.Value);
        if (!string.IsNullOrEmpty(starting_after) && Guid.TryParse(starting_after, out var afterId))
        {
            var afterRefund = await _context.Refunds.FindAsync(afterId);
            if (afterRefund != null)
                query = query.Where(r => r.CreatedAt < afterRefund.CreatedAt);
        }

        var refunds = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(Math.Min(limit, 100))
            .ToListAsync();

        var dtos = refunds.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/v1/refunds/{dto.Id}");
            dto.AddLink("payment_intent", $"/api/v1/payment_intents/{dto.PaymentIntentId}");
        }

        var collection = HalCollection<RefundDto>.Create(
            dtos,
            "/api/v1/refunds",
            await query.CountAsync()
        );

        if (dtos.Any())
        {
            var queryParams = new List<string> { $"starting_after={dtos.Last().Id}", $"limit={limit}" };
            if (payment_intent.HasValue)
                queryParams.Add($"payment_intent={payment_intent}");
            collection.AddLink("next", $"/api/v1/refunds?{string.Join("&", queryParams)}");
        }

        return Ok(collection);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RefundDto>> GetById(Guid id)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var refund = await _context.Refunds
            .FirstOrDefaultAsync(r => r.Id == id && r.MerchantId == merchantId);

        if (refund == null)
            return NotFound();

        var dto = MapToDto(refund);
        dto.AddSelfLink($"/api/v1/refunds/{refund.Id}");
        dto.AddLink("payment_intent", $"/api/v1/payment_intents/{refund.PaymentIntentId}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<RefundDto>> Create([FromBody] CreateRefundRequest request)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var paymentIntent = await _context.PaymentIntents
            .FirstOrDefaultAsync(p => p.Id == request.PaymentIntentId && p.MerchantId == merchantId);

        if (paymentIntent == null)
        {
            return BadRequest(new ApiErrorDto
            {
                Error = new ApiErrorDetailDto
                {
                    Type = "invalid_request_error",
                    Code = "payment_intent_not_found",
                    Message = "No such payment_intent.",
                    Param = "payment_intent"
                }
            });
        }

        if (paymentIntent.Status != "succeeded")
        {
            return BadRequest(new ApiErrorDto
            {
                Error = new ApiErrorDetailDto
                {
                    Type = "invalid_request_error",
                    Code = "payment_intent_not_succeeded",
                    Message = "Only succeeded payment intents can be refunded."
                }
            });
        }

        // Calculate already refunded amount
        var totalRefunded = await _context.Refunds
            .Where(r => r.PaymentIntentId == request.PaymentIntentId && r.Status == "succeeded")
            .SumAsync(r => r.Amount);

        var availableToRefund = paymentIntent.AmountReceived - totalRefunded;
        var refundAmount = request.Amount ?? availableToRefund; // Full refund if amount not specified

        if (refundAmount <= 0)
        {
            return BadRequest(new ApiErrorDto
            {
                Error = new ApiErrorDetailDto
                {
                    Type = "invalid_request_error",
                    Code = "charge_already_refunded",
                    Message = "This payment has already been fully refunded."
                }
            });
        }

        if (refundAmount > availableToRefund)
        {
            return BadRequest(new ApiErrorDto
            {
                Error = new ApiErrorDetailDto
                {
                    Type = "invalid_request_error",
                    Code = "amount_too_large",
                    Message = $"Refund amount ({refundAmount}) exceeds the amount available for refund ({availableToRefund}).",
                    Param = "amount"
                }
            });
        }

        var refund = new Refund
        {
            MerchantId = merchantId.Value,
            PaymentIntentId = request.PaymentIntentId,
            Amount = refundAmount,
            Currency = paymentIntent.Currency,
            Status = "succeeded", // Simulated - instant success
            Reason = request.Reason,
            ReceiptNumber = _keyService.GenerateReceiptNumber(),
            SucceededAt = DateTime.UtcNow,
            Metadata = request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : null
        };

        _context.Refunds.Add(refund);
        await _context.SaveChangesAsync();

        // Send webhooks
        await _webhookService.SendWebhookAsync(merchantId.Value, "refund.created", "refund", refund.Id);
        await _webhookService.SendWebhookAsync(merchantId.Value, "refund.succeeded", "refund", refund.Id);

        var dto = MapToDto(refund);
        dto.AddSelfLink($"/api/v1/refunds/{refund.Id}");
        dto.AddLink("payment_intent", $"/api/v1/payment_intents/{refund.PaymentIntentId}");

        return CreatedAtAction(nameof(GetById), new { id = refund.Id }, dto);
    }

    private Guid? GetAuthenticatedMerchantId()
    {
        if (HttpContext.Items.TryGetValue("MerchantId", out var merchantIdObj) && merchantIdObj is Guid merchantId)
            return merchantId;
        return null;
    }

    private static RefundDto MapToDto(Refund r)
    {
        return new RefundDto
        {
            Id = r.Id,
            MerchantId = r.MerchantId,
            PaymentIntentId = r.PaymentIntentId,
            Amount = r.Amount,
            Currency = r.Currency,
            Status = r.Status,
            Reason = r.Reason,
            ReceiptNumber = r.ReceiptNumber,
            FailureReason = r.FailureReason,
            Metadata = !string.IsNullOrEmpty(r.Metadata)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(r.Metadata)
                : null,
            CreatedAt = r.CreatedAt,
            SucceededAt = r.SucceededAt
        };
    }
}
