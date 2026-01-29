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
[Route("api/v1/terminals")]
public class TerminalsController : ControllerBase
{
    private readonly PaymentGatewayDbContext _context;
    private readonly KeyGenerationService _keyService;
    private readonly PaymentProcessingService _paymentService;
    private readonly WebhookService _webhookService;

    public TerminalsController(
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
    public async Task<ActionResult<HalCollection<TerminalDto>>> GetAll(
        [FromQuery] string? status = null,
        [FromQuery] Guid? external_location_id = null,
        [FromQuery] int limit = 20)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var query = _context.Terminals
            .Where(t => t.MerchantId == merchantId)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status == status);
        if (external_location_id.HasValue)
            query = query.Where(t => t.ExternalLocationId == external_location_id.Value);

        var terminals = await query
            .OrderByDescending(t => t.CreatedAt)
            .Take(Math.Min(limit, 100))
            .ToListAsync();

        var dtos = terminals.Select(MapToDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/v1/terminals/{dto.Id}");
            AddTerminalLinks(dto);
        }

        return Ok(HalCollection<TerminalDto>.Create(
            dtos,
            "/api/v1/terminals",
            await query.CountAsync()
        ));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TerminalDto>> GetById(Guid id)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var terminal = await _context.Terminals
            .FirstOrDefaultAsync(t => t.Id == id && t.MerchantId == merchantId);

        if (terminal == null)
            return NotFound();

        var dto = MapToDto(terminal);
        dto.AddSelfLink($"/api/v1/terminals/{terminal.Id}");
        AddTerminalLinks(dto);

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<TerminalRegistrationDto>> Create([FromBody] CreateTerminalRequest request)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var registrationCode = _keyService.GenerateTerminalRegistrationCode();

        var terminal = new Terminal
        {
            MerchantId = merchantId.Value,
            Label = request.Label,
            DeviceType = request.DeviceType,
            LocationName = request.LocationName,
            LocationAddress = request.LocationAddress,
            ExternalLocationId = request.ExternalLocationId,
            RegistrationCode = registrationCode,
            RegistrationCodeExpiresAt = DateTime.UtcNow.AddHours(24),
            Status = "pending",
            Metadata = request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : null
        };

        _context.Terminals.Add(terminal);
        await _context.SaveChangesAsync();

        var dto = new TerminalRegistrationDto
        {
            Id = terminal.Id,
            MerchantId = terminal.MerchantId,
            Label = terminal.Label,
            DeviceType = terminal.DeviceType,
            SerialNumber = terminal.SerialNumber,
            DeviceSwVersion = terminal.DeviceSwVersion,
            LocationName = terminal.LocationName,
            LocationAddress = terminal.LocationAddress,
            ExternalLocationId = terminal.ExternalLocationId,
            IsRegistered = terminal.IsRegistered,
            Status = terminal.Status,
            RegistrationCode = registrationCode,
            RegistrationCodeExpiresAt = terminal.RegistrationCodeExpiresAt,
            CreatedAt = terminal.CreatedAt
        };

        dto.AddSelfLink($"/api/v1/terminals/{terminal.Id}");
        dto.AddLink("register", $"/api/v1/terminals/{terminal.Id}/register", "Complete registration");

        return CreatedAtAction(nameof(GetById), new { id = terminal.Id }, dto);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<TerminalDto>> Update(Guid id, [FromBody] UpdateTerminalRequest request)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var terminal = await _context.Terminals
            .FirstOrDefaultAsync(t => t.Id == id && t.MerchantId == merchantId);

        if (terminal == null)
            return NotFound();

        if (request.Label != null)
            terminal.Label = request.Label;
        if (request.LocationName != null)
            terminal.LocationName = request.LocationName;
        if (request.LocationAddress != null)
            terminal.LocationAddress = request.LocationAddress;
        if (request.ExternalLocationId.HasValue)
            terminal.ExternalLocationId = request.ExternalLocationId;
        if (request.Metadata != null)
            terminal.Metadata = JsonSerializer.Serialize(request.Metadata);

        terminal.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = MapToDto(terminal);
        dto.AddSelfLink($"/api/v1/terminals/{terminal.Id}");
        AddTerminalLinks(dto);

        return Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var terminal = await _context.Terminals
            .FirstOrDefaultAsync(t => t.Id == id && t.MerchantId == merchantId);

        if (terminal == null)
            return NotFound();

        _context.Terminals.Remove(terminal);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:guid}/register")]
    public async Task<ActionResult<TerminalDto>> Register(Guid id, [FromBody] RegisterTerminalRequest request)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var terminal = await _context.Terminals
            .FirstOrDefaultAsync(t => t.Id == id && t.MerchantId == merchantId);

        if (terminal == null)
            return NotFound();

        if (terminal.IsRegistered)
        {
            return BadRequest(new ApiErrorDto
            {
                Error = new ApiErrorDetailDto
                {
                    Type = "invalid_request_error",
                    Code = "terminal_already_registered",
                    Message = "This terminal is already registered."
                }
            });
        }

        if (terminal.RegistrationCode != request.RegistrationCode)
        {
            return BadRequest(new ApiErrorDto
            {
                Error = new ApiErrorDetailDto
                {
                    Type = "invalid_request_error",
                    Code = "invalid_registration_code",
                    Message = "Invalid registration code."
                }
            });
        }

        if (terminal.RegistrationCodeExpiresAt.HasValue && terminal.RegistrationCodeExpiresAt < DateTime.UtcNow)
        {
            return BadRequest(new ApiErrorDto
            {
                Error = new ApiErrorDetailDto
                {
                    Type = "invalid_request_error",
                    Code = "registration_code_expired",
                    Message = "The registration code has expired."
                }
            });
        }

        terminal.IsRegistered = true;
        terminal.RegisteredAt = DateTime.UtcNow;
        terminal.Status = "online";
        terminal.RegistrationCode = null; // Clear after successful registration
        terminal.RegistrationCodeExpiresAt = null;
        terminal.UpdatedAt = DateTime.UtcNow;

        // For simulated terminals, generate a serial number
        if (terminal.DeviceType == "simulated")
        {
            terminal.SerialNumber = $"SIM-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}";
            terminal.DeviceSwVersion = "1.0.0-simulated";
        }

        await _context.SaveChangesAsync();

        // Send webhook
        await _webhookService.SendWebhookAsync(merchantId.Value, "terminal.online", "terminal", terminal.Id);

        var dto = MapToDto(terminal);
        dto.AddSelfLink($"/api/v1/terminals/{terminal.Id}");
        AddTerminalLinks(dto);

        return Ok(dto);
    }

    // Terminal Reader Actions - for POS
    [HttpPost("{id:guid}/collect_payment_method")]
    public async Task<ActionResult<TerminalReaderActionDto>> CollectPaymentMethod(
        Guid id,
        [FromBody] TerminalCollectPaymentRequest request)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var terminal = await _context.Terminals
            .FirstOrDefaultAsync(t => t.Id == id && t.MerchantId == merchantId);

        if (terminal == null)
            return NotFound();

        if (!terminal.IsRegistered || terminal.Status != "online")
        {
            return BadRequest(new ApiErrorDto
            {
                Error = new ApiErrorDetailDto
                {
                    Type = "invalid_request_error",
                    Code = "terminal_offline",
                    Message = "Terminal is not online."
                }
            });
        }

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
                    Message = "No such payment_intent."
                }
            });
        }

        if (paymentIntent.Status != "requires_payment_method")
        {
            return BadRequest(new ApiErrorDto
            {
                Error = new ApiErrorDetailDto
                {
                    Type = "invalid_request_error",
                    Code = "payment_intent_unexpected_state",
                    Message = $"PaymentIntent status must be requires_payment_method, but is {paymentIntent.Status}."
                }
            });
        }

        // Link payment intent to terminal
        paymentIntent.TerminalId = terminal.Id;
        paymentIntent.Channel = "pos";
        paymentIntent.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Update terminal last seen
        terminal.LastSeenAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var actionDto = new TerminalReaderActionDto
        {
            TerminalId = terminal.Id,
            PaymentIntentId = paymentIntent.Id,
            ActionType = "collect_payment_method",
            Status = "in_progress"
        };

        actionDto.AddSelfLink($"/api/v1/terminals/{terminal.Id}");
        actionDto.AddLink("payment_intent", $"/api/v1/payment_intents/{paymentIntent.Id}");
        actionDto.AddLink("process_payment", $"/api/v1/terminals/{terminal.Id}/process_payment", "Process the collected payment");

        return Ok(actionDto);
    }

    [HttpPost("{id:guid}/process_payment")]
    public async Task<ActionResult<TerminalReaderActionDto>> ProcessPayment(
        Guid id,
        [FromBody] TerminalCollectPaymentRequest request)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var terminal = await _context.Terminals
            .FirstOrDefaultAsync(t => t.Id == id && t.MerchantId == merchantId);

        if (terminal == null)
            return NotFound();

        if (!terminal.IsRegistered || terminal.Status != "online")
        {
            return BadRequest(new ApiErrorDto
            {
                Error = new ApiErrorDetailDto
                {
                    Type = "invalid_request_error",
                    Code = "terminal_offline",
                    Message = "Terminal is not online."
                }
            });
        }

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
                    Message = "No such payment_intent."
                }
            });
        }

        // Process the card-present payment
        var (success, transaction) = await _paymentService.ProcessCardPresentPayment(paymentIntent, terminal);

        var actionDto = new TerminalReaderActionDto
        {
            TerminalId = terminal.Id,
            PaymentIntentId = paymentIntent.Id,
            ActionType = "process_payment",
            Status = success ? "succeeded" : "failed",
            FailureCode = success ? null : transaction.FailureCode,
            FailureMessage = success ? null : transaction.FailureMessage
        };

        actionDto.AddSelfLink($"/api/v1/terminals/{terminal.Id}");
        actionDto.AddLink("payment_intent", $"/api/v1/payment_intents/{paymentIntent.Id}");

        return Ok(actionDto);
    }

    [HttpPost("{id:guid}/cancel_action")]
    public async Task<ActionResult<TerminalDto>> CancelAction(Guid id)
    {
        var merchantId = GetAuthenticatedMerchantId();
        if (merchantId == null)
            return Unauthorized();

        var terminal = await _context.Terminals
            .FirstOrDefaultAsync(t => t.Id == id && t.MerchantId == merchantId);

        if (terminal == null)
            return NotFound();

        // In a real implementation, this would cancel any pending action on the physical terminal
        terminal.LastSeenAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = MapToDto(terminal);
        dto.AddSelfLink($"/api/v1/terminals/{terminal.Id}");
        AddTerminalLinks(dto);

        return Ok(dto);
    }

    private Guid? GetAuthenticatedMerchantId()
    {
        if (HttpContext.Items.TryGetValue("MerchantId", out var merchantIdObj) && merchantIdObj is Guid merchantId)
            return merchantId;
        return null;
    }

    private void AddTerminalLinks(TerminalDto dto)
    {
        if (dto.IsRegistered && dto.Status == "online")
        {
            dto.AddLink("collect_payment_method", $"/api/v1/terminals/{dto.Id}/collect_payment_method", "Collect payment method from customer");
            dto.AddLink("process_payment", $"/api/v1/terminals/{dto.Id}/process_payment", "Process payment");
            dto.AddLink("cancel_action", $"/api/v1/terminals/{dto.Id}/cancel_action", "Cancel current action");
        }
        else if (!dto.IsRegistered)
        {
            dto.AddLink("register", $"/api/v1/terminals/{dto.Id}/register", "Complete registration");
        }
    }

    private static TerminalDto MapToDto(Terminal t)
    {
        return new TerminalDto
        {
            Id = t.Id,
            MerchantId = t.MerchantId,
            Label = t.Label,
            DeviceType = t.DeviceType,
            SerialNumber = t.SerialNumber,
            DeviceSwVersion = t.DeviceSwVersion,
            LocationName = t.LocationName,
            LocationAddress = t.LocationAddress,
            ExternalLocationId = t.ExternalLocationId,
            IsRegistered = t.IsRegistered,
            Status = t.Status,
            LastSeenAt = t.LastSeenAt,
            Metadata = !string.IsNullOrEmpty(t.Metadata)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(t.Metadata)
                : null,
            CreatedAt = t.CreatedAt
        };
    }
}
