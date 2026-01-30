using DarkVelocity.Inventory.Api.Data;
using DarkVelocity.Inventory.Api.Entities;
using DarkVelocity.Inventory.Api.EventHandlers;
using DarkVelocity.Shared.Contracts.Events;
using DarkVelocity.Shared.Infrastructure.Events;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Inventory.Tests;

public class OrderEventHandlerTests : IClassFixture<InventoryApiFixture>
{
    private readonly InventoryApiFixture _fixture;

    public OrderEventHandlerTests(InventoryApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task OrderCompleted_WithMatchingRecipe_ConsumesStockAndPublishesEvents()
    {
        // Arrange
        _fixture.ClearEventLog();

        var orderCompleted = new OrderCompleted(
            OrderId: Guid.NewGuid(),
            LocationId: _fixture.TestLocationId,
            OrderNumber: "TEST-001",
            GrandTotal: 15.00m,
            Lines: new List<OrderLineSnapshot>
            {
                new OrderLineSnapshot(
                    LineId: Guid.NewGuid(),
                    MenuItemId: _fixture.TestMenuItemId, // Links to test recipe
                    ItemName: "Classic Burger",
                    Quantity: 2,
                    UnitPrice: 7.50m,
                    LineTotal: 15.00m)
            });

        // Get initial stock levels
        decimal initialStock;
        using (var db = _fixture.GetDbContext())
        {
            var ingredient = await db.Ingredients.FindAsync(_fixture.TestIngredientId);
            initialStock = ingredient!.CurrentStock ?? 0;
        }

        // Act
        await _fixture.GetEventBus().PublishAsync(orderCompleted);

        // Assert - Verify stock was consumed
        using (var db = _fixture.GetDbContext())
        {
            var ingredient = await db.Ingredients.FindAsync(_fixture.TestIngredientId);
            var currentStock = ingredient!.CurrentStock ?? 0;

            // Recipe uses 0.15kg per portion with 5% waste = 0.1575kg per burger
            // 2 burgers = 0.315kg consumed
            var expectedConsumption = 0.15m * (1 + 5m / 100m) * 2; // 0.315
            currentStock.Should().BeApproximately(initialStock - expectedConsumption, 0.0001m);

            // Verify consumption records were created
            var consumptions = await db.StockConsumptions
                .Where(c => c.OrderId == orderCompleted.OrderId)
                .ToListAsync();
            consumptions.Should().NotBeEmpty();
            consumptions.Sum(c => c.Quantity).Should().BeApproximately(expectedConsumption, 0.0001m);
        }

        // Assert - Verify StockConsumedForSale event was published
        var events = _fixture.GetEventBus().GetEventLog();
        var stockConsumedEvent = events.OfType<StockConsumedForSale>()
            .FirstOrDefault(e => e.OrderId == orderCompleted.OrderId);

        stockConsumedEvent.Should().NotBeNull();
        stockConsumedEvent!.LocationId.Should().Be(_fixture.TestLocationId);
        stockConsumedEvent.Consumptions.Should().NotBeEmpty();
        stockConsumedEvent.TotalCOGS.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task OrderCompleted_WithNoMatchingRecipe_DoesNotConsumeStock()
    {
        // Arrange
        _fixture.ClearEventLog();

        var unknownMenuItemId = Guid.NewGuid(); // No recipe linked to this
        var orderCompleted = new OrderCompleted(
            OrderId: Guid.NewGuid(),
            LocationId: _fixture.TestLocationId,
            OrderNumber: "TEST-002",
            GrandTotal: 10.00m,
            Lines: new List<OrderLineSnapshot>
            {
                new OrderLineSnapshot(
                    LineId: Guid.NewGuid(),
                    MenuItemId: unknownMenuItemId,
                    ItemName: "Unknown Item",
                    Quantity: 1,
                    UnitPrice: 10.00m,
                    LineTotal: 10.00m)
            });

        // Get initial stock levels
        decimal initialStock;
        using (var db = _fixture.GetDbContext())
        {
            var ingredient = await db.Ingredients.FindAsync(_fixture.TestIngredientId);
            initialStock = ingredient!.CurrentStock ?? 0;
        }

        // Act
        await _fixture.GetEventBus().PublishAsync(orderCompleted);

        // Assert - Verify stock was NOT consumed
        using (var db = _fixture.GetDbContext())
        {
            var ingredient = await db.Ingredients.FindAsync(_fixture.TestIngredientId);
            var currentStock = ingredient!.CurrentStock ?? 0;
            currentStock.Should().Be(initialStock);

            // Verify no consumption records for this order
            var consumptions = await db.StockConsumptions
                .Where(c => c.OrderId == orderCompleted.OrderId)
                .ToListAsync();
            consumptions.Should().BeEmpty();
        }

        // Assert - No StockConsumedForSale event should be published
        var events = _fixture.GetEventBus().GetEventLog();
        var stockConsumedEvent = events.OfType<StockConsumedForSale>()
            .FirstOrDefault(e => e.OrderId == orderCompleted.OrderId);
        stockConsumedEvent.Should().BeNull();
    }

    [Fact]
    public async Task OrderCompleted_WhenBatchExhausted_PublishesStockBatchExhaustedEvent()
    {
        // Arrange
        _fixture.ClearEventLog();

        // Create a new ingredient and batch that will be exhausted
        Guid exhaustibleIngredientId;
        Guid exhaustibleBatchId;
        Guid exhaustibleRecipeId;
        Guid exhaustibleMenuItemId = Guid.NewGuid();

        using (var db = _fixture.GetDbContext())
        {
            var ingredient = new Ingredient
            {
                Code = "EXHAUST-TEST-" + Guid.NewGuid().ToString("N").Substring(0, 6),
                Name = "Exhaustible Test Ingredient",
                UnitOfMeasure = "kg",
                ReorderLevel = 0.5m,
                ReorderQuantity = 1m,
                CurrentStock = 0.5m
            };
            db.Ingredients.Add(ingredient);
            exhaustibleIngredientId = ingredient.Id;

            var batch = new StockBatch
            {
                IngredientId = exhaustibleIngredientId,
                LocationId = _fixture.TestLocationId,
                InitialQuantity = 0.5m,
                RemainingQuantity = 0.5m, // Small amount that will be exhausted
                UnitCost = 10.00m,
                ReceivedAt = DateTime.UtcNow.AddDays(-1),
                Status = "active"
            };
            db.StockBatches.Add(batch);
            exhaustibleBatchId = batch.Id;

            var recipe = new Recipe
            {
                Code = "EXHAUST-RECIPE-" + Guid.NewGuid().ToString("N").Substring(0, 6),
                Name = "Exhaust Test Recipe",
                MenuItemId = exhaustibleMenuItemId,
                PortionYield = 1
            };
            db.Recipes.Add(recipe);
            exhaustibleRecipeId = recipe.Id;

            await db.SaveChangesAsync();

            var recipeIngredient = new RecipeIngredient
            {
                RecipeId = exhaustibleRecipeId,
                IngredientId = exhaustibleIngredientId,
                Quantity = 0.5m, // Will exhaust the batch
                WastePercentage = 0
            };
            db.RecipeIngredients.Add(recipeIngredient);

            await db.SaveChangesAsync();
        }

        var orderCompleted = new OrderCompleted(
            OrderId: Guid.NewGuid(),
            LocationId: _fixture.TestLocationId,
            OrderNumber: "TEST-003",
            GrandTotal: 20.00m,
            Lines: new List<OrderLineSnapshot>
            {
                new OrderLineSnapshot(
                    LineId: Guid.NewGuid(),
                    MenuItemId: exhaustibleMenuItemId,
                    ItemName: "Exhaust Test Item",
                    Quantity: 1,
                    UnitPrice: 20.00m,
                    LineTotal: 20.00m)
            });

        // Act
        await _fixture.GetEventBus().PublishAsync(orderCompleted);

        // Assert - Verify batch was exhausted
        using (var db = _fixture.GetDbContext())
        {
            var batch = await db.StockBatches.FindAsync(exhaustibleBatchId);
            batch!.Status.Should().Be("exhausted");
            batch.RemainingQuantity.Should().Be(0);
        }

        // Assert - Verify StockBatchExhausted event was published
        var events = _fixture.GetEventBus().GetEventLog();
        var exhaustedEvent = events.OfType<StockBatchExhausted>()
            .FirstOrDefault(e => e.BatchId == exhaustibleBatchId);

        exhaustedEvent.Should().NotBeNull();
        exhaustedEvent!.IngredientId.Should().Be(exhaustibleIngredientId);
    }

    [Fact]
    public async Task OrderCompleted_WhenStockFallsBelowReorderLevel_PublishesLowStockAlert()
    {
        // Arrange
        _fixture.ClearEventLog();

        // Create a new ingredient with stock close to reorder level
        Guid lowStockIngredientId;
        Guid lowStockMenuItemId = Guid.NewGuid();

        using (var db = _fixture.GetDbContext())
        {
            var ingredient = new Ingredient
            {
                Code = "LOWSTOCK-TEST-" + Guid.NewGuid().ToString("N").Substring(0, 6),
                Name = "Low Stock Test Ingredient",
                UnitOfMeasure = "kg",
                ReorderLevel = 2.0m, // Will trigger alert when stock falls below this
                ReorderQuantity = 5.0m,
                CurrentStock = 2.5m // Just above reorder level
            };
            db.Ingredients.Add(ingredient);
            lowStockIngredientId = ingredient.Id;

            var batch = new StockBatch
            {
                IngredientId = lowStockIngredientId,
                LocationId = _fixture.TestLocationId,
                InitialQuantity = 2.5m,
                RemainingQuantity = 2.5m,
                UnitCost = 5.00m,
                ReceivedAt = DateTime.UtcNow.AddDays(-1),
                Status = "active"
            };
            db.StockBatches.Add(batch);

            var recipe = new Recipe
            {
                Code = "LOWSTOCK-RECIPE-" + Guid.NewGuid().ToString("N").Substring(0, 6),
                Name = "Low Stock Test Recipe",
                MenuItemId = lowStockMenuItemId,
                PortionYield = 1
            };
            db.Recipes.Add(recipe);

            await db.SaveChangesAsync();

            var recipeIngredient = new RecipeIngredient
            {
                RecipeId = recipe.Id,
                IngredientId = lowStockIngredientId,
                Quantity = 1.0m, // Will bring stock to 1.5, below reorder level of 2.0
                WastePercentage = 0
            };
            db.RecipeIngredients.Add(recipeIngredient);

            await db.SaveChangesAsync();
        }

        var orderCompleted = new OrderCompleted(
            OrderId: Guid.NewGuid(),
            LocationId: _fixture.TestLocationId,
            OrderNumber: "TEST-004",
            GrandTotal: 15.00m,
            Lines: new List<OrderLineSnapshot>
            {
                new OrderLineSnapshot(
                    LineId: Guid.NewGuid(),
                    MenuItemId: lowStockMenuItemId,
                    ItemName: "Low Stock Test Item",
                    Quantity: 1,
                    UnitPrice: 15.00m,
                    LineTotal: 15.00m)
            });

        // Act
        await _fixture.GetEventBus().PublishAsync(orderCompleted);

        // Assert - Verify LowStockAlert event was published
        var events = _fixture.GetEventBus().GetEventLog();
        var lowStockEvent = events.OfType<LowStockAlert>()
            .FirstOrDefault(e => e.IngredientId == lowStockIngredientId);

        lowStockEvent.Should().NotBeNull();
        lowStockEvent!.IngredientName.Should().Be("Low Stock Test Ingredient");
        lowStockEvent.LocationId.Should().Be(_fixture.TestLocationId);
        lowStockEvent.CurrentStock.Should().BeLessThanOrEqualTo(2.0m);
        lowStockEvent.ReorderLevel.Should().Be(2.0m);
        lowStockEvent.ReorderQuantity.Should().Be(5.0m);
    }

    [Fact]
    public async Task OrderCompleted_UsesFifoConsumption_ConsumesOldestBatchFirst()
    {
        // Arrange
        _fixture.ClearEventLog();

        // Create ingredient with two batches at different prices
        Guid fifoIngredientId;
        Guid olderBatchId;
        Guid newerBatchId;
        Guid fifoMenuItemId = Guid.NewGuid();

        using (var db = _fixture.GetDbContext())
        {
            var ingredient = new Ingredient
            {
                Code = "FIFO-TEST-" + Guid.NewGuid().ToString("N").Substring(0, 6),
                Name = "FIFO Test Ingredient",
                UnitOfMeasure = "kg",
                ReorderLevel = 0,
                ReorderQuantity = 0,
                CurrentStock = 10.0m
            };
            db.Ingredients.Add(ingredient);
            fifoIngredientId = ingredient.Id;

            // Older batch - cheaper price
            var olderBatch = new StockBatch
            {
                IngredientId = fifoIngredientId,
                LocationId = _fixture.TestLocationId,
                InitialQuantity = 5.0m,
                RemainingQuantity = 5.0m,
                UnitCost = 2.00m, // Older, cheaper
                ReceivedAt = DateTime.UtcNow.AddDays(-10), // Older
                Status = "active"
            };
            db.StockBatches.Add(olderBatch);
            olderBatchId = olderBatch.Id;

            // Newer batch - more expensive
            var newerBatch = new StockBatch
            {
                IngredientId = fifoIngredientId,
                LocationId = _fixture.TestLocationId,
                InitialQuantity = 5.0m,
                RemainingQuantity = 5.0m,
                UnitCost = 4.00m, // Newer, more expensive
                ReceivedAt = DateTime.UtcNow.AddDays(-1), // Newer
                Status = "active"
            };
            db.StockBatches.Add(newerBatch);
            newerBatchId = newerBatch.Id;

            var recipe = new Recipe
            {
                Code = "FIFO-RECIPE-" + Guid.NewGuid().ToString("N").Substring(0, 6),
                Name = "FIFO Test Recipe",
                MenuItemId = fifoMenuItemId,
                PortionYield = 1
            };
            db.Recipes.Add(recipe);

            await db.SaveChangesAsync();

            var recipeIngredient = new RecipeIngredient
            {
                RecipeId = recipe.Id,
                IngredientId = fifoIngredientId,
                Quantity = 2.0m, // Will consume from older batch
                WastePercentage = 0
            };
            db.RecipeIngredients.Add(recipeIngredient);

            await db.SaveChangesAsync();
        }

        var orderCompleted = new OrderCompleted(
            OrderId: Guid.NewGuid(),
            LocationId: _fixture.TestLocationId,
            OrderNumber: "TEST-005",
            GrandTotal: 10.00m,
            Lines: new List<OrderLineSnapshot>
            {
                new OrderLineSnapshot(
                    LineId: Guid.NewGuid(),
                    MenuItemId: fifoMenuItemId,
                    ItemName: "FIFO Test Item",
                    Quantity: 1,
                    UnitPrice: 10.00m,
                    LineTotal: 10.00m)
            });

        // Act
        await _fixture.GetEventBus().PublishAsync(orderCompleted);

        // Assert - Verify older batch was consumed first (FIFO)
        using (var db = _fixture.GetDbContext())
        {
            var olderBatch = await db.StockBatches.FindAsync(olderBatchId);
            var newerBatch = await db.StockBatches.FindAsync(newerBatchId);

            // Older batch should have reduced quantity
            olderBatch!.RemainingQuantity.Should().Be(3.0m); // 5.0 - 2.0 = 3.0

            // Newer batch should be unchanged
            newerBatch!.RemainingQuantity.Should().Be(5.0m);
        }

        // Verify COGS used older batch price
        var events = _fixture.GetEventBus().GetEventLog();
        var stockConsumedEvent = events.OfType<StockConsumedForSale>()
            .FirstOrDefault(e => e.OrderId == orderCompleted.OrderId);

        stockConsumedEvent.Should().NotBeNull();
        // COGS should be 2.0kg * $2.00/kg = $4.00 (older batch price)
        stockConsumedEvent!.TotalCOGS.Should().Be(4.00m);
    }

    [Fact]
    public async Task OrderCompleted_MultipleLineItems_ConsumesStockForAll()
    {
        // Arrange
        _fixture.ClearEventLog();

        // Create two ingredients and recipes
        Guid ingredient1Id;
        Guid ingredient2Id;
        Guid menuItem1Id = Guid.NewGuid();
        Guid menuItem2Id = Guid.NewGuid();

        using (var db = _fixture.GetDbContext())
        {
            var ingredient1 = new Ingredient
            {
                Code = "MULTI-1-" + Guid.NewGuid().ToString("N").Substring(0, 6),
                Name = "Multi Test Ingredient 1",
                UnitOfMeasure = "kg",
                ReorderLevel = 0,
                ReorderQuantity = 0,
                CurrentStock = 10.0m
            };
            db.Ingredients.Add(ingredient1);
            ingredient1Id = ingredient1.Id;

            var ingredient2 = new Ingredient
            {
                Code = "MULTI-2-" + Guid.NewGuid().ToString("N").Substring(0, 6),
                Name = "Multi Test Ingredient 2",
                UnitOfMeasure = "pieces",
                ReorderLevel = 0,
                ReorderQuantity = 0,
                CurrentStock = 20.0m
            };
            db.Ingredients.Add(ingredient2);
            ingredient2Id = ingredient2.Id;

            // Batches for both ingredients
            db.StockBatches.Add(new StockBatch
            {
                IngredientId = ingredient1Id,
                LocationId = _fixture.TestLocationId,
                InitialQuantity = 10.0m,
                RemainingQuantity = 10.0m,
                UnitCost = 5.00m,
                ReceivedAt = DateTime.UtcNow,
                Status = "active"
            });

            db.StockBatches.Add(new StockBatch
            {
                IngredientId = ingredient2Id,
                LocationId = _fixture.TestLocationId,
                InitialQuantity = 20.0m,
                RemainingQuantity = 20.0m,
                UnitCost = 0.50m,
                ReceivedAt = DateTime.UtcNow,
                Status = "active"
            });

            var recipe1 = new Recipe
            {
                Code = "MULTI-RECIPE-1-" + Guid.NewGuid().ToString("N").Substring(0, 6),
                Name = "Multi Test Recipe 1",
                MenuItemId = menuItem1Id,
                PortionYield = 1
            };
            db.Recipes.Add(recipe1);

            var recipe2 = new Recipe
            {
                Code = "MULTI-RECIPE-2-" + Guid.NewGuid().ToString("N").Substring(0, 6),
                Name = "Multi Test Recipe 2",
                MenuItemId = menuItem2Id,
                PortionYield = 1
            };
            db.Recipes.Add(recipe2);

            await db.SaveChangesAsync();

            db.RecipeIngredients.Add(new RecipeIngredient
            {
                RecipeId = recipe1.Id,
                IngredientId = ingredient1Id,
                Quantity = 0.5m,
                WastePercentage = 0
            });

            db.RecipeIngredients.Add(new RecipeIngredient
            {
                RecipeId = recipe2.Id,
                IngredientId = ingredient2Id,
                Quantity = 2.0m,
                WastePercentage = 0
            });

            await db.SaveChangesAsync();
        }

        var orderCompleted = new OrderCompleted(
            OrderId: Guid.NewGuid(),
            LocationId: _fixture.TestLocationId,
            OrderNumber: "TEST-006",
            GrandTotal: 25.00m,
            Lines: new List<OrderLineSnapshot>
            {
                new OrderLineSnapshot(
                    LineId: Guid.NewGuid(),
                    MenuItemId: menuItem1Id,
                    ItemName: "Multi Item 1",
                    Quantity: 2,
                    UnitPrice: 10.00m,
                    LineTotal: 20.00m),
                new OrderLineSnapshot(
                    LineId: Guid.NewGuid(),
                    MenuItemId: menuItem2Id,
                    ItemName: "Multi Item 2",
                    Quantity: 1,
                    UnitPrice: 5.00m,
                    LineTotal: 5.00m)
            });

        // Act
        await _fixture.GetEventBus().PublishAsync(orderCompleted);

        // Assert - Verify both ingredients were consumed
        using (var db = _fixture.GetDbContext())
        {
            var ingredient1 = await db.Ingredients.FindAsync(ingredient1Id);
            var ingredient2 = await db.Ingredients.FindAsync(ingredient2Id);

            // Ingredient 1: 10 - (0.5 * 2) = 9
            ingredient1!.CurrentStock.Should().Be(9.0m);

            // Ingredient 2: 20 - (2.0 * 1) = 18
            ingredient2!.CurrentStock.Should().Be(18.0m);
        }

        // Verify StockConsumedForSale event has both ingredients
        var events = _fixture.GetEventBus().GetEventLog();
        var stockConsumedEvent = events.OfType<StockConsumedForSale>()
            .FirstOrDefault(e => e.OrderId == orderCompleted.OrderId);

        stockConsumedEvent.Should().NotBeNull();
        stockConsumedEvent!.Consumptions.Should().HaveCount(2);

        // COGS: (0.5kg * 2 * $5.00) + (2 pieces * 1 * $0.50) = $5.00 + $1.00 = $6.00
        stockConsumedEvent.TotalCOGS.Should().Be(6.00m);
    }
}
