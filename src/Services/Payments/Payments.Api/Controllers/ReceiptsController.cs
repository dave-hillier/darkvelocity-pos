using System.Text.Json;
using DarkVelocity.Payments.Api.Data;
using DarkVelocity.Payments.Api.Dtos;
using DarkVelocity.Payments.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Payments.Api.Controllers;

[ApiController]
[Route("api/receipts")]
public class ReceiptsController : ControllerBase
{
    private readonly PaymentsDbContext _context;

    public ReceiptsController(PaymentsDbContext context)
    {
        _context = context;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReceiptDto>> GetById(Guid id)
    {
        var receipt = await _context.Receipts
            .FirstOrDefaultAsync(r => r.Id == id);

        if (receipt == null)
            return NotFound();

        var dto = MapToDto(receipt);
        dto.AddSelfLink($"/api/receipts/{receipt.Id}");

        return Ok(dto);
    }

    [HttpGet("by-payment/{paymentId:guid}")]
    public async Task<ActionResult<ReceiptDto>> GetByPayment(Guid paymentId)
    {
        var receipt = await _context.Receipts
            .FirstOrDefaultAsync(r => r.PaymentId == paymentId);

        if (receipt == null)
            return NotFound();

        var dto = MapToDto(receipt);
        dto.AddSelfLink($"/api/receipts/{receipt.Id}");

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<ReceiptDto>> Create([FromBody] CreateReceiptRequest request)
    {
        // Verify payment exists
        var payment = await _context.Payments.FindAsync(request.PaymentId);
        if (payment == null)
            return BadRequest(new { message = "Invalid payment" });

        // Check for existing receipt
        var existingReceipt = await _context.Receipts
            .AnyAsync(r => r.PaymentId == request.PaymentId);

        if (existingReceipt)
            return Conflict(new { message = "Receipt already exists for this payment" });

        var lineItemsJson = request.LineItems != null
            ? JsonSerializer.Serialize(request.LineItems)
            : "[]";

        var receipt = new Receipt
        {
            PaymentId = request.PaymentId,
            BusinessName = request.BusinessName,
            LocationName = request.LocationName,
            AddressLine1 = request.AddressLine1,
            AddressLine2 = request.AddressLine2,
            TaxId = request.TaxId,
            OrderNumber = request.OrderNumber,
            OrderDate = request.OrderDate ?? DateTime.UtcNow,
            ServerName = request.ServerName,
            Subtotal = request.Subtotal,
            TaxTotal = request.TaxTotal,
            DiscountTotal = request.DiscountTotal,
            TipAmount = request.TipAmount,
            GrandTotal = request.GrandTotal,
            PaymentMethodName = request.PaymentMethodName,
            AmountPaid = request.AmountPaid,
            ChangeGiven = request.ChangeGiven,
            LineItemsJson = lineItemsJson
        };

        _context.Receipts.Add(receipt);
        await _context.SaveChangesAsync();

        var dto = MapToDto(receipt);
        dto.AddSelfLink($"/api/receipts/{receipt.Id}");

        return CreatedAtAction(nameof(GetById), new { id = receipt.Id }, dto);
    }

    [HttpPost("{id:guid}/print")]
    public async Task<ActionResult<ReceiptDto>> MarkPrinted(Guid id)
    {
        var receipt = await _context.Receipts.FindAsync(id);

        if (receipt == null)
            return NotFound();

        receipt.PrintedAt = DateTime.UtcNow;
        receipt.PrintCount++;
        receipt.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = MapToDto(receipt);
        dto.AddSelfLink($"/api/receipts/{receipt.Id}");

        return Ok(dto);
    }

    private static ReceiptDto MapToDto(Receipt receipt)
    {
        var lineItems = !string.IsNullOrEmpty(receipt.LineItemsJson)
            ? JsonSerializer.Deserialize<List<ReceiptLineItemDto>>(receipt.LineItemsJson) ?? new List<ReceiptLineItemDto>()
            : new List<ReceiptLineItemDto>();

        return new ReceiptDto
        {
            Id = receipt.Id,
            PaymentId = receipt.PaymentId,
            BusinessName = receipt.BusinessName,
            LocationName = receipt.LocationName,
            AddressLine1 = receipt.AddressLine1,
            AddressLine2 = receipt.AddressLine2,
            TaxId = receipt.TaxId,
            OrderNumber = receipt.OrderNumber,
            OrderDate = receipt.OrderDate,
            ServerName = receipt.ServerName,
            Subtotal = receipt.Subtotal,
            TaxTotal = receipt.TaxTotal,
            DiscountTotal = receipt.DiscountTotal,
            TipAmount = receipt.TipAmount,
            GrandTotal = receipt.GrandTotal,
            PaymentMethodName = receipt.PaymentMethodName,
            AmountPaid = receipt.AmountPaid,
            ChangeGiven = receipt.ChangeGiven,
            LineItems = lineItems,
            PrintedAt = receipt.PrintedAt,
            PrintCount = receipt.PrintCount
        };
    }
}
