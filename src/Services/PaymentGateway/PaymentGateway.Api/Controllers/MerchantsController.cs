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
[Route("api/v1/merchants")]
public class MerchantsController : ControllerBase
{
    private readonly PaymentGatewayDbContext _context;
    private readonly KeyGenerationService _keyService;

    public MerchantsController(PaymentGatewayDbContext context, KeyGenerationService keyService)
    {
        _context = context;
        _keyService = keyService;
    }

    [HttpPost]
    public async Task<ActionResult<MerchantDto>> Create([FromBody] CreateMerchantRequest request)
    {
        // Check if email already exists
        if (await _context.Merchants.AnyAsync(m => m.Email == request.Email))
        {
            return BadRequest(new ApiErrorDto
            {
                Error = new ApiErrorDetailDto
                {
                    Type = "invalid_request_error",
                    Code = "email_exists",
                    Message = "A merchant with this email already exists."
                }
            });
        }

        var merchant = new Merchant
        {
            Name = request.Name,
            Email = request.Email,
            BusinessName = request.BusinessName,
            BusinessType = request.BusinessType,
            Country = request.Country,
            DefaultCurrency = request.DefaultCurrency,
            StatementDescriptor = request.StatementDescriptor,
            AddressLine1 = request.Address?.Line1,
            AddressLine2 = request.Address?.Line2,
            City = request.Address?.City,
            State = request.Address?.State,
            PostalCode = request.Address?.PostalCode,
            Metadata = request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : null
        };

        _context.Merchants.Add(merchant);

        // Create default API keys (test mode secret and publishable)
        var (testSecretKey, testSecretHash, testSecretHint) = _keyService.GenerateApiKey("sk_test_");
        var (testPublishableKey, testPublishableHash, testPublishableHint) = _keyService.GenerateApiKey("pk_test_");

        var testSecretApiKey = new ApiKey
        {
            MerchantId = merchant.Id,
            Name = "Default Test Secret Key",
            KeyType = "secret",
            KeyPrefix = "sk_test_",
            KeyHash = testSecretHash,
            KeyHint = testSecretHint,
            IsLive = false
        };

        var testPublishableApiKey = new ApiKey
        {
            MerchantId = merchant.Id,
            Name = "Default Test Publishable Key",
            KeyType = "publishable",
            KeyPrefix = "pk_test_",
            KeyHash = testPublishableHash,
            KeyHint = testPublishableHint,
            IsLive = false
        };

        _context.ApiKeys.AddRange(testSecretApiKey, testPublishableApiKey);
        await _context.SaveChangesAsync();

        var dto = MapToDto(merchant);
        dto.AddSelfLink($"/api/v1/merchants/{merchant.Id}");

        // Include the generated API keys in the response (only on creation)
        var response = new
        {
            merchant = dto,
            api_keys = new
            {
                test_secret_key = testSecretKey,
                test_publishable_key = testPublishableKey
            },
            _links = dto.Links
        };

        return CreatedAtAction(nameof(GetById), new { id = merchant.Id }, response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MerchantDto>> GetById(Guid id)
    {
        // Get merchant ID from authentication context
        var authenticatedMerchantId = GetAuthenticatedMerchantId();
        if (authenticatedMerchantId == null)
            return Unauthorized();

        // Only allow access to own merchant data
        if (id != authenticatedMerchantId)
            return NotFound();

        var merchant = await _context.Merchants.FindAsync(id);
        if (merchant == null)
            return NotFound();

        var dto = MapToDto(merchant);
        dto.AddSelfLink($"/api/v1/merchants/{merchant.Id}");
        dto.AddLink("api_keys", $"/api/v1/merchants/{merchant.Id}/api_keys", "API Keys");
        dto.AddLink("payment_intents", "/api/v1/payment_intents", "Payment Intents");
        dto.AddLink("terminals", "/api/v1/terminals", "Terminals");
        dto.AddLink("webhook_endpoints", "/api/v1/webhook_endpoints", "Webhook Endpoints");

        return Ok(dto);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<MerchantDto>> Update(Guid id, [FromBody] UpdateMerchantRequest request)
    {
        var authenticatedMerchantId = GetAuthenticatedMerchantId();
        if (authenticatedMerchantId == null || id != authenticatedMerchantId)
            return NotFound();

        var merchant = await _context.Merchants.FindAsync(id);
        if (merchant == null)
            return NotFound();

        if (request.Name != null)
            merchant.Name = request.Name;
        if (request.BusinessName != null)
            merchant.BusinessName = request.BusinessName;
        if (request.BusinessType != null)
            merchant.BusinessType = request.BusinessType;
        if (request.StatementDescriptor != null)
            merchant.StatementDescriptor = request.StatementDescriptor;
        if (request.Address != null)
        {
            merchant.AddressLine1 = request.Address.Line1;
            merchant.AddressLine2 = request.Address.Line2;
            merchant.City = request.Address.City;
            merchant.State = request.Address.State;
            merchant.PostalCode = request.Address.PostalCode;
        }
        if (request.Metadata != null)
            merchant.Metadata = JsonSerializer.Serialize(request.Metadata);

        merchant.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = MapToDto(merchant);
        dto.AddSelfLink($"/api/v1/merchants/{merchant.Id}");

        return Ok(dto);
    }

    // API Keys management
    [HttpGet("{merchantId:guid}/api_keys")]
    public async Task<ActionResult<HalCollection<ApiKeyDto>>> GetApiKeys(Guid merchantId)
    {
        var authenticatedMerchantId = GetAuthenticatedMerchantId();
        if (authenticatedMerchantId == null || merchantId != authenticatedMerchantId)
            return NotFound();

        var apiKeys = await _context.ApiKeys
            .Where(k => k.MerchantId == merchantId && k.RevokedAt == null)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new ApiKeyDto
            {
                Id = k.Id,
                MerchantId = k.MerchantId,
                Name = k.Name,
                KeyType = k.KeyType,
                KeyPrefix = k.KeyPrefix,
                KeyHint = k.KeyHint,
                IsLive = k.IsLive,
                IsActive = k.IsActive,
                LastUsedAt = k.LastUsedAt,
                ExpiresAt = k.ExpiresAt,
                CreatedAt = k.CreatedAt
            })
            .ToListAsync();

        foreach (var key in apiKeys)
        {
            key.AddSelfLink($"/api/v1/merchants/{merchantId}/api_keys/{key.Id}");
        }

        return Ok(HalCollection<ApiKeyDto>.Create(
            apiKeys,
            $"/api/v1/merchants/{merchantId}/api_keys",
            apiKeys.Count
        ));
    }

    [HttpPost("{merchantId:guid}/api_keys")]
    public async Task<ActionResult<ApiKeyCreatedDto>> CreateApiKey(
        Guid merchantId,
        [FromBody] CreateApiKeyRequest request)
    {
        var authenticatedMerchantId = GetAuthenticatedMerchantId();
        if (authenticatedMerchantId == null || merchantId != authenticatedMerchantId)
            return NotFound();

        // Determine key prefix based on type and mode
        var prefix = (request.KeyType, request.IsLive) switch
        {
            ("secret", true) => "sk_live_",
            ("secret", false) => "sk_test_",
            ("publishable", true) => "pk_live_",
            ("publishable", false) => "pk_test_",
            _ => "sk_test_"
        };

        var (key, hash, hint) = _keyService.GenerateApiKey(prefix);

        var apiKey = new ApiKey
        {
            MerchantId = merchantId,
            Name = request.Name,
            KeyType = request.KeyType,
            KeyPrefix = prefix,
            KeyHash = hash,
            KeyHint = hint,
            IsLive = request.IsLive,
            ExpiresAt = request.ExpiresAt
        };

        _context.ApiKeys.Add(apiKey);
        await _context.SaveChangesAsync();

        var dto = new ApiKeyCreatedDto
        {
            Id = apiKey.Id,
            MerchantId = apiKey.MerchantId,
            Name = apiKey.Name,
            KeyType = apiKey.KeyType,
            KeyPrefix = apiKey.KeyPrefix,
            KeyHint = apiKey.KeyHint,
            IsLive = apiKey.IsLive,
            IsActive = apiKey.IsActive,
            ExpiresAt = apiKey.ExpiresAt,
            CreatedAt = apiKey.CreatedAt,
            Key = key // Full key only returned on creation!
        };

        dto.AddSelfLink($"/api/v1/merchants/{merchantId}/api_keys/{apiKey.Id}");

        return CreatedAtAction(nameof(GetApiKeyById), new { merchantId, id = apiKey.Id }, dto);
    }

    [HttpGet("{merchantId:guid}/api_keys/{id:guid}")]
    public async Task<ActionResult<ApiKeyDto>> GetApiKeyById(Guid merchantId, Guid id)
    {
        var authenticatedMerchantId = GetAuthenticatedMerchantId();
        if (authenticatedMerchantId == null || merchantId != authenticatedMerchantId)
            return NotFound();

        var apiKey = await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.MerchantId == merchantId && k.Id == id && k.RevokedAt == null);

        if (apiKey == null)
            return NotFound();

        var dto = new ApiKeyDto
        {
            Id = apiKey.Id,
            MerchantId = apiKey.MerchantId,
            Name = apiKey.Name,
            KeyType = apiKey.KeyType,
            KeyPrefix = apiKey.KeyPrefix,
            KeyHint = apiKey.KeyHint,
            IsLive = apiKey.IsLive,
            IsActive = apiKey.IsActive,
            LastUsedAt = apiKey.LastUsedAt,
            ExpiresAt = apiKey.ExpiresAt,
            CreatedAt = apiKey.CreatedAt
        };

        dto.AddSelfLink($"/api/v1/merchants/{merchantId}/api_keys/{apiKey.Id}");
        dto.AddLink("revoke", $"/api/v1/merchants/{merchantId}/api_keys/{apiKey.Id}/revoke", "Revoke this key");
        dto.AddLink("roll", $"/api/v1/merchants/{merchantId}/api_keys/{apiKey.Id}/roll", "Roll (replace) this key");

        return Ok(dto);
    }

    [HttpPost("{merchantId:guid}/api_keys/{id:guid}/revoke")]
    public async Task<ActionResult> RevokeApiKey(Guid merchantId, Guid id)
    {
        var authenticatedMerchantId = GetAuthenticatedMerchantId();
        if (authenticatedMerchantId == null || merchantId != authenticatedMerchantId)
            return NotFound();

        var apiKey = await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.MerchantId == merchantId && k.Id == id && k.RevokedAt == null);

        if (apiKey == null)
            return NotFound();

        apiKey.IsActive = false;
        apiKey.RevokedAt = DateTime.UtcNow;
        apiKey.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{merchantId:guid}/api_keys/{id:guid}/roll")]
    public async Task<ActionResult<ApiKeyCreatedDto>> RollApiKey(
        Guid merchantId,
        Guid id,
        [FromBody] RollApiKeyRequest request)
    {
        var authenticatedMerchantId = GetAuthenticatedMerchantId();
        if (authenticatedMerchantId == null || merchantId != authenticatedMerchantId)
            return NotFound();

        var oldApiKey = await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.MerchantId == merchantId && k.Id == id && k.RevokedAt == null);

        if (oldApiKey == null)
            return NotFound();

        // Revoke old key
        oldApiKey.IsActive = false;
        oldApiKey.RevokedAt = DateTime.UtcNow;
        oldApiKey.UpdatedAt = DateTime.UtcNow;

        // Create new key with same settings
        var (key, hash, hint) = _keyService.GenerateApiKey(oldApiKey.KeyPrefix);

        var newApiKey = new ApiKey
        {
            MerchantId = merchantId,
            Name = oldApiKey.Name,
            KeyType = oldApiKey.KeyType,
            KeyPrefix = oldApiKey.KeyPrefix,
            KeyHash = hash,
            KeyHint = hint,
            IsLive = oldApiKey.IsLive,
            ExpiresAt = request.ExpiresAt
        };

        _context.ApiKeys.Add(newApiKey);
        await _context.SaveChangesAsync();

        var dto = new ApiKeyCreatedDto
        {
            Id = newApiKey.Id,
            MerchantId = newApiKey.MerchantId,
            Name = newApiKey.Name,
            KeyType = newApiKey.KeyType,
            KeyPrefix = newApiKey.KeyPrefix,
            KeyHint = newApiKey.KeyHint,
            IsLive = newApiKey.IsLive,
            IsActive = newApiKey.IsActive,
            ExpiresAt = newApiKey.ExpiresAt,
            CreatedAt = newApiKey.CreatedAt,
            Key = key
        };

        dto.AddSelfLink($"/api/v1/merchants/{merchantId}/api_keys/{newApiKey.Id}");

        return Ok(dto);
    }

    private Guid? GetAuthenticatedMerchantId()
    {
        if (HttpContext.Items.TryGetValue("MerchantId", out var merchantIdObj) && merchantIdObj is Guid merchantId)
        {
            return merchantId;
        }
        return null;
    }

    private static MerchantDto MapToDto(Merchant merchant)
    {
        return new MerchantDto
        {
            Id = merchant.Id,
            Name = merchant.Name,
            Email = merchant.Email,
            BusinessName = merchant.BusinessName,
            BusinessType = merchant.BusinessType,
            Country = merchant.Country,
            DefaultCurrency = merchant.DefaultCurrency,
            Status = merchant.Status,
            PayoutsEnabled = merchant.PayoutsEnabled,
            ChargesEnabled = merchant.ChargesEnabled,
            StatementDescriptor = merchant.StatementDescriptor,
            Address = new AddressDto
            {
                Line1 = merchant.AddressLine1,
                Line2 = merchant.AddressLine2,
                City = merchant.City,
                State = merchant.State,
                PostalCode = merchant.PostalCode,
                Country = merchant.Country
            },
            CreatedAt = merchant.CreatedAt,
            UpdatedAt = merchant.UpdatedAt
        };
    }
}
