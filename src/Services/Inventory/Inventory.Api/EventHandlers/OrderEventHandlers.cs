using DarkVelocity.Inventory.Api.Data;
using DarkVelocity.Inventory.Api.Services;
using DarkVelocity.Shared.Contracts.Events;
using DarkVelocity.Shared.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Inventory.Api.EventHandlers;

/// <summary>
/// Handles OrderCompleted events to automatically consume inventory based on recipes.
/// For each order line, looks up the recipe by MenuItemId and consumes ingredients using FIFO.
/// Publishes StockConsumedForSale event with detailed batch consumption data including remaining quantities,
/// allowing consuming services to make their own inferences about stock levels and batch status.
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
                    // Get current remaining quantities for each batch consumed
                    var batchIds = consumptionResult.BatchConsumptions.Select(bc => bc.BatchId).ToList();
                    var batchRemainingQuantities = await _context.StockBatches
                        .Where(b => batchIds.Contains(b.Id))
                        .ToDictionaryAsync(b => b.Id, b => b.RemainingQuantity, cancellationToken);

                    var batchConsumptions = consumptionResult.BatchConsumptions
                        .Select(bc => new BatchConsumption(
                            bc.BatchId,
                            bc.QuantityConsumed,
                            bc.UnitCost,
                            bc.Cost,
                            batchRemainingQuantities.GetValueOrDefault(bc.BatchId, 0)))
                        .ToList();

                    allConsumptions.Add(new IngredientConsumption(
                        recipeIngredient.IngredientId,
                        recipeIngredient.Ingredient.Name,
                        consumptionResult.TotalQuantityConsumed,
                        consumptionResult.TotalCost,
                        batchConsumptions));

                    totalCOGS += consumptionResult.TotalCost;
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

        _logger.LogInformation(
            "Completed inventory consumption for order {OrderId}: {ConsumptionCount} ingredients consumed",
            @event.OrderId,
            allConsumptions.Count);
    }
}
