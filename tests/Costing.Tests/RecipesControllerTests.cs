using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Costing.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Costing.Tests;

public class RecipesControllerTests : IClassFixture<CostingApiFixture>
{
    private readonly CostingApiFixture _fixture;
    private readonly HttpClient _client;

    public RecipesControllerTests(CostingApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsAllActiveRecipes()
    {
        var response = await _client.GetAsync("/api/recipes?isActive=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var recipes = await response.Content.ReadFromJsonAsync<List<RecipeSummaryDto>>();
        recipes.Should().NotBeNull();
        recipes!.Should().HaveCountGreaterThanOrEqualTo(2);
        recipes!.Should().OnlyContain(r => r.IsActive);
    }

    [Fact]
    public async Task GetAll_WithCategoryFilter_ReturnsFilteredRecipes()
    {
        // Get a recipe to find its category
        var recipeResponse = await _client.GetAsync($"/api/recipes/{_fixture.BurgerRecipeId}");
        var recipe = await recipeResponse.Content.ReadFromJsonAsync<RecipeDto>();

        var response = await _client.GetAsync($"/api/recipes?categoryId={recipe!.CategoryId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var recipes = await response.Content.ReadFromJsonAsync<List<RecipeSummaryDto>>();
        recipes.Should().NotBeNull();
        recipes!.Should().Contain(r => r.Id == _fixture.BurgerRecipeId);
    }

    [Fact]
    public async Task GetAll_IncludesInactiveWhenNotFiltered()
    {
        var response = await _client.GetAsync("/api/recipes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var recipes = await response.Content.ReadFromJsonAsync<List<RecipeSummaryDto>>();
        recipes.Should().NotBeNull();
        recipes!.Should().Contain(r => !r.IsActive);
    }

    [Fact]
    public async Task GetById_ReturnsRecipeWithIngredients()
    {
        var response = await _client.GetAsync($"/api/recipes/{_fixture.BurgerRecipeId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var recipe = await response.Content.ReadFromJsonAsync<RecipeDto>();
        recipe.Should().NotBeNull();
        recipe!.Id.Should().Be(_fixture.BurgerRecipeId);
        recipe.MenuItemName.Should().Be("Classic Cheeseburger");
        recipe.Code.Should().Be("RCP-BURGER-001");
        recipe.PortionYield.Should().Be(1);
        recipe.Ingredients.Should().HaveCount(3);
        recipe.Ingredients.Should().Contain(i => i.IngredientName == "Ground Beef");
    }

    [Fact]
    public async Task GetById_IncludesHalLinks()
    {
        var response = await _client.GetAsync($"/api/recipes/{_fixture.BurgerRecipeId}");

        var recipe = await response.Content.ReadFromJsonAsync<RecipeDto>();
        recipe!.Links.Should().ContainKey("self");
        recipe.Links.Should().ContainKey("cost");
        recipe.Links.Should().ContainKey("snapshots");
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/recipes/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByMenuItem_ReturnsRecipeForMenuItem()
    {
        var response = await _client.GetAsync($"/api/recipes/by-menu-item/{_fixture.BurgerMenuItemId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var recipe = await response.Content.ReadFromJsonAsync<RecipeDto>();
        recipe.Should().NotBeNull();
        recipe!.MenuItemId.Should().Be(_fixture.BurgerMenuItemId);
    }

    [Fact]
    public async Task GetByMenuItem_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/recipes/by-menu-item/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_WithValidData_ReturnsCreatedRecipe()
    {
        var menuItemId = Guid.NewGuid();
        var request = new CreateRecipeRequest(
            MenuItemId: menuItemId,
            MenuItemName: "Fish and Chips",
            Code: "RCP-FISH-001",
            CategoryName: "Mains",
            PortionYield: 1
        );

        var response = await _client.PostAsJsonAsync("/api/recipes", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var recipe = await response.Content.ReadFromJsonAsync<RecipeDto>();
        recipe.Should().NotBeNull();
        recipe!.MenuItemId.Should().Be(menuItemId);
        recipe.MenuItemName.Should().Be("Fish and Chips");
        recipe.Code.Should().Be("RCP-FISH-001");
        recipe.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Create_DuplicateMenuItem_ReturnsConflict()
    {
        var request = new CreateRecipeRequest(
            MenuItemId: _fixture.BurgerMenuItemId, // Already exists
            MenuItemName: "Another Burger",
            Code: "RCP-TEST-001"
        );

        var response = await _client.PostAsJsonAsync("/api/recipes", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_DuplicateCode_ReturnsConflict()
    {
        var request = new CreateRecipeRequest(
            MenuItemId: Guid.NewGuid(),
            MenuItemName: "New Recipe",
            Code: "RCP-BURGER-001" // Already exists
        );

        var response = await _client.PostAsJsonAsync("/api/recipes", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_WithValidData_ReturnsUpdatedRecipe()
    {
        var request = new UpdateRecipeRequest(
            Description: "Updated description",
            PortionYield: 2
        );

        var response = await _client.PutAsJsonAsync($"/api/recipes/{_fixture.PastaRecipeId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var recipe = await response.Content.ReadFromJsonAsync<RecipeDto>();
        recipe.Should().NotBeNull();
        recipe!.Description.Should().Be("Updated description");
        recipe.PortionYield.Should().Be(2);
    }

    [Fact]
    public async Task Update_NotFound_Returns404()
    {
        var request = new UpdateRecipeRequest(Description: "Test");

        var response = await _client.PutAsJsonAsync($"/api/recipes/{Guid.NewGuid()}", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_DuplicateCode_ReturnsConflict()
    {
        var request = new UpdateRecipeRequest(
            Code: "RCP-BURGER-001" // Already exists
        );

        var response = await _client.PutAsJsonAsync($"/api/recipes/{_fixture.PastaRecipeId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_SoftDeletesRecipe()
    {
        // Create a recipe to delete
        var menuItemId = Guid.NewGuid();
        var createRequest = new CreateRecipeRequest(
            MenuItemId: menuItemId,
            MenuItemName: "Recipe to Delete",
            Code: "RCP-DELETE-001"
        );
        var createResponse = await _client.PostAsJsonAsync("/api/recipes", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<RecipeDto>();

        // Delete it
        var response = await _client.DeleteAsync($"/api/recipes/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify soft deleted
        var getResponse = await _client.GetAsync($"/api/recipes/{created.Id}");
        var recipe = await getResponse.Content.ReadFromJsonAsync<RecipeDto>();
        recipe!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_NotFound_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/recipes/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CalculateCost_ReturnsCorrectCostBreakdown()
    {
        var response = await _client.GetAsync($"/api/recipes/{_fixture.BurgerRecipeId}/cost?menuPrice=12.00");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var cost = await response.Content.ReadFromJsonAsync<RecipeCostCalculationDto>();
        cost.Should().NotBeNull();
        cost!.RecipeId.Should().Be(_fixture.BurgerRecipeId);
        cost.TotalIngredientCost.Should().BeGreaterThan(0);
        cost.CostPerPortion.Should().BeGreaterThan(0);
        cost.PortionYield.Should().Be(1);
        cost.MenuPrice.Should().Be(12.00m);
        cost.CostPercentage.Should().BeGreaterThan(0);
        cost.GrossMarginPercent.Should().BeGreaterThan(0);
        cost.IngredientCosts.Should().HaveCount(3);
    }

    [Fact]
    public async Task CalculateCost_WithWastePercentage_CalculatesEffectiveQuantity()
    {
        var response = await _client.GetAsync($"/api/recipes/{_fixture.BurgerRecipeId}/cost");

        var cost = await response.Content.ReadFromJsonAsync<RecipeCostCalculationDto>();

        // Beef has 5% waste, so effective quantity should be > raw quantity
        var beefLine = cost!.IngredientCosts.First(i => i.IngredientName == "Ground Beef");
        beefLine.EffectiveQuantity.Should().BeGreaterThan(beefLine.Quantity);
        beefLine.EffectiveQuantity.Should().Be(beefLine.Quantity * 1.05m);
    }

    [Fact]
    public async Task CalculateCost_IngredientsOrderedByCost()
    {
        var response = await _client.GetAsync($"/api/recipes/{_fixture.BurgerRecipeId}/cost");

        var cost = await response.Content.ReadFromJsonAsync<RecipeCostCalculationDto>();

        var costs = cost!.IngredientCosts.Select(i => i.LineCost).ToList();
        costs.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task CalculateCost_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/recipes/{Guid.NewGuid()}/cost");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Recalculate_UpdatesRecipeCostFromCurrentPrices()
    {
        var response = await _client.PostAsync($"/api/recipes/{_fixture.BurgerRecipeId}/recalculate", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var recipe = await response.Content.ReadFromJsonAsync<RecipeDto>();
        recipe.Should().NotBeNull();
        recipe!.CurrentCostPerPortion.Should().BeGreaterThan(0);
        recipe.CostCalculatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Recalculate_NotFound_Returns404()
    {
        var response = await _client.PostAsync($"/api/recipes/{Guid.NewGuid()}/recalculate", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
