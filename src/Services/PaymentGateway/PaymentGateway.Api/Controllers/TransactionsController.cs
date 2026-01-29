using System.Text.Json;
using DarkVelocity.PaymentGateway.Api.Data;
using DarkVelocity.PaymentGateway.Api.Dtos;
using DarkVelocity.PaymentGateway.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.PaymentGateway.Api.Controllers;

[ApiController]
[Route("api/v1/transactions")]
public class TransactionsController : ControllerBase
{
    private readonly PaymentGatewayDbContext _context;

    public TransactionsController(PaymentGatewayDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HalCollection<TransactionDto>>> GetAll(
        [FromQuery] Guid? payment_intent = null,
        [FromQuery] string? type = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? created_gte = null,
        [FromQuery] DateTime? created_lte = null,
        [FromQuery] int limit = 20,
        [FromQuery] string? starting_after = null)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var query = _context.Transactions
            .Where(t => t.MerchantId == merchantId)
            .AsQueryable();

        if (payment_intent.HasValue)
            query = query.Where(t => t.PaymentIntentId == payment_intent.Value);
        if (!string.IsNullOrEmpty(type))
            query = query.Where(t => t.Type == type);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status == status);
        if (created_gte.HasValue)
            query = query.Where(t => t.CreatedAt >= created_gte.Value);
        if (created_lte.HasValue)
            query = query.Where(t => t.CreatedAt <= created_lte.Value);
        if (!string.IsNullOrEmpty(starting_after) && Guid.TryParse(starting_after, out var afterId))
        {
            var afterTransaction = await _context.Transactions.FindAsync(afterId);
            if (afterTransaction != null)
                query = query.Where(t => t.CreatedAt < afterTransaction.CreatedAt);
        }

        var transactions = await query
            .OrderByDescending(t => t.CreatedAt)
            .Take(Math.Min(limit, 100))
            .ToListAsync();

        var dtos = transactions.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/v1/transactions/{dto.Id}");
            dto.AddLink("payment_intent", $"/api/v1/payment_intents/{dto.PaymentIntentId}");
        }

        var collection = HalCollection<TransactionDto>.Create(
            dtos,
            "/api/v1/transactions",
            await query.CountAsync()
        );

        if (dtos.Any())
        {
            var queryParams = new List<string> { $"starting_after={dtos.Last().Id}", $"limit={limit}" };
            if (payment_intent.HasValue)
                queryParams.Add($"payment_intent={payment_intent}");
            collection.AddLink("next", $"/api/v1/transactions?{string.Join("&", queryParams)}");
        }

        return Ok(collection);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransactionDto>> GetById(Guid id)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.MerchantId == merchantId);

        if (transaction == null)
            return NotFound();

        var dto = MapToDto(transaction);
        dto.AddSelfLink($"/api/v1/transactions/{transaction.Id}");
        dto.AddLink("payment_intent", $"/api/v1/payment_intents/{transaction.PaymentIntentId}");

        return Ok(dto);
    }

    private Guid? GetAuthenticatedMerchantId()
    {
        if (HttpContext.Items.TryGetValue("MerchantId", out var merchantIdObj) && merchantIdObj is Guid merchantId)
            return merchantId;
        return null;
    }

    private static TransactionDto MapToDto(Transaction t)
    {
        return new TransactionDto
        {
            Id = t.Id,
            MerchantId = t.MerchantId,
            PaymentIntentId = t.PaymentIntentId,
            Type = t.Type,
            Amount = t.Amount,
            Currency = t.Currency,
            Status = t.Status,
            Card = t.CardLast4 != null ? new CardDetailsDto
            {
                Brand = t.CardBrand,
                Last4 = t.CardLast4,
                Funding = t.CardFunding
            } : null,
            AuthorizationCode = t.AuthorizationCode,
            NetworkTransactionId = t.NetworkTransactionId,
            RiskLevel = t.RiskLevel,
            RiskScore = t.RiskScore,
            Failure = t.FailureCode != null ? new TransactionFailureDto
            {
                Code = t.FailureCode,
                Message = t.FailureMessage,
                DeclineCode = t.DeclineCode
            } : null,
            Metadata = !string.IsNullOrEmpty(t.Metadata)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(t.Metadata)
                : null,
            CreatedAt = t.CreatedAt
        };
    }
}
