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
[Route("api/v1/payment_intents")]
public class PaymentIntentsController : ControllerBase
{
    private readonly PaymentGatewayDbContext _context;
    private readonly KeyGenerationService _keyService;
    private readonly PaymentProcessingService _paymentService;
    private readonly WebhookService _webhookService;

    public PaymentIntentsController(
        PaymentGatewayDbContext context,
        KeyGenerationService keyService,
        PaymentProcessingService paymentService,
        WebhookService webhookService)
    {
        _context = context;
        _keyService = keyService;
        _paymentService = paymentService;
        _webhookService = webhookService;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<PaymentIntentDto>>> GetAll(
        [FromQuery] string? status = null,
        [FromQuery] string? channel = null,
        [FromQuery] DateTime? created_gte = null,
        [FromQuery] DateTime? created_lte = null,
        [FromQuery] int limit = 20,
        [FromQuery] string? starting_after = null)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var query = _context.PaymentIntents
            .Where(p => p.MerchantId == merchantId)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(p => p.Status == status);
        if (!string.IsNullOrEmpty(channel))
            query = query.Where(p => p.Channel == channel);
        if (created_gte.HasValue)
            query = query.Where(p => p.CreatedAt >= created_gte.Value);
        if (created_lte.HasValue)
            query = query.Where(p => p.CreatedAt <= created_lte.Value);
        if (!string.IsNullOrEmpty(starting_after) && Guid.TryParse(starting_after, out var afterId))
        {
            var afterPayment = await _context.PaymentIntents.FindAsync(afterId);
            if (afterPayment != null)
                query = query.Where(p => p.CreatedAt < afterPayment.CreatedAt);
        }

        var paymentIntents = await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(Math.Min(limit, 100))
            .ToListAsync();

        var dtos = paymentIntents.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/v1/payment_intents/{dto.Id}");
            AddPaymentIntentLinks(dto);
        }

        var collection = HalCollection<PaymentIntentDto>.Create(
            dtos,
            "/api/v1/payment_intents",
            await query.CountAsync()
        );

        if (dtos.Any())
        {
            collection.AddLink("next", $"/api/v1/payment_intents?starting_after={dtos.Last().Id}&limit={limit}");
        }

        return Ok(collection);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentIntentDto>> GetById(Guid id)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var paymentIntent = await _context.PaymentIntents
            .FirstOrDefaultAsync(p => p.Id == id && p.MerchantId == merchantId);

        if (paymentIntent == null)
            return NotFound();

        var dto = MapToDto(paymentIntent);
        dto.AddSelfLink($"/api/v1/payment_intents/{paymentIntent.Id}");
        AddPaymentIntentLinks(dto);

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<PaymentIntentDto>> Create([FromBody] CreatePaymentIntentRequest request)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var merchant = await _context.Merchants.FindAsync(merchantId);
        if (merchant == null || !merchant.ChargesEnabled)
        {
            return BadRequest(new ApiErrorDto
            {
                Error = new ApiErrorDetailDto
                {
                    Type = "invalid_request_error",
                    Code = "charges_not_enabled",
                    Message = "This merchant account cannot create charges."
                }
            });
        }

        // Validate terminal if POS channel
        Terminal? terminal = null;
        if (request.Channel == "pos" && request.TerminalId.HasValue)
        {
            terminal = await _context.Terminals
                .FirstOrDefaultAsync(t => t.Id == request.TerminalId && t.MerchantId == merchantId);
            if (terminal == null || !terminal.IsRegistered)
            {
                return BadRequest(new ApiErrorDto
                {
                    Error = new ApiErrorDetailDto
                    {
                        Type = "invalid_request_error",
                        Code = "invalid_terminal",
                        Message = "Terminal not found or not registered.",
                        Param = "terminal_id"
                    }
                });
            }
        }

        var clientSecret = _keyService.GenerateClientSecret(Guid.NewGuid());

        var paymentIntent = new PaymentIntent
        {
            MerchantId = merchantId.Value,
            Amount = request.Amount,
            Currency = request.Currency.ToLowerInvariant(),
            CaptureMethod = request.CaptureMethod,
            ConfirmationMethod = request.ConfirmationMethod,
            Channel = request.Channel,
            ClientSecret = clientSecret,
            TerminalId = request.TerminalId,
            Description = request.Description,
            StatementDescriptor = request.StatementDescriptor ?? merchant.StatementDescriptor,
            StatementDescriptorSuffix = request.StatementDescriptorSuffix,
            ReceiptEmail = request.ReceiptEmail,
            ExternalOrderId = request.ExternalOrderId,
            ExternalCustomerId = request.ExternalCustomerId,
            Metadata = request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : null,
            Status = "requires_payment_method"
        };

        _context.PaymentIntents.Add(paymentIntent);
        await _context.SaveChangesAsync();

        // Send webhook
        await _webhookService.SendWebhookAsync(merchantId.Value, "payment_intent.created", "payment_intent", paymentIntent.Id);

        var dto = MapToDto(paymentIntent);
        dto.AddSelfLink($"/api/v1/payment_intents/{paymentIntent.Id}");
        AddPaymentIntentLinks(dto);

        return CreatedAtAction(nameof(GetById), new { id = paymentIntent.Id }, dto);
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<PaymentIntentDto>> Confirm(
        Guid id,
        [FromBody] ConfirmPaymentIntentRequest request)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var paymentIntent = await _context.PaymentIntents
            .Include(p => p.Terminal)
            .FirstOrDefaultAsync(p => p.Id == id && p.MerchantId == merchantId);

        if (paymentIntent == null)
            return NotFound();

        if (paymentIntent.Status != "requires_payment_method" && paymentIntent.Status != "requires_confirmation")
        {
            return BadRequest(new ApiErrorDto
            {
                Error = new ApiErrorDetailDto
                {
                    Type = "invalid_request_error",
                    Code = "payment_intent_unexpected_state",
                    Message = $"This PaymentIntent's status is {paymentIntent.Status}, but must be requires_payment_method or requires_confirmation to confirm."
                }
            });
        }

        // Process payment based on channel
        if (paymentIntent.Channel == "pos" && paymentIntent.Terminal != null)
        {
            // Card-present (POS) payment
            var (success, transaction) = await _paymentService.ProcessCardPresentPayment(
                paymentIntent,
                paymentIntent.Terminal);

            if (!success)
            {
                return BadRequest(new ApiErrorDto
                {
                    Error = new ApiErrorDetailDto
                    {
                        Type = "card_error",
                        Code = transaction.FailureCode ?? "card_declined",
                        Message = transaction.FailureMessage ?? "The card was declined."
                    }
                });
            }
        }
        else if (request.Card != null)
        {
            // Card-not-present (eCommerce) payment
            var (success, transaction) = await _paymentService.ProcessCardPayment(paymentIntent, request.Card);

            if (!success)
            {
                // Reload to get updated error info
                await _context.Entry(paymentIntent).ReloadAsync();

                return BadRequest(new ApiErrorDto
                {
                    Error = new ApiErrorDetailDto
                    {
                        Type = "card_error",
                        Code = paymentIntent.LastErrorCode ?? "card_declined",
                        Message = paymentIntent.LastErrorMessage ?? "The card was declined.",
                        DeclineCode = transaction.DeclineCode
                    }
                });
            }
        }
        else
        {
            return BadRequest(new ApiErrorDto
            {
                Error = new ApiErrorDetailDto
                {
                    Type = "invalid_request_error",
                    Code = "payment_method_required",
                    Message = "A payment method is required to confirm this PaymentIntent."
                }
            });
        }

        await _context.Entry(paymentIntent).ReloadAsync();

        var dto = MapToDto(paymentIntent);
        dto.AddSelfLink($"/api/v1/payment_intents/{paymentIntent.Id}");
        AddPaymentIntentLinks(dto);

        return Ok(dto);
    }

    [HttpPost("{id:guid}/capture")]
    public async Task<ActionResult<PaymentIntentDto>> Capture(
        Guid id,
        [FromBody] CapturePaymentIntentRequest? request = null)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var paymentIntent = await _context.PaymentIntents
            .FirstOrDefaultAsync(p => p.Id == id && p.MerchantId == merchantId);

        if (paymentIntent == null)
            return NotFound();

        if (paymentIntent.Status != "requires_capture")
        {
            return BadRequest(new ApiErrorDto
            {
                Error = new ApiErrorDetailDto
                {
                    Type = "invalid_request_error",
                    Code = "payment_intent_unexpected_state",
                    Message = $"This PaymentIntent's status is {paymentIntent.Status}, but must be requires_capture to capture."
                }
            });
        }

        var (success, transaction) = await _paymentService.CapturePayment(
            paymentIntent,
            request?.AmountToCapture);

        if (!success)
        {
            return BadRequest(new ApiErrorDto
            {
                Error = new ApiErrorDetailDto
                {
                    Type = "invalid_request_error",
                    Code = "amount_too_large",
                    Message = "Capture amount exceeds the amount capturable."
                }
            });
        }

        await _context.Entry(paymentIntent).ReloadAsync();

        var dto = MapToDto(paymentIntent);
        dto.AddSelfLink($"/api/v1/payment_intents/{paymentIntent.Id}");
        AddPaymentIntentLinks(dto);

        return Ok(dto);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<PaymentIntentDto>> Cancel(
        Guid id,
        [FromBody] CancelPaymentIntentRequest? request = null)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var paymentIntent = await _context.PaymentIntents
            .FirstOrDefaultAsync(p => p.Id == id && p.MerchantId == merchantId);

        if (paymentIntent == null)
            return NotFound();

        var cancellableStatuses = new[] { "requires_payment_method", "requires_confirmation", "requires_action", "requires_capture" };
        if (!cancellableStatuses.Contains(paymentIntent.Status))
        {
            return BadRequest(new ApiErrorDto
            {
                Error = new ApiErrorDetailDto
                {
                    Type = "invalid_request_error",
                    Code = "payment_intent_unexpected_state",
                    Message = $"This PaymentIntent's status is {paymentIntent.Status} and cannot be canceled."
                }
            });
        }

        paymentIntent.Status = "canceled";
        paymentIntent.CancellationReason = request?.CancellationReason;
        paymentIntent.CanceledAt = DateTime.UtcNow;
        paymentIntent.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Send webhook
        await _webhookService.SendWebhookAsync(merchantId.Value, "payment_intent.canceled", "payment_intent", paymentIntent.Id);

        var dto = MapToDto(paymentIntent);
        dto.AddSelfLink($"/api/v1/payment_intents/{paymentIntent.Id}");

        return Ok(dto);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<PaymentIntentDto>> Update(
        Guid id,
        [FromBody] UpdatePaymentIntentRequest request)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var paymentIntent = await _context.PaymentIntents
            .FirstOrDefaultAsync(p => p.Id == id && p.MerchantId == merchantId);

        if (paymentIntent == null)
            return NotFound();

        // Only allow updates to certain statuses
        var updatableStatuses = new[] { "requires_payment_method", "requires_confirmation" };
        if (!updatableStatuses.Contains(paymentIntent.Status))
        {
            return BadRequest(new ApiErrorDto
            {
                Error = new ApiErrorDetailDto
                {
                    Type = "invalid_request_error",
                    Code = "payment_intent_unexpected_state",
                    Message = $"This PaymentIntent cannot be updated because its status is {paymentIntent.Status}."
                }
            });
        }

        if (request.Description != null)
            paymentIntent.Description = request.Description;
        if (request.ReceiptEmail != null)
            paymentIntent.ReceiptEmail = request.ReceiptEmail;
        if (request.Metadata != null)
            paymentIntent.Metadata = JsonSerializer.Serialize(request.Metadata);

        paymentIntent.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = MapToDto(paymentIntent);
        dto.AddSelfLink($"/api/v1/payment_intents/{paymentIntent.Id}");
        AddPaymentIntentLinks(dto);

        return Ok(dto);
    }

    private Guid? GetAuthenticatedMerchantId()
    {
        if (HttpContext.Items.TryGetValue("MerchantId", out var merchantIdObj) && merchantIdObj is Guid merchantId)
            return merchantId;
        return null;
    }

    private void AddPaymentIntentLinks(PaymentIntentDto dto)
    {
        switch (dto.Status)
        {
            case "requires_payment_method":
            case "requires_confirmation":
                dto.AddLink("confirm", $"/api/v1/payment_intents/{dto.Id}/confirm", "Confirm this payment intent");
                dto.AddLink("cancel", $"/api/v1/payment_intents/{dto.Id}/cancel", "Cancel this payment intent");
                break;
            case "requires_capture":
                dto.AddLink("capture", $"/api/v1/payment_intents/{dto.Id}/capture", "Capture this payment intent");
                dto.AddLink("cancel", $"/api/v1/payment_intents/{dto.Id}/cancel", "Cancel this payment intent");
                break;
            case "succeeded":
                dto.AddLink("refunds", $"/api/v1/refunds?payment_intent={dto.Id}", "Refunds for this payment intent");
                dto.AddLink("create_refund", "/api/v1/refunds", "Create a refund");
                break;
        }
        dto.AddLink("transactions", $"/api/v1/transactions?payment_intent={dto.Id}", "Transactions for this payment intent");
    }

    private static PaymentIntentDto MapToDto(PaymentIntent pi)
    {
        return new PaymentIntentDto
        {
            Id = pi.Id,
            MerchantId = pi.MerchantId,
            Amount = pi.Amount,
            AmountCapturable = pi.AmountCapturable,
            AmountReceived = pi.AmountReceived,
            Currency = pi.Currency,
            Status = pi.Status,
            CaptureMethod = pi.CaptureMethod,
            ConfirmationMethod = pi.ConfirmationMethod,
            Channel = pi.Channel,
            ClientSecret = pi.ClientSecret,
            PaymentMethodId = pi.PaymentMethodId,
            PaymentMethodType = pi.PaymentMethodType,
            Card = pi.CardLast4 != null ? new CardDetailsDto
            {
                Brand = pi.CardBrand,
                Last4 = pi.CardLast4,
                ExpMonth = pi.CardExpMonth,
                ExpYear = pi.CardExpYear,
                Funding = pi.CardFunding
            } : null,
            TerminalId = pi.TerminalId,
            Description = pi.Description,
            StatementDescriptor = pi.StatementDescriptor,
            ReceiptEmail = pi.ReceiptEmail,
            ExternalOrderId = pi.ExternalOrderId,
            ExternalCustomerId = pi.ExternalCustomerId,
            CancellationReason = pi.CancellationReason,
            CanceledAt = pi.CanceledAt,
            SucceededAt = pi.SucceededAt,
            LastError = pi.LastErrorCode != null ? new PaymentIntentErrorDto
            {
                Code = pi.LastErrorCode,
                Message = pi.LastErrorMessage
            } : null,
            Metadata = !string.IsNullOrEmpty(pi.Metadata)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(pi.Metadata)
                : null,
            CreatedAt = pi.CreatedAt,
            UpdatedAt = pi.UpdatedAt
        };
    }
}
