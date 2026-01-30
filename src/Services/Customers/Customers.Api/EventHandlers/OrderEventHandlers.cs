using DarkVelocity.Customers.Api.Data;
using DarkVelocity.Customers.Api.Entities;
using DarkVelocity.Shared.Contracts.Events;
using DarkVelocity.Shared.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DarkVelocity.Customers.Api.EventHandlers;

/// <summary>
/// Handles OrderCompleted events to award loyalty points to customers.
/// Currently limited because OrderCompleted doesn't include CustomerId.
/// When orders are linked to customers, this handler will award points.
/// </summary>
public class OrderCompletedHandler : IEventHandler<OrderCompleted>
{
    private readonly CustomersDbContext _context;
    private readonly IEventBus _eventBus;
    private readonly ILogger<OrderCompletedHandler> _logger;

    public OrderCompletedHandler(
        CustomersDbContext context,
        IEventBus eventBus,
        ILogger<OrderCompletedHandler> logger)
    {
        _context = context;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task HandleAsync(OrderCompleted @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling OrderCompleted event for order {OrderId} at location {LocationId}, total: {GrandTotal}",
            @event.OrderId,
            @event.LocationId,
            @event.GrandTotal);

        // NOTE: OrderCompleted currently doesn't include CustomerId.
        // This is marked as a future enhancement (Task 7.4).
        // For now, we log the event and skip points processing.
        // When CustomerId is added to OrderCompleted, uncomment and adapt the code below.

        _logger.LogDebug(
            "OrderCompleted event for order {OrderId} does not include CustomerId. " +
            "Points will be awarded when customer-order linking is implemented.",
            @event.OrderId);

        // Future implementation when OrderCompleted includes CustomerId:
        // await AwardPointsForOrderAsync(@event, cancellationToken);
    }

    /// <summary>
    /// Awards loyalty points to a customer for a completed order.
    /// This method will be used when OrderCompleted includes CustomerId.
    /// </summary>
    internal async Task AwardPointsForOrderAsync(
        Guid customerId,
        Guid orderId,
        Guid locationId,
        decimal grandTotal,
        CancellationToken cancellationToken = default)
    {
        // Find the customer
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

        if (customer == null)
        {
            _logger.LogWarning(
                "Customer {CustomerId} not found for order {OrderId}, skipping points award",
                customerId,
                orderId);
            return;
        }

        // Find an active loyalty membership for this customer
        var loyalty = await _context.CustomerLoyalties
            .Include(l => l.Program)
            .ThenInclude(p => p!.Tiers.OrderBy(t => t.MinimumPoints))
            .Include(l => l.CurrentTier)
            .FirstOrDefaultAsync(
                l => l.CustomerId == customerId && l.Program!.Status == "active",
                cancellationToken);

        if (loyalty == null)
        {
            _logger.LogDebug(
                "Customer {CustomerId} is not enrolled in any active loyalty program, skipping points award",
                customerId);
            return;
        }

        var program = loyalty.Program!;

        // Calculate points based on order total and program configuration
        var basePoints = (int)(grandTotal * program.PointsPerCurrencyUnit);
        var multiplier = loyalty.CurrentTier?.PointsMultiplier ?? 1.0m;
        var pointsToEarn = (int)(basePoints * multiplier);

        if (pointsToEarn <= 0)
        {
            _logger.LogDebug(
                "No points to award for order {OrderId} (total: {GrandTotal}, rate: {Rate})",
                orderId,
                grandTotal,
                program.PointsPerCurrencyUnit);
            return;
        }

        var balanceBefore = loyalty.CurrentPoints;

        // Create the points transaction
        var transaction = new PointsTransaction
        {
            CustomerLoyaltyId = loyalty.Id,
            TransactionType = "earn",
            Points = pointsToEarn,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceBefore + pointsToEarn,
            OrderId = orderId,
            LocationId = locationId,
            Description = $"Points earned from order",
            ProcessedAt = DateTime.UtcNow,
            ExpiresAt = program.PointsExpireAfterDays.HasValue
                ? DateTime.UtcNow.AddDays(program.PointsExpireAfterDays.Value)
                : null
        };

        loyalty.CurrentPoints += pointsToEarn;
        loyalty.LifetimePoints += pointsToEarn;
        loyalty.TierQualifyingPoints += pointsToEarn;
        loyalty.LastActivityAt = DateTime.UtcNow;

        // Update customer visit stats
        customer.TotalVisits++;
        customer.TotalSpend += grandTotal;
        customer.AverageOrderValue = customer.TotalSpend / customer.TotalVisits;
        customer.LastVisitAt = DateTime.UtcNow;

        _context.PointsTransactions.Add(transaction);

        // Check for tier upgrade
        string? oldTierName = loyalty.CurrentTier?.Name;
        var newTier = program.Tiers
            .Where(t => t.MinimumPoints <= loyalty.LifetimePoints)
            .OrderByDescending(t => t.MinimumPoints)
            .FirstOrDefault();

        var tierChanged = false;
        if (newTier != null && newTier.Id != loyalty.CurrentTierId)
        {
            loyalty.CurrentTierId = newTier.Id;
            tierChanged = true;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Awarded {Points} points to customer {CustomerId} for order {OrderId}. New balance: {NewBalance}",
            pointsToEarn,
            customerId,
            orderId,
            loyalty.CurrentPoints);

        // Publish PointsEarned event
        await _eventBus.PublishAsync(new PointsEarned(
            CustomerId: customerId,
            TenantId: customer.TenantId,
            ProgramId: loyalty.ProgramId,
            Points: pointsToEarn,
            NewBalance: loyalty.CurrentPoints,
            OrderId: orderId,
            LocationId: locationId
        ), cancellationToken);

        // Publish TierChanged event if tier was upgraded
        if (tierChanged && newTier != null)
        {
            _logger.LogInformation(
                "Customer {CustomerId} tier changed from {OldTier} to {NewTier}",
                customerId,
                oldTierName ?? "none",
                newTier.Name);

            await _eventBus.PublishAsync(new TierChanged(
                CustomerId: customerId,
                TenantId: customer.TenantId,
                ProgramId: loyalty.ProgramId,
                OldTierName: oldTierName,
                NewTierName: newTier.Name,
                Reason: "Points threshold reached"
            ), cancellationToken);
        }
    }
}
