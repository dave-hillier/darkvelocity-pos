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
    public async Task OrderCompleted_WithMatchingRecipe_ConsumesStockAndPublishesEvent()
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
    public async Task OrderCompleted_IncludesRemainingQuantityInBatchConsumptions()
    {
        // Arrange
        _fixture.ClearEventLog();

        // Create a new ingredient and batch with known quantity
        Guid testIngredientId;
        Guid testBatchId;
        Guid testMenuItemId = Guid.NewGuid();
        decimal initialBatchQuantity = 10.0m;
        decimal quantityToConsume = 3.0m;

        using (var db = _fixture.GetDbContext())
        {
            var ingredient = new Ingredient
            {
                Code = "REMAINING-TEST-" + Guid.NewGuid().ToString("N").Substring(0, 6),
                Name = "Remaining Quantity Test Ingredient",
                UnitOfMeasure = "kg",
                ReorderLevel = 0,
                ReorderQuantity = 0,
                CurrentStock = initialBatchQuantity
            };
            db.Ingredients.Add(ingredient);
            testIngredientId = ingredient.Id;

            var batch = new StockBatch
            {
                IngredientId = testIngredientId,
                LocationId = _fixture.TestLocationId,
                InitialQuantity = initialBatchQuantity,
                RemainingQuantity = initialBatchQuantity,
                UnitCost = 5.00m,
                ReceivedAt = DateTime.UtcNow.AddDays(-1),
                Status = "active"
            };
            db.StockBatches.Add(batch);
            testBatchId = batch.Id;

            var recipe = new Recipe
            {
                Code = "REMAINING-RECIPE-" + Guid.NewGuid().ToString("N").Substring(0, 6),
                Name = "Remaining Test Recipe",
                MenuItemId = testMenuItemId,
                PortionYield = 1
            };
            db.Recipes.Add(recipe);

            await db.SaveChangesAsync();

            var recipeIngredient = new RecipeIngredient
            {
                RecipeId = recipe.Id,
                IngredientId = testIngredientId,
                Quantity = quantityToConsume,
                WastePercentage = 0
            };
            db.RecipeIngredients.Add(recipeIngredient);

            await db.SaveChangesAsync();
        }

        var orderCompleted = new OrderCompleted(
            OrderId: Guid.NewGuid(),
            LocationId: _fixture.TestLocationId,
            OrderNumber: "TEST-REMAINING",
            GrandTotal: 20.00m,
            Lines: new List<OrderLineSnapshot>
            {
                new OrderLineSnapshot(
                    LineId: Guid.NewGuid(),
                    MenuItemId: testMenuItemId,
                    ItemName: "Remaining Test Item",
                    Quantity: 1,
                    UnitPrice: 20.00m,
                    LineTotal: 20.00m)
            });

        // Act
        await _fixture.GetEventBus().PublishAsync(orderCompleted);

        // Assert - Verify RemainingQuantity is in the event
        var events = _fixture.GetEventBus().GetEventLog();
        var stockConsumedEvent = events.OfType<StockConsumedForSale>()
            .FirstOrDefault(e => e.OrderId == orderCompleted.OrderId);

        stockConsumedEvent.Should().NotBeNull();

        var ingredientConsumption = stockConsumedEvent!.Consumptions
            .FirstOrDefault(c => c.IngredientId == testIngredientId);
        ingredientConsumption.Should().NotBeNull();

        var batchConsumption = ingredientConsumption!.BatchConsumptions
            .FirstOrDefault(bc => bc.BatchId == testBatchId);
        batchConsumption.Should().NotBeNull();

        // Remaining should be initial - consumed = 10 - 3 = 7
        batchConsumption!.RemainingQuantity.Should().Be(initialBatchQuantity - quantityToConsume);
        batchConsumption.Quantity.Should().Be(quantityToConsume);
    }

    [Fact]
    public async Task OrderCompleted_WhenBatchFullyConsumed_RemainingQuantityIsZero()
    {
        // Arrange
        _fixture.ClearEventLog();

        // Create a batch that will be fully exhausted
        Guid exhaustibleIngredientId;
        Guid exhaustibleBatchId;
        Guid exhaustibleMenuItemId = Guid.NewGuid();
        decimal batchQuantity = 0.5m;

        using (var db = _fixture.GetDbContext())
        {
            var ingredient = new Ingredient
            {
                Code = "EXHAUST-TEST-" + Guid.NewGuid().ToString("N").Substring(0, 6),
                Name = "Exhaustible Test Ingredient",
                UnitOfMeasure = "kg",
                ReorderLevel = 0,
                ReorderQuantity = 0,
                CurrentStock = batchQuantity
            };
            db.Ingredients.Add(ingredient);
            exhaustibleIngredientId = ingredient.Id;

            var batch = new StockBatch
            {
                IngredientId = exhaustibleIngredientId,
                LocationId = _fixture.TestLocationId,
                InitialQuantity = batchQuantity,
                RemainingQuantity = batchQuantity,
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

            await db.SaveChangesAsync();

            var recipeIngredient = new RecipeIngredient
            {
                RecipeId = recipe.Id,
                IngredientId = exhaustibleIngredientId,
                Quantity = batchQuantity, // Will exhaust the batch
                WastePercentage = 0
            };
            db.RecipeIngredients.Add(recipeIngredient);

            await db.SaveChangesAsync();
        }

        var orderCompleted = new OrderCompleted(
            OrderId: Guid.NewGuid(),
            LocationId: _fixture.TestLocationId,
            OrderNumber: "TEST-EXHAUST",
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

        // Assert - Verify batch was exhausted (RemainingQuantity = 0)
        var events = _fixture.GetEventBus().GetEventLog();
        var stockConsumedEvent = events.OfType<StockConsumedForSale>()
            .FirstOrDefault(e => e.OrderId == orderCompleted.OrderId);

        stockConsumedEvent.Should().NotBeNull();

        var batchConsumption = stockConsumedEvent!.Consumptions
            .SelectMany(c => c.BatchConsumptions)
            .FirstOrDefault(bc => bc.BatchId == exhaustibleBatchId);

        batchConsumption.Should().NotBeNull();
        batchConsumption!.RemainingQuantity.Should().Be(0);

        // Verify database state - batch should be marked exhausted
        using (var db = _fixture.GetDbContext())
        {
            var batch = await db.StockBatches.FindAsync(exhaustibleBatchId);
            batch!.Status.Should().Be("exhausted");
            batch.RemainingQuantity.Should().Be(0);
        }
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
            OrderNumber: "TEST-FIFO",
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

        // Verify event contains only older batch consumption with correct remaining
        var events = _fixture.GetEventBus().GetEventLog();
        var stockConsumedEvent = events.OfType<StockConsumedForSale>()
            .FirstOrDefault(e => e.OrderId == orderCompleted.OrderId);

        stockConsumedEvent.Should().NotBeNull();
        // COGS should be 2.0kg * $2.00/kg = $4.00 (older batch price)
        stockConsumedEvent!.TotalCOGS.Should().Be(4.00m);

        var batchConsumption = stockConsumedEvent.Consumptions
            .SelectMany(c => c.BatchConsumptions)
            .FirstOrDefault(bc => bc.BatchId == olderBatchId);
        batchConsumption.Should().NotBeNull();
        batchConsumption!.RemainingQuantity.Should().Be(3.0m);
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
            OrderNumber: "TEST-MULTI",
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
