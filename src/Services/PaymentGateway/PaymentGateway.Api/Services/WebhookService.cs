using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DarkVelocity.PaymentGateway.Api.Data;
using DarkVelocity.PaymentGateway.Api.Dtos;
using DarkVelocity.PaymentGateway.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.PaymentGateway.Api.Services;

public class WebhookService
{
    private readonly PaymentGatewayDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public WebhookService(
        PaymentGatewayDbContext context,
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookService> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task SendWebhookAsync(Guid merchantId, string eventType, string objectType, Guid objectId)
    {
        var endpoints = await _context.WebhookEndpoints
            .Where(e => e.MerchantId == merchantId && e.IsActive)
            .ToListAsync();

        foreach (var endpoint in endpoints)
        {
            // Check if this event type is enabled
            if (!IsEventEnabled(endpoint.EnabledEvents, eventType))
                continue;

            // Get the object data
            var objectData = await GetObjectDataAsync(objectType, objectId);
            if (objectData == null)
                continue;

            // Create payload
            var payload = new WebhookPayload
            {
                Id = Guid.NewGuid(),
                EventType = eventType,
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ApiVersion = endpoint.ApiVersion,
                Data = new WebhookPayloadData { Object = objectData }
            };

            var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);

            // Create webhook event record
            var webhookEvent = new WebhookEvent
            {
                MerchantId = merchantId,
                WebhookEndpointId = endpoint.Id,
                EventType = eventType,
                ObjectType = objectType,
                ObjectId = objectId,
                Payload = payloadJson,
                Status = "pending"
            };

            _context.WebhookEvents.Add(webhookEvent);
            await _context.SaveChangesAsync();

            // Attempt delivery (fire and forget for now)
            _ = DeliverWebhookAsync(webhookEvent, endpoint, payloadJson);
        }
    }

    private async Task DeliverWebhookAsync(WebhookEvent webhookEvent, WebhookEndpoint endpoint, string payload)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            // Generate signature
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var signedPayload = $"{timestamp}.{payload}";
            var signature = ComputeSignature(signedPayload, endpoint.Secret);

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            request.Headers.Add("Stripe-Signature", $"t={timestamp},v1={signature}");
            request.Headers.Add("User-Agent", "PaymentGateway/1.0");

            webhookEvent.DeliveryAttempts++;
            webhookEvent.LastAttemptAt = DateTime.UtcNow;

            var response = await client.SendAsync(request);
            webhookEvent.ResponseStatusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                webhookEvent.Status = "delivered";
                webhookEvent.DeliveredAt = DateTime.UtcNow;
                endpoint.ConsecutiveFailures = 0;
            }
            else
            {
                webhookEvent.Status = "failed";
                webhookEvent.ResponseBody = await response.Content.ReadAsStringAsync();
                webhookEvent.ErrorMessage = $"HTTP {webhookEvent.ResponseStatusCode}";
                endpoint.ConsecutiveFailures++;

                // Schedule retry with exponential backoff
                if (webhookEvent.DeliveryAttempts < 5)
                {
                    var delay = TimeSpan.FromMinutes(Math.Pow(2, webhookEvent.DeliveryAttempts));
                    webhookEvent.NextRetryAt = DateTime.UtcNow.Add(delay);
                }

                // Disable endpoint after too many failures
                if (endpoint.ConsecutiveFailures >= 10)
                {
                    endpoint.IsActive = false;
                    endpoint.DisabledAt = DateTime.UtcNow;
                    endpoint.DisabledReason = "Too many consecutive failures";
                }
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deliver webhook {WebhookEventId} to {Url}", webhookEvent.Id, endpoint.Url);

            webhookEvent.Status = "failed";
            webhookEvent.ErrorMessage = ex.Message;
            webhookEvent.DeliveryAttempts++;
            webhookEvent.LastAttemptAt = DateTime.UtcNow;
            endpoint.ConsecutiveFailures++;

            if (webhookEvent.DeliveryAttempts < 5)
            {
                var delay = TimeSpan.FromMinutes(Math.Pow(2, webhookEvent.DeliveryAttempts));
                webhookEvent.NextRetryAt = DateTime.UtcNow.Add(delay);
            }

            await _context.SaveChangesAsync();
        }
    }

    private bool IsEventEnabled(string enabledEvents, string eventType)
    {
        if (enabledEvents == "*")
            return true;

        var events = enabledEvents.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return events.Contains(eventType) || events.Contains(eventType.Split('.')[0] + ".*");
    }

    private async Task<object?> GetObjectDataAsync(string objectType, Guid objectId)
    {
        return objectType switch
        {
            "payment_intent" => await _context.PaymentIntents.FindAsync(objectId),
            "refund" => await _context.Refunds.FindAsync(objectId),
            "terminal" => await _context.Terminals.FindAsync(objectId),
            _ => null
        };
    }

    private static string ComputeSignature(string payload, string secret)
    {
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(secretBytes, payloadBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public string ComputeWebhookSignature(string payload, string secret, long timestamp)
    {
        var signedPayload = $"{timestamp}.{payload}";
        return ComputeSignature(signedPayload, secret);
    }
}
