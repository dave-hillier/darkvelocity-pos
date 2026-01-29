using System.Security.Claims;
using DarkVelocity.PaymentGateway.Api.Services;
using DarkVelocity.PaymentGateway.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.PaymentGateway.Api.Middleware;

public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private const string ApiKeyHeaderName = "Authorization";
    private const string ApiKeyQueryName = "api_key";

    public ApiKeyAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, PaymentGatewayDbContext dbContext, KeyGenerationService keyService)
    {
        // Skip authentication for health check and API root
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (path == "/health" || path == "/api/v1" || path == "/api/v1/" ||
            path.StartsWith("/swagger") || path.StartsWith("/api/v1/merchants") && context.Request.Method == "POST")
        {
            await _next(context);
            return;
        }

        // Try to extract API key from Authorization header (Bearer token) or query parameter
        string? apiKey = null;

        if (context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var headerValue))
        {
            var header = headerValue.ToString();
            if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                apiKey = header.Substring(7);
            }
            else if (header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                // Basic auth with API key as username, empty password
                var encoded = header.Substring(6);
                try
                {
                    var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                    apiKey = decoded.Split(':')[0];
                }
                catch
                {
                    // Invalid base64
                }
            }
        }

        apiKey ??= context.Request.Query[ApiKeyQueryName].FirstOrDefault();

        if (string.IsNullOrEmpty(apiKey))
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":{\"type\":\"authentication_error\",\"code\":\"missing_api_key\",\"message\":\"No API key provided. Set the Authorization header with Bearer <api_key>.\"}}");
            return;
        }

        // Validate API key
        var keyHash = keyService.ComputeHash(apiKey);
        var apiKeyEntity = await dbContext.ApiKeys
            .Include(k => k.Merchant)
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.IsActive);

        if (apiKeyEntity == null)
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":{\"type\":\"authentication_error\",\"code\":\"invalid_api_key\",\"message\":\"Invalid API key provided.\"}}");
            return;
        }

        // Check expiration
        if (apiKeyEntity.ExpiresAt.HasValue && apiKeyEntity.ExpiresAt.Value < DateTime.UtcNow)
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":{\"type\":\"authentication_error\",\"code\":\"expired_api_key\",\"message\":\"API key has expired.\"}}");
            return;
        }

        // Check if merchant is active
        if (apiKeyEntity.Merchant?.Status != "active")
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":{\"type\":\"authentication_error\",\"code\":\"merchant_inactive\",\"message\":\"Merchant account is not active.\"}}");
            return;
        }

        // Update last used timestamp (fire and forget)
        apiKeyEntity.LastUsedAt = DateTime.UtcNow;
        _ = dbContext.SaveChangesAsync();

        // Add claims to the context
        var claims = new List<Claim>
        {
            new("merchant_id", apiKeyEntity.MerchantId.ToString()),
            new("api_key_id", apiKeyEntity.Id.ToString()),
            new("key_type", apiKeyEntity.KeyType),
            new("is_live", apiKeyEntity.IsLive.ToString().ToLowerInvariant())
        };

        var identity = new ClaimsIdentity(claims, "ApiKey");
        context.User = new ClaimsPrincipal(identity);

        // Store merchant ID in HttpContext.Items for easy access
        context.Items["MerchantId"] = apiKeyEntity.MerchantId;
        context.Items["ApiKeyId"] = apiKeyEntity.Id;
        context.Items["IsLiveMode"] = apiKeyEntity.IsLive;
        context.Items["KeyType"] = apiKeyEntity.KeyType;

        await _next(context);
    }
}

public static class ApiKeyAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyAuthenticationMiddleware>();
    }
}
