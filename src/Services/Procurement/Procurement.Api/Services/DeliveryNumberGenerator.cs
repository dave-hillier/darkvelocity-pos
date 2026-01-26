using DarkVelocity.Procurement.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Procurement.Api.Services;

public interface IDeliveryNumberGenerator
{
    Task<string> GenerateAsync(Guid locationId);
}

public class DeliveryNumberGenerator : IDeliveryNumberGenerator
{
    private readonly ProcurementDbContext _context;

    public DeliveryNumberGenerator(ProcurementDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateAsync(Guid locationId)
    {
        var today = DateTime.UtcNow.Date;
        var prefix = $"DEL-{today:yyyyMMdd}";

        var lastDelivery = await _context.Deliveries
            .Where(d => d.LocationId == locationId && d.DeliveryNumber.StartsWith(prefix))
            .OrderByDescending(d => d.DeliveryNumber)
            .FirstOrDefaultAsync();

        int sequence = 1;
        if (lastDelivery != null)
        {
            var lastSequence = lastDelivery.DeliveryNumber.Split('-').Last();
            if (int.TryParse(lastSequence, out var parsed))
            {
                sequence = parsed + 1;
            }
        }

        return $"{prefix}-{sequence:D4}";
    }
}
