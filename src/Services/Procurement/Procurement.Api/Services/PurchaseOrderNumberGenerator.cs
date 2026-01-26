using DarkVelocity.Procurement.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Procurement.Api.Services;

public interface IPurchaseOrderNumberGenerator
{
    Task<string> GenerateAsync(Guid locationId);
}

public class PurchaseOrderNumberGenerator : IPurchaseOrderNumberGenerator
{
    private readonly ProcurementDbContext _context;

    public PurchaseOrderNumberGenerator(ProcurementDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateAsync(Guid locationId)
    {
        var today = DateTime.UtcNow.Date;
        var prefix = $"PO-{today:yyyyMMdd}";

        var lastOrder = await _context.PurchaseOrders
            .Where(po => po.LocationId == locationId && po.OrderNumber.StartsWith(prefix))
            .OrderByDescending(po => po.OrderNumber)
            .FirstOrDefaultAsync();

        int sequence = 1;
        if (lastOrder != null)
        {
            var lastSequence = lastOrder.OrderNumber.Split('-').Last();
            if (int.TryParse(lastSequence, out var parsed))
            {
                sequence = parsed + 1;
            }
        }

        return $"{prefix}-{sequence:D4}";
    }
}
