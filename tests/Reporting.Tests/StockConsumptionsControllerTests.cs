using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Reporting.Api.Controllers;
using DarkVelocity.Reporting.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Reporting.Tests;

public class StockConsumptionsControllerTests : IClassFixture<ReportingApiFixture>
{
    private readonly ReportingApiFixture _fixture;
    private readonly HttpClient _client;

    public StockConsumptionsControllerTests(ReportingApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsConsumptions()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/stock-consumptions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<StockConsumptionDto>>();
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_FilterByOrder()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock-consumptions?orderId={_fixture.TestOrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<StockConsumptionDto>>();
        result.Should().NotBeNull();
        result!.All(c => c.OrderId == _fixture.TestOrderId).Should().BeTrue();
    }

    [Fact]
    public async Task GetAll_FilterByMenuItem()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock-consumptions?menuItemId={_fixture.TestMenuItemId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<StockConsumptionDto>>();
        result.Should().NotBeNull();
        result!.All(c => c.MenuItemId == _fixture.TestMenuItemId).Should().BeTrue();
    }

    [Fact]
    public async Task GetAll_FilterByIngredient()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock-consumptions?ingredientId={_fixture.TestIngredientId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<StockConsumptionDto>>();
        result.Should().NotBeNull();
        result!.All(c => c.IngredientId == _fixture.TestIngredientId).Should().BeTrue();
    }

    [Fact]
    public async Task GetByOrder_ReturnsConsumptionsForOrder()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock-consumptions/by-order/{_fixture.TestOrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<StockConsumptionDto>>();
        result.Should().NotBeNull();
        result!.All(c => c.OrderId == _fixture.TestOrderId).Should().BeTrue();
    }

    [Fact]
    public async Task GetSummary_ReturnsSummaryData()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock-consumptions/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<StockConsumptionSummaryDto>();
        result.Should().NotBeNull();
        result!.TotalConsumptions.Should().BeGreaterThan(0);
        result.TotalCost.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetSummary_AggregatesByIngredient()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock-consumptions/summary");

        var result = await response.Content.ReadFromJsonAsync<StockConsumptionSummaryDto>();
        result.Should().NotBeNull();
        result!.ByIngredient.Should().NotBeEmpty();
        result.ByIngredient[0].IngredientName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Record_NewConsumption_ReturnsCreated()
    {
        var request = new RecordConsumptionRequest(
            OrderId: Guid.NewGuid(),
            OrderLineId: Guid.NewGuid(),
            MenuItemId: _fixture.TestMenuItemId,
            IngredientId: Guid.NewGuid(),
            IngredientName: "Lettuce",
            StockBatchId: Guid.NewGuid(),
            QuantityConsumed: 0.05m,
            UnitCost: 2.00m
        );

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock-consumptions", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<StockConsumptionDto>();
        result.Should().NotBeNull();
        result!.IngredientName.Should().Be("Lettuce");
        result.QuantityConsumed.Should().Be(0.05m);
        result.UnitCost.Should().Be(2.00m);
        result.TotalCost.Should().Be(0.10m); // 0.05 * 2.00
    }

    [Fact]
    public async Task RecordBatch_MultipleConsumptions_ReturnsAll()
    {
        var orderId = Guid.NewGuid();
        var orderLineId = Guid.NewGuid();
        var requests = new List<RecordConsumptionRequest>
        {
            new(
                OrderId: orderId,
                OrderLineId: orderLineId,
                MenuItemId: _fixture.TestMenuItemId,
                IngredientId: Guid.NewGuid(),
                IngredientName: "Tomato",
                StockBatchId: Guid.NewGuid(),
                QuantityConsumed: 0.10m,
                UnitCost: 1.50m
            ),
            new(
                OrderId: orderId,
                OrderLineId: orderLineId,
                MenuItemId: _fixture.TestMenuItemId,
                IngredientId: Guid.NewGuid(),
                IngredientName: "Onion",
                StockBatchId: Guid.NewGuid(),
                QuantityConsumed: 0.05m,
                UnitCost: 1.00m
            ),
            new(
                OrderId: orderId,
                OrderLineId: orderLineId,
                MenuItemId: _fixture.TestMenuItemId,
                IngredientId: Guid.NewGuid(),
                IngredientName: "Cheese",
                StockBatchId: Guid.NewGuid(),
                QuantityConsumed: 0.03m,
                UnitCost: 8.00m
            )
        };

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock-consumptions/batch", requests);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<StockConsumptionDto>>();
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result!.Sum(c => c.TotalCost).Should().Be(0.44m); // 0.15 + 0.05 + 0.24
    }

    [Fact]
    public async Task GetById_ExistingConsumption_ReturnsDetails()
    {
        // First record a consumption
        var request = new RecordConsumptionRequest(
            OrderId: Guid.NewGuid(),
            OrderLineId: Guid.NewGuid(),
            MenuItemId: _fixture.TestMenuItemId,
            IngredientId: Guid.NewGuid(),
            IngredientName: "Pickle",
            StockBatchId: Guid.NewGuid(),
            QuantityConsumed: 0.02m,
            UnitCost: 3.00m
        );

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock-consumptions", request);
        var created = await createResponse.Content.ReadFromJsonAsync<StockConsumptionDto>();

        // Now get it
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock-consumptions/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<StockConsumptionDto>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
        result.IngredientName.Should().Be("Pickle");
    }

    [Fact]
    public async Task GetById_NonExistingConsumption_ReturnsNotFound()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock-consumptions/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_IncludesHalLinks()
    {
        // First record a consumption
        var request = new RecordConsumptionRequest(
            OrderId: Guid.NewGuid(),
            OrderLineId: Guid.NewGuid(),
            MenuItemId: _fixture.TestMenuItemId,
            IngredientId: Guid.NewGuid(),
            IngredientName: "Bacon",
            StockBatchId: Guid.NewGuid(),
            QuantityConsumed: 0.04m,
            UnitCost: 6.00m
        );

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock-consumptions", request);
        var created = await createResponse.Content.ReadFromJsonAsync<StockConsumptionDto>();

        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock-consumptions/{created!.Id}");

        var result = await response.Content.ReadFromJsonAsync<StockConsumptionDto>();
        result.Should().NotBeNull();
        result!.Links.Should().ContainKey("self");
    }

    [Fact]
    public async Task GetSummary_CountsUniqueOrders()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock-consumptions/summary");

        var result = await response.Content.ReadFromJsonAsync<StockConsumptionSummaryDto>();
        result.Should().NotBeNull();
        result!.UniqueOrders.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetSummary_CountsUniqueIngredients()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/stock-consumptions/summary");

        var result = await response.Content.ReadFromJsonAsync<StockConsumptionSummaryDto>();
        result.Should().NotBeNull();
        result!.UniqueIngredients.Should().BeGreaterThan(0);
    }
}
