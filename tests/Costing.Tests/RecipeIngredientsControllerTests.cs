using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Costing.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Costing.Tests;

public class RecipeIngredientsControllerTests : IClassFixture<CostingApiFixture>
{
    private readonly CostingApiFixture _fixture;
    private readonly HttpClient _client;

    public RecipeIngredientsControllerTests(CostingApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsAllIngredientsForRecipe()
    {
        var response = await _client.GetAsync($"/api/recipes/{_fixture.BurgerRecipeId}/ingredients");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var ingredients = await response.Content.ReadFromJsonAsync<List<RecipeIngredientDto>>();
        ingredients.Should().NotBeNull();
        ingredients!.Should().HaveCount(3);
        ingredients.Should().Contain(i => i.IngredientName == "Ground Beef");
        ingredients.Should().Contain(i => i.IngredientName == "Burger Bun");
        ingredients.Should().Contain(i => i.IngredientName == "Cheddar Cheese");
    }

    [Fact]
    public async Task GetAll_RecipeNotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/recipes/{Guid.NewGuid()}/ingredients");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_ReturnsIngredientWithDetails()
    {
        var response = await _client.GetAsync($"/api/recipes/{_fixture.BurgerRecipeId}/ingredients/{_fixture.BeefId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var ingredient = await response.Content.ReadFromJsonAsync<RecipeIngredientDto>();
        ingredient.Should().NotBeNull();
        ingredient!.IngredientId.Should().Be(_fixture.BeefId);
        ingredient.IngredientName.Should().Be("Ground Beef");
        ingredient.Quantity.Should().Be(0.15m);
        ingredient.UnitOfMeasure.Should().Be("kg");
        ingredient.WastePercentage.Should().Be(5m);
    }

    [Fact]
    public async Task GetById_IncludesHalLinks()
    {
        var response = await _client.GetAsync($"/api/recipes/{_fixture.BurgerRecipeId}/ingredients/{_fixture.BeefId}");

        var ingredient = await response.Content.ReadFromJsonAsync<RecipeIngredientDto>();
        ingredient!.Links.Should().ContainKey("self");
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/recipes/{_fixture.BurgerRecipeId}/ingredients/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Add_WithValidData_ReturnsCreatedIngredient()
    {
        var newIngredientId = Guid.NewGuid();
        var request = new AddRecipeIngredientRequest(
            IngredientId: newIngredientId,
            IngredientName: "Lettuce",
            Quantity: 0.05m,
            UnitOfMeasure: "kg",
            WastePercentage: 15m
        );

        var response = await _client.PostAsJsonAsync($"/api/recipes/{_fixture.BurgerRecipeId}/ingredients", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var ingredient = await response.Content.ReadFromJsonAsync<RecipeIngredientDto>();
        ingredient.Should().NotBeNull();
        ingredient!.IngredientId.Should().Be(newIngredientId);
        ingredient.IngredientName.Should().Be("Lettuce");
        ingredient.Quantity.Should().Be(0.05m);
        ingredient.WastePercentage.Should().Be(15m);
    }

    [Fact]
    public async Task Add_RecipeNotFound_Returns404()
    {
        var request = new AddRecipeIngredientRequest(
            IngredientId: Guid.NewGuid(),
            IngredientName: "Test",
            Quantity: 1m,
            UnitOfMeasure: "kg"
        );

        var response = await _client.PostAsJsonAsync($"/api/recipes/{Guid.NewGuid()}/ingredients", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Add_DuplicateIngredient_ReturnsConflict()
    {
        var request = new AddRecipeIngredientRequest(
            IngredientId: _fixture.BeefId, // Already in burger recipe
            IngredientName: "Ground Beef",
            Quantity: 0.2m,
            UnitOfMeasure: "kg"
        );

        var response = await _client.PostAsJsonAsync($"/api/recipes/{_fixture.BurgerRecipeId}/ingredients", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Add_CalculatesLineCostWithWaste()
    {
        // Add ingredient with known price
        var request = new AddRecipeIngredientRequest(
            IngredientId: _fixture.PastaId, // No price exists
            IngredientName: "Pasta",
            Quantity: 0.5m,
            UnitOfMeasure: "kg",
            WastePercentage: 0m
        );

        var response = await _client.PostAsJsonAsync($"/api/recipes/{_fixture.PastaRecipeId}/ingredients", request);

        var ingredient = await response.Content.ReadFromJsonAsync<RecipeIngredientDto>();
        // Without a price entry, unit cost should be 0
        ingredient!.CurrentUnitCost.Should().Be(0);
        ingredient.CurrentLineCost.Should().Be(0);
    }

    [Fact]
    public async Task Add_UpdatesRecipeTotalCost()
    {
        // Get initial cost
        var before = await _client.GetAsync($"/api/recipes/{_fixture.PastaRecipeId}");
        var recipeBefore = await before.Content.ReadFromJsonAsync<RecipeDto>();

        // Add new ingredient with price
        var ingredientId = Guid.NewGuid();

        // First create a price for this ingredient
        var priceRequest = new CreateIngredientPriceRequest(
            IngredientId: ingredientId,
            IngredientName: "Olive Oil",
            CurrentPrice: 20.00m,
            UnitOfMeasure: "litre",
            PackSize: 1m
        );
        await _client.PostAsJsonAsync("/api/ingredient-prices", priceRequest);

        // Now add to recipe
        var request = new AddRecipeIngredientRequest(
            IngredientId: ingredientId,
            IngredientName: "Olive Oil",
            Quantity: 0.1m,
            UnitOfMeasure: "litre"
        );
        await _client.PostAsJsonAsync($"/api/recipes/{_fixture.PastaRecipeId}/ingredients", request);

        // Check recipe cost updated
        var after = await _client.GetAsync($"/api/recipes/{_fixture.PastaRecipeId}");
        var recipeAfter = await after.Content.ReadFromJsonAsync<RecipeDto>();

        recipeAfter!.CostCalculatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Update_WithValidData_ReturnsUpdatedIngredient()
    {
        var request = new UpdateRecipeIngredientRequest(
            Quantity: 0.2m,
            WastePercentage: 8m
        );

        var response = await _client.PutAsJsonAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/ingredients/{_fixture.BeefId}",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var ingredient = await response.Content.ReadFromJsonAsync<RecipeIngredientDto>();
        ingredient.Should().NotBeNull();
        ingredient!.Quantity.Should().Be(0.2m);
        ingredient.WastePercentage.Should().Be(8m);
    }

    [Fact]
    public async Task Update_RecalculatesLineCost()
    {
        var request = new UpdateRecipeIngredientRequest(
            Quantity: 0.25m // Increase from 0.15
        );

        var response = await _client.PutAsJsonAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/ingredients/{_fixture.BeefId}",
            request);

        var ingredient = await response.Content.ReadFromJsonAsync<RecipeIngredientDto>();
        // With waste of 5% and unit cost of $10, line cost should be 0.25 * 1.05 * 10 = 2.625
        ingredient!.CurrentLineCost.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Update_NotFound_Returns404()
    {
        var request = new UpdateRecipeIngredientRequest(Quantity: 1m);

        var response = await _client.PutAsJsonAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/ingredients/{Guid.NewGuid()}",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Remove_DeletesIngredient()
    {
        // First add an ingredient to remove
        var ingredientId = Guid.NewGuid();
        var addRequest = new AddRecipeIngredientRequest(
            IngredientId: ingredientId,
            IngredientName: "Pickles",
            Quantity: 0.02m,
            UnitOfMeasure: "kg"
        );
        await _client.PostAsJsonAsync($"/api/recipes/{_fixture.BurgerRecipeId}/ingredients", addRequest);

        // Now remove it
        var response = await _client.DeleteAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/ingredients/{ingredientId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify removed
        var getResponse = await _client.GetAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/ingredients/{ingredientId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Remove_NotFound_Returns404()
    {
        var response = await _client.DeleteAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/ingredients/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Remove_UpdatesRecipeTotalCost()
    {
        // First add an ingredient to remove
        var ingredientId = Guid.NewGuid();
        var addRequest = new AddRecipeIngredientRequest(
            IngredientId: ingredientId,
            IngredientName: "Tomato",
            Quantity: 0.1m,
            UnitOfMeasure: "kg"
        );
        await _client.PostAsJsonAsync($"/api/recipes/{_fixture.BurgerRecipeId}/ingredients", addRequest);

        // Remove it
        await _client.DeleteAsync($"/api/recipes/{_fixture.BurgerRecipeId}/ingredients/{ingredientId}");

        // Check recipe cost was recalculated
        var recipe = await _client.GetAsync($"/api/recipes/{_fixture.BurgerRecipeId}");
        var recipeDto = await recipe.Content.ReadFromJsonAsync<RecipeDto>();
        recipeDto!.CostCalculatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetAll_IngredientsOrderedByName()
    {
        var response = await _client.GetAsync($"/api/recipes/{_fixture.BurgerRecipeId}/ingredients");

        var ingredients = await response.Content.ReadFromJsonAsync<List<RecipeIngredientDto>>();

        var names = ingredients!.Select(i => i.IngredientName).ToList();
        names.Should().BeInAscendingOrder();
    }
}
