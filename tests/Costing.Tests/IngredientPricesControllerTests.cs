using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Costing.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Costing.Tests;

public class IngredientPricesControllerTests : IClassFixture<CostingApiFixture>
{
    private readonly CostingApiFixture _fixture;
    private readonly HttpClient _client;

    public IngredientPricesControllerTests(CostingApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsAllIngredientPrices()
    {
        var response = await _client.GetAsync("/api/ingredient-prices");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var prices = await response.Content.ReadFromJsonAsync<List<IngredientPriceDto>>();
        prices.Should().NotBeNull();
        prices!.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task GetAll_WithActiveFilter_ReturnsFilteredPrices()
    {
        var response = await _client.GetAsync("/api/ingredient-prices?isActive=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var prices = await response.Content.ReadFromJsonAsync<List<IngredientPriceDto>>();
        prices.Should().NotBeNull();
        prices!.Should().OnlyContain(p => p.IsActive);
    }

    [Fact]
    public async Task GetAll_WithRecentChangeFilter_ReturnsRecentChanges()
    {
        var response = await _client.GetAsync("/api/ingredient-prices?hasRecentChange=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var prices = await response.Content.ReadFromJsonAsync<List<IngredientPriceDto>>();
        prices.Should().NotBeNull();
        // Cheese has a recent price change
        prices!.Should().Contain(p => p.IngredientName == "Cheddar Cheese");
    }

    [Fact]
    public async Task GetAll_OrderedByIngredientName()
    {
        var response = await _client.GetAsync("/api/ingredient-prices");

        var prices = await response.Content.ReadFromJsonAsync<List<IngredientPriceDto>>();

        var names = prices!.Select(p => p.IngredientName).ToList();
        names.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetByIngredient_ReturnsIngredientPrice()
    {
        var response = await _client.GetAsync($"/api/ingredient-prices/{_fixture.BeefId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var price = await response.Content.ReadFromJsonAsync<IngredientPriceDto>();
        price.Should().NotBeNull();
        price!.IngredientId.Should().Be(_fixture.BeefId);
        price.IngredientName.Should().Be("Ground Beef");
        price.CurrentPrice.Should().Be(50.00m);
        price.PackSize.Should().Be(5m);
        price.PricePerUnit.Should().Be(10.00m);
    }

    [Fact]
    public async Task GetByIngredient_IncludesHalLinks()
    {
        var response = await _client.GetAsync($"/api/ingredient-prices/{_fixture.BeefId}");

        var price = await response.Content.ReadFromJsonAsync<IngredientPriceDto>();
        price!.Links.Should().ContainKey("self");
        price.Links.Should().ContainKey("recipes");
    }

    [Fact]
    public async Task GetByIngredient_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/ingredient-prices/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_WithValidData_ReturnsCreatedPrice()
    {
        var ingredientId = Guid.NewGuid();
        var request = new CreateIngredientPriceRequest(
            IngredientId: ingredientId,
            IngredientName: "Fresh Salmon",
            CurrentPrice: 80.00m,
            UnitOfMeasure: "kg",
            PackSize: 2m,
            PreferredSupplierId: Guid.NewGuid(),
            PreferredSupplierName: "Fresh Fish Co"
        );

        var response = await _client.PostAsJsonAsync("/api/ingredient-prices", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var price = await response.Content.ReadFromJsonAsync<IngredientPriceDto>();
        price.Should().NotBeNull();
        price!.IngredientId.Should().Be(ingredientId);
        price.IngredientName.Should().Be("Fresh Salmon");
        price.CurrentPrice.Should().Be(80.00m);
        price.PricePerUnit.Should().Be(40.00m); // 80 / 2
        price.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Create_CalculatesPricePerUnit()
    {
        var ingredientId = Guid.NewGuid();
        var request = new CreateIngredientPriceRequest(
            IngredientId: ingredientId,
            IngredientName: "Rice",
            CurrentPrice: 25.00m,
            UnitOfMeasure: "kg",
            PackSize: 10m
        );

        var response = await _client.PostAsJsonAsync("/api/ingredient-prices", request);

        var price = await response.Content.ReadFromJsonAsync<IngredientPriceDto>();
        price!.PricePerUnit.Should().Be(2.50m); // 25 / 10
    }

    [Fact]
    public async Task Create_DuplicateIngredient_ReturnsConflict()
    {
        var request = new CreateIngredientPriceRequest(
            IngredientId: _fixture.BeefId, // Already exists
            IngredientName: "Ground Beef",
            CurrentPrice: 60.00m,
            UnitOfMeasure: "kg"
        );

        var response = await _client.PostAsJsonAsync("/api/ingredient-prices", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_WithValidData_ReturnsUpdatedPrice()
    {
        var request = new UpdateIngredientPriceRequest(
            CurrentPrice: 55.00m,
            PackSize: 5m
        );

        var response = await _client.PutAsJsonAsync($"/api/ingredient-prices/{_fixture.BeefId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var price = await response.Content.ReadFromJsonAsync<IngredientPriceDto>();
        price.Should().NotBeNull();
        price!.CurrentPrice.Should().Be(55.00m);
        price.PricePerUnit.Should().Be(11.00m); // 55 / 5
    }

    [Fact]
    public async Task Update_TracksPreviousPrice()
    {
        // First create a price to update
        var ingredientId = Guid.NewGuid();
        var createRequest = new CreateIngredientPriceRequest(
            IngredientId: ingredientId,
            IngredientName: "Butter",
            CurrentPrice: 10.00m,
            UnitOfMeasure: "kg",
            PackSize: 1m
        );
        await _client.PostAsJsonAsync("/api/ingredient-prices", createRequest);

        // Update with new price
        var updateRequest = new UpdateIngredientPriceRequest(
            CurrentPrice: 12.00m,
            PackSize: 1m
        );
        var response = await _client.PutAsJsonAsync($"/api/ingredient-prices/{ingredientId}", updateRequest);

        var price = await response.Content.ReadFromJsonAsync<IngredientPriceDto>();
        price!.PreviousPrice.Should().Be(10.00m);
        price.PriceChangePercent.Should().Be(20.00m); // (12-10)/10 * 100
        price.PriceChangedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Update_NotFound_Returns404()
    {
        var request = new UpdateIngredientPriceRequest(
            CurrentPrice: 10.00m,
            PackSize: 1m
        );

        var response = await _client.PutAsJsonAsync($"/api/ingredient-prices/{Guid.NewGuid()}", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAffectedRecipes_ReturnsRecipesUsingIngredient()
    {
        var response = await _client.GetAsync($"/api/ingredient-prices/{_fixture.BeefId}/recipes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var recipes = await response.Content.ReadFromJsonAsync<List<RecipeSummaryDto>>();
        recipes.Should().NotBeNull();
        recipes!.Should().Contain(r => r.Id == _fixture.BurgerRecipeId);
    }

    [Fact]
    public async Task RecalculateAffectedRecipes_UpdatesAllRecipeCosts()
    {
        var response = await _client.PostAsync(
            $"/api/ingredient-prices/{_fixture.BeefId}/recalculate-recipes",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<RecalculateResultDto>();
        result.Should().NotBeNull();
        result!.IngredientId.Should().Be(_fixture.BeefId);
        result.AffectedRecipeCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RecalculateAffectedRecipes_NotFound_Returns404()
    {
        var response = await _client.PostAsync(
            $"/api/ingredient-prices/{Guid.NewGuid()}/recalculate-recipes",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_SoftDeletesPrice()
    {
        // Create a price to delete
        var ingredientId = Guid.NewGuid();
        var createRequest = new CreateIngredientPriceRequest(
            IngredientId: ingredientId,
            IngredientName: "To Delete",
            CurrentPrice: 5.00m,
            UnitOfMeasure: "kg"
        );
        await _client.PostAsJsonAsync("/api/ingredient-prices", createRequest);

        // Delete it
        var response = await _client.DeleteAsync($"/api/ingredient-prices/{ingredientId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify soft deleted
        var getResponse = await _client.GetAsync($"/api/ingredient-prices/{ingredientId}");
        var price = await getResponse.Content.ReadFromJsonAsync<IngredientPriceDto>();
        price!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_NotFound_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/ingredient-prices/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_UpdatesRelatedRecipeIngredientCosts()
    {
        // Update beef price
        var updateRequest = new UpdateIngredientPriceRequest(
            CurrentPrice: 60.00m, // Up from 50
            PackSize: 5m
        );
        await _client.PutAsJsonAsync($"/api/ingredient-prices/{_fixture.BeefId}", updateRequest);

        // Check recipe ingredient was updated
        var response = await _client.GetAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/ingredients/{_fixture.BeefId}");

        var ingredient = await response.Content.ReadFromJsonAsync<RecipeIngredientDto>();
        ingredient!.CurrentUnitCost.Should().Be(12.00m); // 60 / 5
    }
}

// Helper DTO for recalculate response
public class RecalculateResultDto
{
    public Guid IngredientId { get; set; }
    public string? IngredientName { get; set; }
    public decimal NewPricePerUnit { get; set; }
    public int AffectedRecipeCount { get; set; }
}
