using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// BDD-style tests for stock and inventory management.
/// Tests follow Given-When-Then pattern and are sociable/integrated,
/// covering interactions between multiple grains and real-world scenarios.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "BDD")]
public class StockInventoryBddTests
{
    private readonly TestClusterFixture _fixture;

    public StockInventoryBddTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    // ============================================================================
    // Feature: Stock Consumption on Sales
    // As a restaurant manager
    // I want stock levels to decrease when items are sold
    // So that I can track ingredient usage accurately
    // ============================================================================

    [Fact]
    public async Task Given_StockAtLocation_When_SaleOccurs_Then_ReportedStockLevelsDecrease()
    {
        // Given: 100 kg of ground beef at the main kitchen
        var (inventory, context) = await GivenStockAtLocation(
            ingredientName: "Ground Beef",
            quantity: 100m,
            unit: "kg");

        // When: A sale consumes 5 kg for burgers
        await WhenSaleConsumesStock(inventory, quantity: 5m, context.OrderId);

        // Then: Reported stock level is 95 kg
        var levelInfo = await inventory.GetLevelInfoAsync();
        levelInfo.QuantityAvailable.Should().Be(95m);
        levelInfo.QuantityOnHand.Should().Be(95m);
    }

    [Fact]
    public async Task Given_StockAtLocation_When_MultipleSalesOccur_Then_StockLevelReflectsCumulativeConsumption()
    {
        // Given: 200 kg of flour at the bakery
        var (inventory, context) = await GivenStockAtLocation(
            ingredientName: "All-Purpose Flour",
            quantity: 200m,
            unit: "kg");

        // When: Multiple sales occur throughout the day
        await WhenSaleConsumesStock(inventory, quantity: 25m, Guid.NewGuid()); // Morning pastries
        await WhenSaleConsumesStock(inventory, quantity: 30m, Guid.NewGuid()); // Lunch bread
        await WhenSaleConsumesStock(inventory, quantity: 45m, Guid.NewGuid()); // Dinner service

        // Then: Reported stock level reflects cumulative consumption (200 - 25 - 30 - 45 = 100)
        var levelInfo = await inventory.GetLevelInfoAsync();
        levelInfo.QuantityAvailable.Should().Be(100m);
    }

    [Fact]
    public async Task Given_StockAtLocation_When_SaleConsumesExactStock_Then_StockLevelIsZero()
    {
        // Given: Exactly 50 portions of salmon at the sushi bar
        var (inventory, context) = await GivenStockAtLocation(
            ingredientName: "Fresh Salmon",
            quantity: 50m,
            unit: "portions");

        // When: A large party orders all available salmon
        await WhenSaleConsumesStock(inventory, quantity: 50m, context.OrderId);

        // Then: Stock level is zero and marked as out of stock
        var levelInfo = await inventory.GetLevelInfoAsync();
        levelInfo.QuantityAvailable.Should().Be(0m);
        levelInfo.Level.Should().Be(StockLevel.OutOfStock);
    }

    [Fact]
    public async Task Given_InsufficientStock_When_SaleAttempted_Then_ConsumptionFails()
    {
        // Given: Only 10 bottles of premium wine
        var (inventory, _) = await GivenStockAtLocation(
            ingredientName: "Chateau Margaux 2015",
            quantity: 10m,
            unit: "bottles");

        // When: Attempting to sell 15 bottles
        var act = () => WhenSaleConsumesStock(inventory, quantity: 15m, Guid.NewGuid());

        // Then: The sale consumption fails with insufficient stock
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Insufficient stock*");
    }

    // ============================================================================
    // Feature: FIFO Batch Consumption
    // As a food safety manager
    // I want oldest stock used first
    // So that we minimize waste from expiration
    // ============================================================================

    [Fact]
    public async Task Given_MultipleBatchesAtLocation_When_SaleOccurs_Then_OldestBatchConsumedFirst()
    {
        // Given: Three batches of chicken received on different days
        var context = CreateTestContext();
        var inventory = await CreateInventoryGrain(context, "Chicken Breast", "kg");

        // Batch 1: Received Monday (oldest)
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand(
            "CHICKEN-MON-001", 30m, 8.00m, DateTime.UtcNow.AddDays(7)));

        // Batch 2: Received Wednesday
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand(
            "CHICKEN-WED-001", 30m, 8.50m, DateTime.UtcNow.AddDays(9)));

        // Batch 3: Received Friday (newest)
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand(
            "CHICKEN-FRI-001", 30m, 9.00m, DateTime.UtcNow.AddDays(11)));

        // When: A sale consumes 40 kg of chicken
        var result = await inventory.ConsumeForOrderAsync(Guid.NewGuid(), 40m, Guid.NewGuid());

        // Then: All of Monday's batch and 10 kg from Wednesday's batch are consumed (FIFO)
        result.BatchBreakdown.Should().HaveCount(2);

        var mondayBatchConsumption = result.BatchBreakdown.First(b => b.BatchNumber == "CHICKEN-MON-001");
        mondayBatchConsumption.Quantity.Should().Be(30m); // Entire batch consumed

        var wednesdayBatchConsumption = result.BatchBreakdown.First(b => b.BatchNumber == "CHICKEN-WED-001");
        wednesdayBatchConsumption.Quantity.Should().Be(10m); // Partial batch consumed

        // And: Remaining stock is 50 kg (90 - 40)
        var levelInfo = await inventory.GetLevelInfoAsync();
        levelInfo.QuantityAvailable.Should().Be(50m);
    }

    [Fact]
    public async Task Given_MultipleBatchesWithDifferentCosts_When_SaleOccurs_Then_CostReflectsFifoConsumption()
    {
        // Given: Two batches of olive oil at different costs
        var context = CreateTestContext();
        var inventory = await CreateInventoryGrain(context, "Extra Virgin Olive Oil", "liters");

        // Older batch at lower cost
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand(
            "OIL-OLD", 20m, 10.00m));

        // Newer batch at higher cost
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand(
            "OIL-NEW", 20m, 15.00m));

        // When: A sale consumes 25 liters
        var result = await inventory.ConsumeForOrderAsync(Guid.NewGuid(), 25m, Guid.NewGuid());

        // Then: Cost calculation uses FIFO (20 * $10 + 5 * $15 = $275)
        result.TotalCost.Should().Be(275m);

        // And: Batch breakdown shows correct allocation
        result.BatchBreakdown.Should().Contain(b => b.BatchNumber == "OIL-OLD" && b.Quantity == 20m);
        result.BatchBreakdown.Should().Contain(b => b.BatchNumber == "OIL-NEW" && b.Quantity == 5m);
    }

    [Fact]
    public async Task Given_BatchFullyConsumed_When_NextSaleOccurs_Then_NextBatchUsed()
    {
        // Given: Two batches of butter
        var context = CreateTestContext();
        var inventory = await CreateInventoryGrain(context, "Unsalted Butter", "kg");

        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BUTTER-A", 10m, 5.00m));
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand("BUTTER-B", 10m, 5.50m));

        // When: First sale exhausts first batch
        await inventory.ConsumeForOrderAsync(Guid.NewGuid(), 10m, Guid.NewGuid());

        // Then: Only second batch remains active
        var activeBatches = await inventory.GetActiveBatchesAsync();
        activeBatches.Should().HaveCount(1);
        activeBatches[0].BatchNumber.Should().Be("BUTTER-B");

        // When: Second sale occurs
        var secondResult = await inventory.ConsumeForOrderAsync(Guid.NewGuid(), 3m, Guid.NewGuid());

        // Then: Consumption comes from second batch
        secondResult.BatchBreakdown.Should().ContainSingle()
            .Which.BatchNumber.Should().Be("BUTTER-B");
    }

    // ============================================================================
    // Feature: Stock Level Thresholds
    // As a purchasing manager
    // I want to be alerted when stock falls below reorder points
    // So that I can reorder before running out
    // ============================================================================

    [Fact]
    public async Task Given_StockAboveReorderPoint_When_SaleBringsStockBelowReorderPoint_Then_StockLevelIsLow()
    {
        // Given: 50 kg of cheese with reorder point at 20 kg
        var (inventory, _) = await GivenStockAtLocation(
            ingredientName: "Cheddar Cheese",
            quantity: 50m,
            unit: "kg",
            reorderPoint: 20m,
            parLevel: 100m);

        // Verify starting state
        var initialLevel = await inventory.GetStockLevelAsync();
        initialLevel.Should().Be(StockLevel.Normal);

        // When: Sales consume 35 kg, bringing stock to 15 kg (below reorder point)
        await WhenSaleConsumesStock(inventory, quantity: 35m, Guid.NewGuid());

        // Then: Stock level is marked as Low
        var levelInfo = await inventory.GetLevelInfoAsync();
        levelInfo.QuantityAvailable.Should().Be(15m);
        levelInfo.Level.Should().Be(StockLevel.Low);
    }

    [Fact]
    public async Task Given_StockAtParLevel_When_Consumed_Then_StockLevelTransitionsCorrectly()
    {
        // Given: 60 kg of tomatoes (above par level of 50, reorder at 10)
        var (inventory, _) = await GivenStockAtLocation(
            ingredientName: "Roma Tomatoes",
            quantity: 60m,
            unit: "kg",
            reorderPoint: 10m,
            parLevel: 50m);

        // Initial state should be AbovePar
        var initialLevel = await inventory.GetStockLevelAsync();
        initialLevel.Should().Be(StockLevel.AbovePar);

        // When: Consumption brings stock to 40 (Normal range)
        await WhenSaleConsumesStock(inventory, quantity: 20m, Guid.NewGuid());
        var normalLevel = await inventory.GetStockLevelAsync();
        normalLevel.Should().Be(StockLevel.Normal);

        // When: Further consumption brings stock to 8 (Low)
        await WhenSaleConsumesStock(inventory, quantity: 32m, Guid.NewGuid());
        var lowLevel = await inventory.GetStockLevelAsync();
        lowLevel.Should().Be(StockLevel.Low);

        // When: Final consumption depletes stock
        await WhenSaleConsumesStock(inventory, quantity: 8m, Guid.NewGuid());
        var depletedLevel = await inventory.GetStockLevelAsync();
        depletedLevel.Should().Be(StockLevel.OutOfStock);
    }

    // ============================================================================
    // Feature: Order Void Restores Stock
    // As a manager
    // I want voided orders to restore inventory
    // So that stock levels remain accurate after cancellations
    // ============================================================================

    [Fact]
    public async Task Given_StockConsumedForOrder_When_OrderIsVoided_Then_StockIsRestored()
    {
        // Given: 100 units of pasta and a completed order
        var (inventory, context) = await GivenStockAtLocation(
            ingredientName: "Fresh Pasta",
            quantity: 100m,
            unit: "portions");

        var orderId = Guid.NewGuid();
        await WhenSaleConsumesStock(inventory, quantity: 20m, orderId);

        var levelAfterSale = await inventory.GetLevelInfoAsync();
        levelAfterSale.QuantityAvailable.Should().Be(80m);

        // Get the consumption movement for reversal
        var state = await inventory.GetStateAsync();
        var consumptionMovement = state.RecentMovements.First(m => m.ReferenceId == orderId);

        // When: The order is voided and consumption is reversed
        await inventory.ReverseConsumptionAsync(
            consumptionMovement.Id,
            "Order voided - customer cancelled",
            Guid.NewGuid());

        // Then: Stock is restored to original level
        var levelAfterVoid = await inventory.GetLevelInfoAsync();
        levelAfterVoid.QuantityAvailable.Should().Be(100m);
    }

    [Fact]
    public async Task Given_MultipleOrdersConsumedStock_When_OneOrderVoided_Then_OnlyThatOrderStockRestored()
    {
        // Given: 200 units of rice
        var (inventory, _) = await GivenStockAtLocation(
            ingredientName: "Jasmine Rice",
            quantity: 200m,
            unit: "portions");

        // Two orders consume stock
        var order1Id = Guid.NewGuid();
        var order2Id = Guid.NewGuid();
        await WhenSaleConsumesStock(inventory, quantity: 30m, order1Id);
        await WhenSaleConsumesStock(inventory, quantity: 50m, order2Id);

        var levelAfterOrders = await inventory.GetLevelInfoAsync();
        levelAfterOrders.QuantityAvailable.Should().Be(120m); // 200 - 30 - 50

        // When: Only order1 is voided
        var state = await inventory.GetStateAsync();
        var order1Movement = state.RecentMovements.First(m => m.ReferenceId == order1Id);
        await inventory.ReverseConsumptionAsync(order1Movement.Id, "Order 1 voided", Guid.NewGuid());

        // Then: Only order1's consumption is restored
        var levelAfterVoid = await inventory.GetLevelInfoAsync();
        levelAfterVoid.QuantityAvailable.Should().Be(150m); // 120 + 30
    }

    // ============================================================================
    // Feature: Stock Transfer Between Locations
    // As an operations manager
    // I want to transfer stock between sites
    // So that I can balance inventory across locations
    // ============================================================================

    [Fact]
    public async Task Given_StockAtSourceLocation_When_TransferredToDestination_Then_BothLocationsReflectChange()
    {
        // Given: 100 bottles of wine at main warehouse, 20 at satellite bar
        var context = CreateTestContext();

        var warehouseInventory = await CreateInventoryGrain(
            context with { SiteId = Guid.NewGuid() },
            "Pinot Noir Reserve",
            "bottles");
        await warehouseInventory.ReceiveBatchAsync(new ReceiveBatchCommand("WINE-WH-001", 100m, 25.00m));

        var barSiteId = Guid.NewGuid();
        var barInventory = await CreateInventoryGrain(
            context with { SiteId = barSiteId },
            "Pinot Noir Reserve",
            "bottles");
        await barInventory.ReceiveBatchAsync(new ReceiveBatchCommand("WINE-BAR-001", 20m, 25.00m));

        // When: 30 bottles transferred from warehouse to bar
        var transferId = Guid.NewGuid();
        await warehouseInventory.TransferOutAsync(
            new TransferOutCommand(30m, barSiteId, transferId, Guid.NewGuid()));

        await barInventory.ReceiveTransferAsync(
            new ReceiveTransferCommand(30m, 25.00m, context.SiteId, transferId, "TRANSFER-001"));

        // Then: Warehouse has 70 bottles, bar has 50 bottles
        var warehouseLevel = await warehouseInventory.GetLevelInfoAsync();
        warehouseLevel.QuantityAvailable.Should().Be(70m);

        var barLevel = await barInventory.GetLevelInfoAsync();
        barLevel.QuantityAvailable.Should().Be(50m);
    }

    [Fact]
    public async Task Given_InsufficientStockAtSource_When_TransferAttempted_Then_TransferFails()
    {
        // Given: Only 10 kg of specialty coffee at source
        var (sourceInventory, context) = await GivenStockAtLocation(
            ingredientName: "Ethiopian Yirgacheffe",
            quantity: 10m,
            unit: "kg");

        // When: Attempting to transfer 20 kg
        var act = () => sourceInventory.TransferOutAsync(
            new TransferOutCommand(20m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        // Then: Transfer fails with insufficient stock
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Insufficient*");
    }

    // ============================================================================
    // Feature: Waste Recording
    // As a kitchen manager
    // I want to record waste accurately
    // So that I can track shrinkage and adjust ordering
    // ============================================================================

    [Fact]
    public async Task Given_StockAtLocation_When_WasteRecorded_Then_StockLevelDecreases()
    {
        // Given: 50 kg of fresh vegetables
        var (inventory, _) = await GivenStockAtLocation(
            ingredientName: "Mixed Salad Greens",
            quantity: 50m,
            unit: "kg");

        // When: 5 kg recorded as waste due to spoilage
        await inventory.RecordWasteAsync(
            new RecordWasteCommand(5m, "Wilted overnight", "Spoilage", Guid.NewGuid()));

        // Then: Stock level is 45 kg
        var levelInfo = await inventory.GetLevelInfoAsync();
        levelInfo.QuantityAvailable.Should().Be(45m);
    }

    [Fact]
    public async Task Given_MultipleWasteEvents_When_Queried_Then_TotalWasteReflectedInStock()
    {
        // Given: 100 kg of produce
        var (inventory, _) = await GivenStockAtLocation(
            ingredientName: "Romaine Lettuce",
            quantity: 100m,
            unit: "kg");

        // When: Multiple waste events occur
        await inventory.RecordWasteAsync(
            new RecordWasteCommand(3m, "Damaged in storage", "Damage", Guid.NewGuid()));
        await inventory.RecordWasteAsync(
            new RecordWasteCommand(7m, "Expired", "Spoilage", Guid.NewGuid()));
        await inventory.RecordWasteAsync(
            new RecordWasteCommand(2m, "Prep waste", "Production", Guid.NewGuid()));

        // Then: Stock reflects total waste of 12 kg
        var levelInfo = await inventory.GetLevelInfoAsync();
        levelInfo.QuantityAvailable.Should().Be(88m); // 100 - 3 - 7 - 2
    }

    // ============================================================================
    // Feature: Physical Count Adjustments
    // As an inventory auditor
    // I want to adjust stock based on physical counts
    // So that system matches actual inventory
    // ============================================================================

    [Fact]
    public async Task Given_StockAtLocation_When_PhysicalCountLower_Then_StockAdjustedDown()
    {
        // Given: System shows 100 bottles of vodka
        var (inventory, _) = await GivenStockAtLocation(
            ingredientName: "Premium Vodka",
            quantity: 100m,
            unit: "bottles");

        // When: Physical count reveals only 92 bottles
        await inventory.AdjustQuantityAsync(
            new AdjustQuantityCommand(92m, "Monthly physical count - variance", Guid.NewGuid()));

        // Then: Stock adjusted to match physical count
        var levelInfo = await inventory.GetLevelInfoAsync();
        levelInfo.QuantityOnHand.Should().Be(92m);
    }

    [Fact]
    public async Task Given_StockAtLocation_When_PhysicalCountHigher_Then_StockAdjustedUp()
    {
        // Given: System shows 50 kg of sugar
        var (inventory, _) = await GivenStockAtLocation(
            ingredientName: "Granulated Sugar",
            quantity: 50m,
            unit: "kg");

        // When: Physical count reveals 55 kg (found unrecorded delivery)
        await inventory.AdjustQuantityAsync(
            new AdjustQuantityCommand(55m, "Found extra stock - unrecorded receipt", Guid.NewGuid()));

        // Then: Stock adjusted to match physical count
        var levelInfo = await inventory.GetLevelInfoAsync();
        levelInfo.QuantityOnHand.Should().Be(55m);
    }

    // ============================================================================
    // Feature: Expired Batch Write-off
    // As a food safety manager
    // I want expired batches automatically removed
    // So that we never serve expired ingredients
    // ============================================================================

    [Fact]
    public async Task Given_BatchesWithDifferentExpiryDates_When_WriteOffExpired_Then_OnlyExpiredRemoved()
    {
        // Given: Mixed batches of dairy - some expired, some fresh
        var context = CreateTestContext();
        var inventory = await CreateInventoryGrain(context, "Heavy Cream", "liters");

        // Expired batch (yesterday)
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand(
            "CREAM-EXPIRED", 20m, 3.00m, DateTime.UtcNow.AddDays(-1)));

        // Valid batch (expires in 2 weeks)
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand(
            "CREAM-FRESH", 30m, 3.50m, DateTime.UtcNow.AddDays(14)));

        // Another expired batch (3 days ago)
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand(
            "CREAM-OLD", 15m, 2.50m, DateTime.UtcNow.AddDays(-3)));

        var levelBefore = await inventory.GetLevelInfoAsync();
        levelBefore.QuantityAvailable.Should().Be(65m); // 20 + 30 + 15

        // When: Write-off expired batches
        await inventory.WriteOffExpiredBatchesAsync(Guid.NewGuid());

        // Then: Only fresh batch remains
        var levelAfter = await inventory.GetLevelInfoAsync();
        levelAfter.QuantityAvailable.Should().Be(30m);

        var activeBatches = await inventory.GetActiveBatchesAsync();
        activeBatches.Should().ContainSingle()
            .Which.BatchNumber.Should().Be("CREAM-FRESH");
    }

    // ============================================================================
    // Feature: Combined Operations Scenario
    // Real-world scenario with multiple operations
    // ============================================================================

    [Fact]
    public async Task Given_ComplexInventoryScenario_When_MultipleOperationsOccur_Then_StockTrackedAccurately()
    {
        // Given: A busy restaurant day starting with 100 kg of beef
        var context = CreateTestContext();
        var inventory = await CreateInventoryGrain(context, "Prime Beef", "kg");

        // Morning delivery
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand(
            "BEEF-AM", 60m, 15.00m, DateTime.UtcNow.AddDays(5)));
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand(
            "BEEF-PM", 40m, 16.00m, DateTime.UtcNow.AddDays(7)));

        // When: Day's operations occur

        // Lunch service sales
        var lunchOrder1 = Guid.NewGuid();
        await inventory.ConsumeForOrderAsync(lunchOrder1, 8m, Guid.NewGuid());
        await inventory.ConsumeForOrderAsync(Guid.NewGuid(), 12m, Guid.NewGuid());

        // One lunch order voided
        var state = await inventory.GetStateAsync();
        var lunchMovement = state.RecentMovements.First(m => m.ReferenceId == lunchOrder1);
        await inventory.ReverseConsumptionAsync(lunchMovement.Id, "Customer complaint", Guid.NewGuid());

        // Prep waste
        await inventory.RecordWasteAsync(
            new RecordWasteCommand(3m, "Trim waste from butchering", "Production", Guid.NewGuid()));

        // Dinner service sales
        await inventory.ConsumeForOrderAsync(Guid.NewGuid(), 15m, Guid.NewGuid());
        await inventory.ConsumeForOrderAsync(Guid.NewGuid(), 10m, Guid.NewGuid());

        // Transfer 5 kg to sister restaurant
        await inventory.TransferOutAsync(
            new TransferOutCommand(5m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        // Then: Final stock should be:
        // 100 (initial) - 8 (lunch1) - 12 (lunch2) + 8 (void) - 3 (waste) - 15 (dinner1) - 10 (dinner2) - 5 (transfer)
        // = 100 - 8 - 12 + 8 - 3 - 15 - 10 - 5 = 55
        var finalLevel = await inventory.GetLevelInfoAsync();
        finalLevel.QuantityAvailable.Should().Be(55m);
    }

    // ============================================================================
    // Helper Methods - BDD-style Given/When builders
    // ============================================================================

    private record TestContext(Guid OrgId, Guid SiteId, Guid IngredientId, Guid OrderId);

    private TestContext CreateTestContext() => new(
        OrgId: Guid.NewGuid(),
        SiteId: Guid.NewGuid(),
        IngredientId: Guid.NewGuid(),
        OrderId: Guid.NewGuid());

    private async Task<(IInventoryGrain Inventory, TestContext Context)> GivenStockAtLocation(
        string ingredientName,
        decimal quantity,
        string unit,
        decimal reorderPoint = 10m,
        decimal parLevel = 100m)
    {
        var context = CreateTestContext();
        var inventory = await CreateInventoryGrain(context, ingredientName, unit, reorderPoint, parLevel);

        // Receive initial batch
        await inventory.ReceiveBatchAsync(new ReceiveBatchCommand(
            $"INIT-{Guid.NewGuid().ToString()[..8]}",
            quantity,
            2.50m));

        return (inventory, context);
    }

    private async Task<IInventoryGrain> CreateInventoryGrain(
        TestContext context,
        string ingredientName,
        string unit,
        decimal reorderPoint = 10m,
        decimal parLevel = 100m)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IInventoryGrain>(
            GrainKeys.Inventory(context.OrgId, context.SiteId, context.IngredientId));

        await grain.InitializeAsync(new InitializeInventoryCommand(
            context.OrgId,
            context.SiteId,
            context.IngredientId,
            ingredientName,
            $"SKU-{context.IngredientId.ToString()[..8]}",
            unit,
            "Test Category",
            reorderPoint,
            parLevel));

        return grain;
    }

    private static async Task WhenSaleConsumesStock(
        IInventoryGrain inventory,
        decimal quantity,
        Guid orderId)
    {
        await inventory.ConsumeForOrderAsync(orderId, quantity, Guid.NewGuid());
    }
}
