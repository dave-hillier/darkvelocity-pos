using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Inventory.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Inventory.Tests;

public class StockControllerTests : IClassFixture<InventoryApiFixture>
{
    private readonly InventoryApiFixture _fixture;
    private readonly HttpClient _client;

    public StockControllerTests(InventoryApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetStockLevels_ReturnsStockForLocation()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/stock");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<StockLevelDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetStockLevels_IncludesCorrectTotals()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/stock");

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<StockLevelDto>>();
        var beefStock = collection!.Embedded.Items.FirstOrDefault(s => s.IngredientCode == "BEEF-MINCE");

        beefStock.Should().NotBeNull();
        beefStock!.TotalStock.Should().Be(10m); // 5 + 5 from two batches
        beefStock.ActiveBatchCount.Should().Be(2);
    }

    [Fact]
    public async Task GetLowStock_ReturnsOnlyLowStockItems()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/stock/low-stock");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<StockLevelDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().OnlyContain(s => s.IsLowStock);
    }

    [Fact]
    public async Task GetBatches_ReturnsBatchesForIngredient()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/{_fixture.TestIngredientId}/batches");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<StockBatchDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetBatches_ReturnsInFifoOrder()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/{_fixture.TestIngredientId}/batches");

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<StockBatchDto>>();
        var batches = collection!.Embedded.Items.ToList();

        // First batch should be older (cheaper at 4.00)
        batches[0].UnitCost.Should().Be(4.00m);
        batches[1].UnitCost.Should().Be(6.00m);
    }

    [Fact]
    public async Task CreateBatch_CreatesStockBatch()
    {
        // Create a new ingredient first
        var ingredientRequest = new CreateIngredientRequest(
            Code: $"NEW-STOCK-{Guid.NewGuid():N}",
            Name: "New Stock Test",
            UnitOfMeasure: "unit");
        var ingredientResponse = await _client.PostAsJsonAsync("/api/ingredients", ingredientRequest);
        var ingredient = await ingredientResponse.Content.ReadFromJsonAsync<IngredientDto>();

        // Create a stock batch
        var request = new CreateStockBatchRequest(
            IngredientId: ingredient!.Id,
            Quantity: 20m,
            UnitCost: 5.50m,
            BatchNumber: "BATCH-001",
            ExpiryDate: DateTime.UtcNow.AddDays(30));

        var response = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/stock/batches", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var batch = await response.Content.ReadFromJsonAsync<StockBatchDto>();
        batch.Should().NotBeNull();
        batch!.InitialQuantity.Should().Be(20m);
        batch.RemainingQuantity.Should().Be(20m);
        batch.UnitCost.Should().Be(5.50m);
        batch.BatchNumber.Should().Be("BATCH-001");
        batch.Status.Should().Be("active");
    }

    [Fact]
    public async Task CreateBatch_InvalidIngredient_ReturnsBadRequest()
    {
        var request = new CreateStockBatchRequest(
            IngredientId: Guid.NewGuid(), // Non-existent
            Quantity: 10m,
            UnitCost: 5.00m);

        var response = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/stock/batches", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConsumeStock_ConsumesFromOldestBatchFirst_FIFO()
    {
        // Create a new ingredient with batches
        var ingredientRequest = new CreateIngredientRequest(
            Code: $"FIFO-TEST-{Guid.NewGuid():N}",
            Name: "FIFO Test",
            UnitOfMeasure: "kg");
        var ingredientResponse = await _client.PostAsJsonAsync("/api/ingredients", ingredientRequest);
        var ingredient = await ingredientResponse.Content.ReadFromJsonAsync<IngredientDto>();

        // Create first batch (older, cheaper)
        var batch1Request = new CreateStockBatchRequest(
            IngredientId: ingredient!.Id,
            Quantity: 5m,
            UnitCost: 4.00m);
        await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/stock/batches", batch1Request);

        // Wait a tiny bit to ensure different timestamps
        await Task.Delay(10);

        // Create second batch (newer, more expensive)
        var batch2Request = new CreateStockBatchRequest(
            IngredientId: ingredient.Id,
            Quantity: 5m,
            UnitCost: 6.00m);
        await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/stock/batches", batch2Request);

        // Consume 3 units - should come from first batch at 4.00
        var consumeRequest = new ConsumeStockRequest(
            IngredientId: ingredient.Id,
            Quantity: 3m,
            ConsumptionType: "sale");

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/consume",
            consumeRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ConsumptionResultDto>();
        result!.TotalQuantityConsumed.Should().Be(3m);
        result.TotalCost.Should().Be(12.00m); // 3 * 4.00
        result.BatchConsumptions.Should().HaveCount(1);
        result.BatchConsumptions[0].UnitCost.Should().Be(4.00m);
    }

    [Fact]
    public async Task ConsumeStock_SpansMultipleBatches_FIFO()
    {
        // Create a new ingredient with batches
        var ingredientRequest = new CreateIngredientRequest(
            Code: $"FIFO-SPAN-{Guid.NewGuid():N}",
            Name: "FIFO Span Test",
            UnitOfMeasure: "kg");
        var ingredientResponse = await _client.PostAsJsonAsync("/api/ingredients", ingredientRequest);
        var ingredient = await ingredientResponse.Content.ReadFromJsonAsync<IngredientDto>();

        // Create first batch (older)
        var batch1Request = new CreateStockBatchRequest(
            IngredientId: ingredient!.Id,
            Quantity: 3m,
            UnitCost: 4.00m);
        await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/stock/batches", batch1Request);

        await Task.Delay(10);

        // Create second batch (newer)
        var batch2Request = new CreateStockBatchRequest(
            IngredientId: ingredient.Id,
            Quantity: 5m,
            UnitCost: 6.00m);
        await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/stock/batches", batch2Request);

        // Consume 5 units - should take 3 from first batch (4.00) and 2 from second (6.00)
        var consumeRequest = new ConsumeStockRequest(
            IngredientId: ingredient.Id,
            Quantity: 5m,
            ConsumptionType: "sale");

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/consume",
            consumeRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ConsumptionResultDto>();
        result!.TotalQuantityConsumed.Should().Be(5m);
        result.TotalCost.Should().Be(24.00m); // (3 * 4.00) + (2 * 6.00) = 12 + 12
        result.BatchConsumptions.Should().HaveCount(2);
    }

    [Fact]
    public async Task ConsumeStock_InsufficientStock_ReturnsPartialWithWarning()
    {
        // Create a new ingredient with limited stock
        var ingredientRequest = new CreateIngredientRequest(
            Code: $"INSUFFICIENT-{Guid.NewGuid():N}",
            Name: "Insufficient Test",
            UnitOfMeasure: "kg");
        var ingredientResponse = await _client.PostAsJsonAsync("/api/ingredients", ingredientRequest);
        var ingredient = await ingredientResponse.Content.ReadFromJsonAsync<IngredientDto>();

        // Create batch with only 2 units
        var batchRequest = new CreateStockBatchRequest(
            IngredientId: ingredient!.Id,
            Quantity: 2m,
            UnitCost: 5.00m);
        await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/stock/batches", batchRequest);

        // Try to consume 5 units (more than available)
        var consumeRequest = new ConsumeStockRequest(
            IngredientId: ingredient.Id,
            Quantity: 5m,
            ConsumptionType: "sale");

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/consume",
            consumeRequest);

        // Should still succeed but with warning
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseText = await response.Content.ReadAsStringAsync();
        responseText.Should().Contain("warning");
        responseText.Should().Contain("Insufficient stock");
    }
}
