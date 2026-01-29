using System.Text.Json;
using DarkVelocity.PaymentGateway.Api.Data;
using DarkVelocity.PaymentGateway.Api.Dtos;
using DarkVelocity.PaymentGateway.Api.Entities;
using DarkVelocity.PaymentGateway.Api.Services;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.PaymentGateway.Api.Controllers;

/// <summary>
/// Checkout Sessions controller for eCommerce hosted checkout experience.
/// Similar to Stripe Checkout - creates a hosted payment page.
/// </summary>
[ApiController]
[Route("api/v1/checkout/sessions")]
public class CheckoutController : ControllerBase
{
    private readonly PaymentGatewayDbContext _context;
    private readonly KeyGenerationService _keyService;
    private readonly WebhookService _webhookService;

    public CheckoutController(
        PaymentGatewayDbContext context,
        KeyGenerationService keyService,
        WebhookService webhookService)
    {
        _context = context;
        _keyService = keyService;
        _webhookService = webhookService;
    }

    [HttpPost]
    public async Task<ActionResult<CheckoutSessionDto>> Create([FromBody] CreateCheckoutSessionRequest request)
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

        // Create underlying PaymentIntent
        var clientSecret = _keyService.GenerateClientSecret(Guid.NewGuid());

        var paymentIntent = new PaymentIntent
        {
            MerchantId = merchantId.Value,
            Amount = request.Amount,
            Currency = request.Currency.ToLowerInvariant(),
            CaptureMethod = "automatic",
            ConfirmationMethod = "automatic",
            Channel = "ecommerce",
            ClientSecret = clientSecret,
            ReceiptEmail = request.CustomerEmail,
            ExternalOrderId = request.ExternalOrderId,
            ExternalCustomerId = request.ExternalCustomerId,
            StatementDescriptor = merchant.StatementDescriptor,
            Metadata = request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : null,
            Status = "requires_payment_method"
        };

        _context.PaymentIntents.Add(paymentIntent);
        await _context.SaveChangesAsync();

        // Generate checkout session
        var sessionId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddHours(24);

        // In a real implementation, this would be a hosted checkout URL
        var checkoutUrl = $"https://checkout.paymentgateway.local/pay/{sessionId}";

        var dto = new CheckoutSessionDto
        {
            Id = sessionId,
            MerchantId = merchantId.Value,
            PaymentIntentId = paymentIntent.Id,
            Url = checkoutUrl,
            Status = "open",
            AmountTotal = request.Amount,
            Currency = request.Currency.ToLowerInvariant(),
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            CustomerEmail = request.CustomerEmail,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };

        dto.AddSelfLink($"/api/v1/checkout/sessions/{sessionId}");
        dto.AddLink("payment_intent", $"/api/v1/payment_intents/{paymentIntent.Id}", "Underlying payment intent");
        dto.AddLink("checkout", checkoutUrl, "Hosted checkout page");

        // Store the session info in payment intent metadata
        var sessionMetadata = new Dictionary<string, string>
        {
            ["checkout_session_id"] = sessionId.ToString(),
            ["success_url"] = request.SuccessUrl ?? "",
            ["cancel_url"] = request.CancelUrl ?? ""
        };

        if (request.Metadata != null)
        {
            foreach (var kvp in request.Metadata)
                sessionMetadata[kvp.Key] = kvp.Value;
        }

        paymentIntent.Metadata = JsonSerializer.Serialize(sessionMetadata);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = sessionId }, dto);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CheckoutSessionDto>> GetById(Guid id)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        // Find payment intent with this checkout session ID in metadata
        var paymentIntents = await _context.PaymentIntents
            .Where(p => p.MerchantId == merchantId && p.Channel == "ecommerce")
            .ToListAsync();

        PaymentIntent? paymentIntent = null;
        Dictionary<string, string>? metadata = null;

        foreach (var pi in paymentIntents)
        {
            if (string.IsNullOrEmpty(pi.Metadata))
                continue;

            var piMetadata = JsonSerializer.Deserialize<Dictionary<string, string>>(pi.Metadata);
            if (piMetadata != null && piMetadata.TryGetValue("checkout_session_id", out var sessionId) && sessionId == id.ToString())
            {
                paymentIntent = pi;
                metadata = piMetadata;
                break;
            }
        }

        if (paymentIntent == null || metadata == null)
            return NotFound();

        var status = paymentIntent.Status switch
        {
            "succeeded" => "complete",
            "canceled" => "expired",
            _ => "open"
        };

        var checkoutUrl = $"https://checkout.paymentgateway.local/pay/{id}";

        var dto = new CheckoutSessionDto
        {
            Id = id,
            MerchantId = merchantId.Value,
            PaymentIntentId = paymentIntent.Id,
            Url = checkoutUrl,
            Status = status,
            AmountTotal = paymentIntent.Amount,
            Currency = paymentIntent.Currency,
            SuccessUrl = metadata.GetValueOrDefault("success_url"),
            CancelUrl = metadata.GetValueOrDefault("cancel_url"),
            CustomerEmail = paymentIntent.ReceiptEmail,
            ExpiresAt = paymentIntent.CreatedAt.AddHours(24),
            CreatedAt = paymentIntent.CreatedAt
        };

        dto.AddSelfLink($"/api/v1/checkout/sessions/{id}");
        dto.AddLink("payment_intent", $"/api/v1/payment_intents/{paymentIntent.Id}", "Underlying payment intent");

        if (status == "open")
            dto.AddLink("checkout", checkoutUrl, "Hosted checkout page");

        return Ok(dto);
    }

    [HttpPost("{id:guid}/expire")]
    public async Task<ActionResult<CheckoutSessionDto>> Expire(Guid id)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        // Find payment intent with this checkout session ID
        var paymentIntents = await _context.PaymentIntents
            .Where(p => p.MerchantId == merchantId && p.Channel == "ecommerce")
            .ToListAsync();

        PaymentIntent? paymentIntent = null;

        foreach (var pi in paymentIntents)
        {
            if (string.IsNullOrEmpty(pi.Metadata))
                continue;

            var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(pi.Metadata);
            if (metadata != null && metadata.TryGetValue("checkout_session_id", out var sessionId) && sessionId == id.ToString())
            {
                paymentIntent = pi;
                break;
            }
        }

        if (paymentIntent == null)
            return NotFound();

        if (paymentIntent.Status == "succeeded")
        {
            return BadRequest(new ApiErrorDto
            {
                Error = new ApiErrorDetailDto
                {
                    Type = "invalid_request_error",
                    Code = "checkout_session_already_complete",
                    Message = "This checkout session has already been completed."
                }
            });
        }

        // Cancel the payment intent
        paymentIntent.Status = "canceled";
        paymentIntent.CancellationReason = "expired";
        paymentIntent.CanceledAt = DateTime.UtcNow;
        paymentIntent.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var metadata2 = JsonSerializer.Deserialize<Dictionary<string, string>>(paymentIntent.Metadata!);

        var dto = new CheckoutSessionDto
        {
            Id = id,
            MerchantId = merchantId.Value,
            PaymentIntentId = paymentIntent.Id,
            Url = $"https://checkout.paymentgateway.local/pay/{id}",
            Status = "expired",
            AmountTotal = paymentIntent.Amount,
            Currency = paymentIntent.Currency,
            SuccessUrl = metadata2?.GetValueOrDefault("success_url"),
            CancelUrl = metadata2?.GetValueOrDefault("cancel_url"),
            CustomerEmail = paymentIntent.ReceiptEmail,
            ExpiresAt = paymentIntent.CreatedAt.AddHours(24),
            CreatedAt = paymentIntent.CreatedAt
        };

        dto.AddSelfLink($"/api/v1/checkout/sessions/{id}");
        dto.AddLink("payment_intent", $"/api/v1/payment_intents/{paymentIntent.Id}", "Underlying payment intent");

        return Ok(dto);
    }

    private Guid? GetAuthenticatedMerchantId()
    {
        if (HttpContext.Items.TryGetValue("MerchantId", out var merchantIdObj) && merchantIdObj is Guid merchantId)
            return merchantId;
        return null;
    }
}
