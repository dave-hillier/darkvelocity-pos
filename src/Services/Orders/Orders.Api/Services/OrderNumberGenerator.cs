using DarkVelocity.Orders.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Orders.Api.Services;

public interface IOrderNumberGenerator
{
    Task<string> GenerateAsync(Guid locationId);
}

public class OrderNumberGenerator : IOrderNumberGenerator
{
    private readonly OrdersDbContext _context;

    public OrderNumberGenerator(OrdersDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateAsync(Guid locationId)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var todayOrderCount = await _context.Orders
            .CountAsync(o => o.LocationId == locationId
                && o.CreatedAt >= today
                && o.CreatedAt < tomorrow);

        var sequenceNumber = todayOrderCount + 1;
        return $"{today:yyyyMMdd}-{sequenceNumber:D4}";
    }
}
