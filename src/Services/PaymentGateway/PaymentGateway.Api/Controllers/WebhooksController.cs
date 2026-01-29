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
[Route("api/v1/webhook_endpoints")]
public class WebhooksController : ControllerBase
{
    private readonly PaymentGatewayDbContext _context;
    private readonly KeyGenerationService _keyService;
    private readonly WebhookService _webhookService;

    public WebhooksController(
        PaymentGatewayDbContext context,
        KeyGenerationService keyService,
        WebhookService webhookService)
    {
        _context = context;
        _keyService = keyService;
        _webhookService = webhookService;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<WebhookEndpointDto>>> GetAll([FromQuery] int limit = 20)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var endpoints = await _context.WebhookEndpoints
            .Where(w => w.MerchantId == merchantId)
            .OrderByDescending(w => w.CreatedAt)
            .Take(Math.Min(limit, 100))
            .ToListAsync();

        var dtos = endpoints.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/v1/webhook_endpoints/{dto.Id}");
            dto.AddLink("events", $"/api/v1/webhook_endpoints/{dto.Id}/events", "Webhook events");
        }

        return Ok(HalCollection<WebhookEndpointDto>.Create(
            dtos,
            "/api/v1/webhook_endpoints",
            endpoints.Count
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WebhookEndpointDto>> GetById(Guid id)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var endpoint = await _context.WebhookEndpoints
            .FirstOrDefaultAsync(w => w.Id == id && w.MerchantId == merchantId);

        if (endpoint == null)
            return NotFound();

        var dto = MapToDto(endpoint);
        dto.AddSelfLink($"/api/v1/webhook_endpoints/{endpoint.Id}");
        dto.AddLink("events", $"/api/v1/webhook_endpoints/{endpoint.Id}/events", "Webhook events");
        dto.AddLink("test", $"/api/v1/webhook_endpoints/{endpoint.Id}/test", "Send a test webhook");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<WebhookEndpointCreatedDto>> Create([FromBody] CreateWebhookEndpointRequest request)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        // Check for duplicate URL
        if (await _context.WebhookEndpoints.AnyAsync(w => w.MerchantId == merchantId && w.Url == request.Url))
        {
            return BadRequest(new ApiErrorDto
            {
                Error = new ApiErrorDetailDto
                {
                    Type = "invalid_request_error",
                    Code = "webhook_url_exists",
                    Message = "A webhook endpoint with this URL already exists."
                }
            });
        }

        var secret = _keyService.GenerateWebhookSecret();
        var enabledEvents = request.EnabledEvents != null && request.EnabledEvents.Count > 0
            ? string.Join(",", request.EnabledEvents)
            : "*";

        var endpoint = new WebhookEndpoint
        {
            MerchantId = merchantId.Value,
            Url = request.Url,
            Secret = secret,
            Description = request.Description,
            EnabledEvents = enabledEvents,
            ApiVersion = request.ApiVersion ?? "2024-01-01",
            Metadata = request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : null
        };

        _context.WebhookEndpoints.Add(endpoint);
        await _context.SaveChangesAsync();

        var dto = new WebhookEndpointCreatedDto
        {
            Id = endpoint.Id,
            MerchantId = endpoint.MerchantId,
            Url = endpoint.Url,
            Description = endpoint.Description,
            EnabledEvents = endpoint.EnabledEvents,
            IsActive = endpoint.IsActive,
            ApiVersion = endpoint.ApiVersion,
            ConsecutiveFailures = endpoint.ConsecutiveFailures,
            CreatedAt = endpoint.CreatedAt,
            Secret = secret // Only returned on creation!
        };

        dto.AddSelfLink($"/api/v1/webhook_endpoints/{endpoint.Id}");
        dto.AddLink("events", $"/api/v1/webhook_endpoints/{endpoint.Id}/events", "Webhook events");

        return CreatedAtAction(nameof(GetById), new { id = endpoint.Id }, dto);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<WebhookEndpointDto>> Update(Guid id, [FromBody] UpdateWebhookEndpointRequest request)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var endpoint = await _context.WebhookEndpoints
            .FirstOrDefaultAsync(w => w.Id == id && w.MerchantId == merchantId);

        if (endpoint == null)
            return NotFound();

        if (request.Url != null)
            endpoint.Url = request.Url;
        if (request.Description != null)
            endpoint.Description = request.Description;
        if (request.EnabledEvents != null)
            endpoint.EnabledEvents = request.EnabledEvents.Count > 0 ? string.Join(",", request.EnabledEvents) : "*";
        if (request.IsActive.HasValue)
        {
            endpoint.IsActive = request.IsActive.Value;
            if (request.IsActive.Value)
            {
                endpoint.DisabledAt = null;
                endpoint.DisabledReason = null;
                endpoint.ConsecutiveFailures = 0;
            }
        }
        if (request.Metadata != null)
            endpoint.Metadata = JsonSerializer.Serialize(request.Metadata);

        endpoint.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = MapToDto(endpoint);
        dto.AddSelfLink($"/api/v1/webhook_endpoints/{endpoint.Id}");
        dto.AddLink("events", $"/api/v1/webhook_endpoints/{endpoint.Id}/events", "Webhook events");

        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var endpoint = await _context.WebhookEndpoints
            .FirstOrDefaultAsync(w => w.Id == id && w.MerchantId == merchantId);

        if (endpoint == null)
            return NotFound();

        _context.WebhookEndpoints.Remove(endpoint);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:guid}/test")]
    public async Task<ActionResult<WebhookEventDto>> Test(Guid id)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var endpoint = await _context.WebhookEndpoints
            .FirstOrDefaultAsync(w => w.Id == id && w.MerchantId == merchantId);

        if (endpoint == null)
            return NotFound();

        // Create a test webhook event
        var testPayload = JsonSerializer.Serialize(new
        {
            id = Guid.NewGuid(),
            type = "test.webhook",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            api_version = endpoint.ApiVersion,
            data = new
            {
                @object = new
                {
                    message = "This is a test webhook from the Payment Gateway API."
                }
            }
        });

        var webhookEvent = new WebhookEvent
        {
            MerchantId = merchantId.Value,
            WebhookEndpointId = endpoint.Id,
            EventType = "test.webhook",
            ObjectType = "test",
            ObjectId = Guid.Empty,
            Payload = testPayload,
            Status = "pending"
        };

        _context.WebhookEvents.Add(webhookEvent);
        await _context.SaveChangesAsync();

        // Note: In production, this would trigger immediate delivery
        // For testing, we just create the event record

        var dto = new WebhookEventDto
        {
            Id = webhookEvent.Id,
            MerchantId = webhookEvent.MerchantId,
            WebhookEndpointId = webhookEvent.WebhookEndpointId,
            EventType = webhookEvent.EventType,
            ObjectType = webhookEvent.ObjectType,
            ObjectId = webhookEvent.ObjectId,
            Status = webhookEvent.Status,
            DeliveryAttempts = webhookEvent.DeliveryAttempts,
            CreatedAt = webhookEvent.CreatedAt
        };

        dto.AddSelfLink($"/api/v1/webhook_endpoints/{endpoint.Id}/events/{webhookEvent.Id}");
        dto.AddLink("endpoint", $"/api/v1/webhook_endpoints/{endpoint.Id}");

        return Ok(dto);
    }

    // Webhook Events
    [HttpGet("{endpointId:guid}/events")]
    public async Task<ActionResult<HalCollection<WebhookEventDto>>> GetEvents(
        Guid endpointId,
        [FromQuery] string? status = null,
        [FromQuery] string? event_type = null,
        [FromQuery] int limit = 20)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var endpoint = await _context.WebhookEndpoints
            .FirstOrDefaultAsync(w => w.Id == endpointId && w.MerchantId == merchantId);

        if (endpoint == null)
            return NotFound();

        var query = _context.WebhookEvents
            .Where(e => e.WebhookEndpointId == endpointId)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(e => e.Status == status);
        if (!string.IsNullOrEmpty(event_type))
            query = query.Where(e => e.EventType == event_type);

        var events = await query
            .OrderByDescending(e => e.CreatedAt)
            .Take(Math.Min(limit, 100))
            .ToListAsync();

        var dtos = events.Select(e => new WebhookEventDto
        {
            Id = e.Id,
            MerchantId = e.MerchantId,
            WebhookEndpointId = e.WebhookEndpointId,
            EventType = e.EventType,
            ObjectType = e.ObjectType,
            ObjectId = e.ObjectId,
            Status = e.Status,
            DeliveryAttempts = e.DeliveryAttempts,
            LastAttemptAt = e.LastAttemptAt,
            DeliveredAt = e.DeliveredAt,
            ResponseStatusCode = e.ResponseStatusCode,
            ErrorMessage = e.ErrorMessage,
            NextRetryAt = e.NextRetryAt,
            CreatedAt = e.CreatedAt
        }).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/v1/webhook_endpoints/{endpointId}/events/{dto.Id}");
            dto.AddLink("endpoint", $"/api/v1/webhook_endpoints/{endpointId}");
        }

        return Ok(HalCollection<WebhookEventDto>.Create(
            dtos,
            $"/api/v1/webhook_endpoints/{endpointId}/events",
            events.Count
        ));
    }

    [HttpGet("{endpointId:guid}/events/{eventId:guid}")]
    public async Task<ActionResult<WebhookEventDetailDto>> GetEvent(Guid endpointId, Guid eventId)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var webhookEvent = await _context.WebhookEvents
            .FirstOrDefaultAsync(e => e.Id == eventId && e.WebhookEndpointId == endpointId && e.MerchantId == merchantId);

        if (webhookEvent == null)
            return NotFound();

        var dto = new WebhookEventDetailDto
        {
            Id = webhookEvent.Id,
            MerchantId = webhookEvent.MerchantId,
            WebhookEndpointId = webhookEvent.WebhookEndpointId,
            EventType = webhookEvent.EventType,
            ObjectType = webhookEvent.ObjectType,
            ObjectId = webhookEvent.ObjectId,
            Status = webhookEvent.Status,
            DeliveryAttempts = webhookEvent.DeliveryAttempts,
            LastAttemptAt = webhookEvent.LastAttemptAt,
            DeliveredAt = webhookEvent.DeliveredAt,
            ResponseStatusCode = webhookEvent.ResponseStatusCode,
            ErrorMessage = webhookEvent.ErrorMessage,
            NextRetryAt = webhookEvent.NextRetryAt,
            CreatedAt = webhookEvent.CreatedAt,
            Payload = webhookEvent.Payload,
            ResponseBody = webhookEvent.ResponseBody
        };

        dto.AddSelfLink($"/api/v1/webhook_endpoints/{endpointId}/events/{webhookEvent.Id}");
        dto.AddLink("endpoint", $"/api/v1/webhook_endpoints/{endpointId}");
        dto.AddLink("retry", $"/api/v1/webhook_endpoints/{endpointId}/events/{webhookEvent.Id}/retry", "Retry delivery");

        return Ok(dto);
    }

    [HttpPost("{endpointId:guid}/events/{eventId:guid}/retry")]
    public async Task<ActionResult<WebhookEventDto>> RetryEvent(Guid endpointId, Guid eventId)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var webhookEvent = await _context.WebhookEvents
            .FirstOrDefaultAsync(e => e.Id == eventId && e.WebhookEndpointId == endpointId && e.MerchantId == merchantId);

        if (webhookEvent == null)
            return NotFound();

        // Reset for retry
        webhookEvent.Status = "pending";
        webhookEvent.NextRetryAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = new WebhookEventDto
        {
            Id = webhookEvent.Id,
            MerchantId = webhookEvent.MerchantId,
            WebhookEndpointId = webhookEvent.WebhookEndpointId,
            EventType = webhookEvent.EventType,
            ObjectType = webhookEvent.ObjectType,
            ObjectId = webhookEvent.ObjectId,
            Status = webhookEvent.Status,
            DeliveryAttempts = webhookEvent.DeliveryAttempts,
            LastAttemptAt = webhookEvent.LastAttemptAt,
            DeliveredAt = webhookEvent.DeliveredAt,
            ResponseStatusCode = webhookEvent.ResponseStatusCode,
            ErrorMessage = webhookEvent.ErrorMessage,
            NextRetryAt = webhookEvent.NextRetryAt,
            CreatedAt = webhookEvent.CreatedAt
        };

        dto.AddSelfLink($"/api/v1/webhook_endpoints/{endpointId}/events/{webhookEvent.Id}");
        dto.AddLink("endpoint", $"/api/v1/webhook_endpoints/{endpointId}");

        return Ok(dto);
    }

    private Guid? GetAuthenticatedMerchantId()
    {
        if (HttpContext.Items.TryGetValue("MerchantId", out var merchantIdObj) && merchantIdObj is Guid merchantId)
            return merchantId;
        return null;
    }

    private static WebhookEndpointDto MapToDto(WebhookEndpoint w)
    {
        return new WebhookEndpointDto
        {
            Id = w.Id,
            MerchantId = w.MerchantId,
            Url = w.Url,
            Description = w.Description,
            EnabledEvents = w.EnabledEvents,
            IsActive = w.IsActive,
            ApiVersion = w.ApiVersion,
            ConsecutiveFailures = w.ConsecutiveFailures,
            DisabledAt = w.DisabledAt,
            DisabledReason = w.DisabledReason,
            CreatedAt = w.CreatedAt
        };
    }
}
