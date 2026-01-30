using DarkVelocity.Inventory.Api.Data;
using DarkVelocity.Inventory.Api.Services;
using DarkVelocity.Shared.Contracts.Events;
using DarkVelocity.Shared.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Inventory.Api.EventHandlers;

/// <summary>
/// Handles OrderCompleted events to automatically consume inventory based on recipes.
/// For each order line, looks up the recipe by MenuItemId and consumes ingredients using FIFO.
/// Publishes StockConsumedForSale, StockBatchExhausted, and LowStockAlert events as needed.
/// </summary>
public class OrderCompletedHandler : IEventHandler<OrderCompleted>
{
    private readonly InventoryDbContext _context;
    private readonly IFifoConsumptionService _consumptionService;
    private readonly IEventBus _eventBus;
    private readonly ILogger<OrderCompletedHandler> _logger;

    public OrderCompletedHandler(
        InventoryDbContext context,
        IFifoConsumptionService consumptionService,
        IEventBus eventBus,
        ILogger<OrderCompletedHandler> logger)
    {
        _context = context;
        _consumptionService = consumptionService;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task HandleAsync(OrderCompleted @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling OrderCompleted event for order {OrderId} at location {LocationId} with {LineCount} lines",
            @event.OrderId,
            @event.LocationId,
            @event.Lines.Count);

        var allConsumptions = new List<IngredientConsumption>();
        var exhaustedBatches = new List<(Guid BatchId, Guid IngredientId)>();
        var lowStockIngredients = new List<(Guid IngredientId, string Name, decimal CurrentStock, decimal ReorderLevel, decimal ReorderQuantity)>();
        decimal totalCOGS = 0;

        foreach (var line in @event.Lines)
        {
            // Find recipe by MenuItemId
            var recipe = await _context.Recipes
                .Include(r => r.Ingredients)
                    .ThenInclude(ri => ri.Ingredient)
                .FirstOrDefaultAsync(r => r.MenuItemId == line.MenuItemId && r.IsActive, cancellationToken);

            if (recipe == null)
            {
                _logger.LogDebug(
                    "No recipe found for menu item {MenuItemId} ({ItemName}), skipping inventory consumption",
                    line.MenuItemId,
                    line.ItemName);
                continue;
            }

            _logger.LogDebug(
                "Processing recipe {RecipeCode} for menu item {MenuItemId} with {IngredientCount} ingredients, quantity {Quantity}",
                recipe.Code,
                line.MenuItemId,
                recipe.Ingredients.Count,
                line.Quantity);

            foreach (var recipeIngredient in recipe.Ingredients)
            {
                if (recipeIngredient.Ingredient == null || !recipeIngredient.Ingredient.IsActive)
                    continue;

                // Calculate effective quantity accounting for waste percentage
                var effectiveQuantity = recipeIngredient.Quantity * (1 + recipeIngredient.WastePercentage / 100);
                var totalQuantityNeeded = effectiveQuantity * line.Quantity;

                // Track batches before consumption to detect exhaustion
                var activeBatchesBefore = await _context.StockBatches
                    .Where(b => b.IngredientId == recipeIngredient.IngredientId
                        && b.LocationId == @event.LocationId
                        && b.Status == "active")
                    .Select(b => new { b.Id, b.RemainingQuantity })
                    .ToListAsync(cancellationToken);

                // Consume stock using FIFO
                var consumptionResult = await _consumptionService.ConsumeAsync(
                    @event.LocationId,
                    recipeIngredient.IngredientId,
                    totalQuantityNeeded,
                    @event.OrderId,
                    recipe.Id,
                    "sale");

                if (consumptionResult.TotalQuantityConsumed < totalQuantityNeeded)
                {
                    _logger.LogWarning(
                        "Insufficient stock for ingredient {IngredientName}: needed {Needed}, consumed {Consumed}",
                        recipeIngredient.Ingredient.Name,
                        totalQuantityNeeded,
                        consumptionResult.TotalQuantityConsumed);
                }

                if (consumptionResult.BatchConsumptions.Count > 0)
                {
                    var batchConsumptions = consumptionResult.BatchConsumptions
                        .Select(bc => new BatchConsumption(bc.BatchId, bc.QuantityConsumed, bc.UnitCost, bc.Cost))
                        .ToList();

                    allConsumptions.Add(new IngredientConsumption(
                        recipeIngredient.IngredientId,
                        recipeIngredient.Ingredient.Name,
                        consumptionResult.TotalQuantityConsumed,
                        consumptionResult.TotalCost,
                        batchConsumptions));

                    totalCOGS += consumptionResult.TotalCost;

                    // Check for exhausted batches
                    foreach (var batchBefore in activeBatchesBefore)
                    {
                        var batchAfter = await _context.StockBatches
                            .Where(b => b.Id == batchBefore.Id)
                            .Select(b => new { b.Status })
                            .FirstOrDefaultAsync(cancellationToken);

                        if (batchAfter?.Status == "exhausted")
                        {
                            exhaustedBatches.Add((batchBefore.Id, recipeIngredient.IngredientId));
                        }
                    }

                    // Check for low stock after consumption
                    var ingredient = recipeIngredient.Ingredient;
                    var currentStock = ingredient.CurrentStock ?? 0;
                    if (currentStock <= ingredient.ReorderLevel && ingredient.ReorderLevel > 0)
                    {
                        // Only add if not already in the list
                        if (!lowStockIngredients.Any(i => i.IngredientId == ingredient.Id))
                        {
                            lowStockIngredients.Add((
                                ingredient.Id,
                                ingredient.Name,
                                currentStock,
                                ingredient.ReorderLevel,
                                ingredient.ReorderQuantity));
                        }
                    }
                }
            }
        }

        // Publish StockConsumedForSale event if any stock was consumed
        if (allConsumptions.Count > 0)
        {
            var stockConsumedEvent = new StockConsumedForSale(
                @event.OrderId,
                @event.LocationId,
                allConsumptions,
                totalCOGS);

            await _eventBus.PublishAsync(stockConsumedEvent, cancellationToken);

            _logger.LogInformation(
                "Published StockConsumedForSale event for order {OrderId}: {IngredientCount} ingredients, total COGS {TotalCOGS:C}",
                @event.OrderId,
                allConsumptions.Count,
                totalCOGS);
        }

        // Publish StockBatchExhausted events for each exhausted batch
        foreach (var (batchId, ingredientId) in exhaustedBatches)
        {
            var exhaustedEvent = new StockBatchExhausted(batchId, ingredientId);
            await _eventBus.PublishAsync(exhaustedEvent, cancellationToken);

            _logger.LogInformation(
                "Published StockBatchExhausted event for batch {BatchId} (ingredient {IngredientId})",
                batchId,
                ingredientId);
        }

        // Publish LowStockAlert events for ingredients below reorder level
        foreach (var (ingredientId, name, currentStock, reorderLevel, reorderQuantity) in lowStockIngredients)
        {
            var lowStockEvent = new LowStockAlert(
                ingredientId,
                name,
                @event.LocationId,
                currentStock,
                reorderLevel,
                reorderQuantity);

            await _eventBus.PublishAsync(lowStockEvent, cancellationToken);

            _logger.LogWarning(
                "Published LowStockAlert for {IngredientName}: current stock {CurrentStock} is below reorder level {ReorderLevel}",
                name,
                currentStock,
                reorderLevel);
        }

        _logger.LogInformation(
            "Completed inventory consumption for order {OrderId}: {ConsumptionCount} ingredients consumed, " +
            "{ExhaustedCount} batches exhausted, {LowStockCount} low stock alerts",
            @event.OrderId,
            allConsumptions.Count,
            exhaustedBatches.Count,
            lowStockIngredients.Count);
    }
}
