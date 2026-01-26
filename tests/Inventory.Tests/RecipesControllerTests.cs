using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Inventory.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Inventory.Tests;

public class RecipesControllerTests : IClassFixture<InventoryApiFixture>
{
    private readonly InventoryApiFixture _fixture;
    private readonly HttpClient _client;

    public RecipesControllerTests(InventoryApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsRecipes()
    {
        var response = await _client.GetAsync("/api/recipes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<RecipeDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetById_ReturnsRecipe()
    {
        var response = await _client.GetAsync($"/api/recipes/{_fixture.TestRecipeId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var recipe = await response.Content.ReadFromJsonAsync<RecipeDto>();
        recipe.Should().NotBeNull();
        recipe!.Id.Should().Be(_fixture.TestRecipeId);
        recipe.Code.Should().Be("BURGER-CLASSIC");
        recipe.Name.Should().Be("Classic Burger");
    }

    [Fact]
    public async Task GetById_IncludesIngredients()
    {
        var response = await _client.GetAsync($"/api/recipes/{_fixture.TestRecipeId}");

        var recipe = await response.Content.ReadFromJsonAsync<RecipeDto>();
        recipe!.Ingredients.Should().NotBeEmpty();
        recipe.Ingredients.Should().Contain(i => i.IngredientId == _fixture.TestIngredientId);
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/recipes/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_CreatesRecipe()
    {
        var request = new CreateRecipeRequest(
            Code: $"PIZZA-MARG-{Guid.NewGuid():N}",
            Name: "Margherita Pizza",
            PortionYield: 2,
            Instructions: "Stretch dough, add toppings, bake");

        var response = await _client.PostAsJsonAsync("/api/recipes", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var recipe = await response.Content.ReadFromJsonAsync<RecipeDto>();
        recipe.Should().NotBeNull();
        recipe!.Name.Should().Be("Margherita Pizza");
        recipe.PortionYield.Should().Be(2);
        recipe.Instructions.Should().Be("Stretch dough, add toppings, bake");
        recipe.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Create_DuplicateCode_ReturnsConflict()
    {
        var request = new CreateRecipeRequest(
            Code: "BURGER-CLASSIC", // Already exists
            Name: "Duplicate Recipe");

        var response = await _client.PostAsJsonAsync("/api/recipes", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_UpdatesRecipe()
    {
        // First create a recipe
        var createRequest = new CreateRecipeRequest(
            Code: $"UPDATE-RECIPE-{Guid.NewGuid():N}",
            Name: "Update Test",
            PortionYield: 1);

        var createResponse = await _client.PostAsJsonAsync("/api/recipes", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<RecipeDto>();

        // Update it
        var updateRequest = new UpdateRecipeRequest(
            Name: "Updated Recipe Name",
            PortionYield: 4);

        var response = await _client.PutAsJsonAsync($"/api/recipes/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<RecipeDto>();
        updated!.Name.Should().Be("Updated Recipe Name");
        updated.PortionYield.Should().Be(4);
    }

    [Fact]
    public async Task AddIngredient_AddsIngredientToRecipe()
    {
        // First create a recipe
        var createRequest = new CreateRecipeRequest(
            Code: $"ADD-ING-{Guid.NewGuid():N}",
            Name: "Add Ingredient Test");

        var createResponse = await _client.PostAsJsonAsync("/api/recipes", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<RecipeDto>();

        // Add an ingredient
        var addRequest = new AddRecipeIngredientRequest(
            IngredientId: _fixture.TestIngredientId,
            Quantity: 0.25m,
            WastePercentage: 10m);

        var response = await _client.PostAsJsonAsync($"/api/recipes/{created!.Id}/ingredients", addRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var recipeIngredient = await response.Content.ReadFromJsonAsync<RecipeIngredientDto>();
        recipeIngredient!.IngredientId.Should().Be(_fixture.TestIngredientId);
        recipeIngredient.Quantity.Should().Be(0.25m);
        recipeIngredient.WastePercentage.Should().Be(10m);
        recipeIngredient.EffectiveQuantity.Should().Be(0.275m); // 0.25 * 1.10
    }

    [Fact]
    public async Task AddIngredient_InvalidIngredient_ReturnsBadRequest()
    {
        var addRequest = new AddRecipeIngredientRequest(
            IngredientId: Guid.NewGuid(), // Non-existent
            Quantity: 0.5m);

        var response = await _client.PostAsJsonAsync($"/api/recipes/{_fixture.TestRecipeId}/ingredients", addRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RemoveIngredient_RemovesIngredientFromRecipe()
    {
        // First create a recipe with an ingredient
        var createRequest = new CreateRecipeRequest(
            Code: $"REM-ING-{Guid.NewGuid():N}",
            Name: "Remove Ingredient Test");

        var createResponse = await _client.PostAsJsonAsync("/api/recipes", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<RecipeDto>();

        var addRequest = new AddRecipeIngredientRequest(
            IngredientId: _fixture.TestIngredientId,
            Quantity: 0.5m);

        var addResponse = await _client.PostAsJsonAsync($"/api/recipes/{created!.Id}/ingredients", addRequest);
        var recipeIngredient = await addResponse.Content.ReadFromJsonAsync<RecipeIngredientDto>();

        // Remove the ingredient
        var response = await _client.DeleteAsync($"/api/recipes/{created.Id}/ingredients/{recipeIngredient!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's removed
        var getResponse = await _client.GetAsync($"/api/recipes/{created.Id}");
        var recipe = await getResponse.Content.ReadFromJsonAsync<RecipeDto>();
        recipe!.Ingredients.Should().BeEmpty();
    }

    [Fact]
    public async Task CalculateCost_CalculatesRecipeCost()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/recipes/{_fixture.TestRecipeId}/calculate-cost?locationId={_fixture.TestLocationId}",
            new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var recipe = await response.Content.ReadFromJsonAsync<RecipeDto>();
        recipe!.CalculatedCost.Should().NotBeNull();
        recipe.CalculatedCost.Should().BeGreaterThan(0);
        recipe.CostCalculatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_DeactivatesRecipe()
    {
        // First create a recipe
        var createRequest = new CreateRecipeRequest(
            Code: $"DELETE-RECIPE-{Guid.NewGuid():N}",
            Name: "Delete Test");

        var createResponse = await _client.PostAsJsonAsync("/api/recipes", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<RecipeDto>();

        // Delete (soft delete)
        var response = await _client.DeleteAsync($"/api/recipes/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's deactivated
        var getResponse = await _client.GetAsync($"/api/recipes/{created.Id}");
        var deactivated = await getResponse.Content.ReadFromJsonAsync<RecipeDto>();
        deactivated!.IsActive.Should().BeFalse();
    }
}
