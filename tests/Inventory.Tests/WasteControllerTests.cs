using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Inventory.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Inventory.Tests;

public class WasteControllerTests : IClassFixture<InventoryApiFixture>
{
    private readonly InventoryApiFixture _fixture;
    private readonly HttpClient _client;

    public WasteControllerTests(InventoryApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsWasteRecords()
    {
        // First create a waste record
        var userId = Guid.NewGuid();
        var request = new RecordWasteRequest(
            IngredientId: _fixture.TestIngredientId,
            Quantity: 0.5m,
            Reason: "expired",
            RecordedByUserId: userId);

        await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/waste", request);

        // Then get all
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/waste");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<WasteRecordDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_FiltersByDateRange()
    {
        var from = DateTime.UtcNow.AddDays(-1);
        var to = DateTime.UtcNow.AddDays(1);

        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/waste?from={from:O}&to={to:O}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RecordWaste_CreatesWasteRecord()
    {
        var userId = Guid.NewGuid();
        var request = new RecordWasteRequest(
            IngredientId: _fixture.TestIngredientId,
            Quantity: 1.0m,
            Reason: "spoiled",
            RecordedByUserId: userId,
            Notes: "Found during morning prep");

        var response = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/waste", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var wasteRecord = await response.Content.ReadFromJsonAsync<WasteRecordDto>();
        wasteRecord.Should().NotBeNull();
        wasteRecord!.IngredientId.Should().Be(_fixture.TestIngredientId);
        wasteRecord.IngredientName.Should().Be("Beef Mince");
        wasteRecord.Quantity.Should().Be(1.0m);
        wasteRecord.Reason.Should().Be("spoiled");
        wasteRecord.Notes.Should().Be("Found during morning prep");
        wasteRecord.RecordedByUserId.Should().Be(userId);
        wasteRecord.RecordedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task RecordWaste_ConsumesStockViaFIFO()
    {
        // Create a new ingredient with known stock
        var ingredientRequest = new CreateIngredientRequest(
            Code: $"WASTE-FIFO-{Guid.NewGuid():N}",
            Name: "Waste FIFO Test",
            UnitOfMeasure: "kg");
        var ingredientResponse = await _client.PostAsJsonAsync("/api/ingredients", ingredientRequest);
        var ingredient = await ingredientResponse.Content.ReadFromJsonAsync<IngredientDto>();

        // Create stock batch
        var batchRequest = new CreateStockBatchRequest(
            IngredientId: ingredient!.Id,
            Quantity: 5m,
            UnitCost: 4.00m);
        await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/stock/batches", batchRequest);

        // Record waste
        var wasteRequest = new RecordWasteRequest(
            IngredientId: ingredient.Id,
            Quantity: 2m,
            Reason: "damaged",
            RecordedByUserId: Guid.NewGuid());

        var response = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/waste", wasteRequest);

        var wasteRecord = await response.Content.ReadFromJsonAsync<WasteRecordDto>();

        // Verify estimated cost reflects FIFO costing
        wasteRecord!.EstimatedCost.Should().Be(8.00m); // 2 * 4.00

        // Verify stock was consumed
        var stockResponse = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock/{ingredient.Id}/batches");
        var stockCollection = await stockResponse.Content.ReadFromJsonAsync<HalCollection<StockBatchDto>>();
        var batch = stockCollection!.Embedded.Items.First();
        batch.RemainingQuantity.Should().Be(3m); // 5 - 2
    }

    [Fact]
    public async Task RecordWaste_InvalidIngredient_ReturnsBadRequest()
    {
        var request = new RecordWasteRequest(
            IngredientId: Guid.NewGuid(), // Non-existent
            Quantity: 1m,
            Reason: "expired",
            RecordedByUserId: Guid.NewGuid());

        var response = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/waste", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RecordWaste_WithSpecificBatch_RecordsCorrectly()
    {
        var request = new RecordWasteRequest(
            IngredientId: _fixture.TestIngredientId,
            Quantity: 0.25m,
            Reason: "contaminated",
            RecordedByUserId: Guid.NewGuid(),
            StockBatchId: _fixture.TestStockBatchId);

        var response = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/waste", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var wasteRecord = await response.Content.ReadFromJsonAsync<WasteRecordDto>();
        wasteRecord!.StockBatchId.Should().Be(_fixture.TestStockBatchId);
    }
}
