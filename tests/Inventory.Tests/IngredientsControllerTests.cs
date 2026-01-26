using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Inventory.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Inventory.Tests;

public class IngredientsControllerTests : IClassFixture<InventoryApiFixture>
{
    private readonly InventoryApiFixture _fixture;
    private readonly HttpClient _client;

    public IngredientsControllerTests(InventoryApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsIngredients()
    {
        var response = await _client.GetAsync("/api/ingredients");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<IngredientDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_FiltersByCategory()
    {
        var response = await _client.GetAsync("/api/ingredients?category=proteins");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<IngredientDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
        collection.Embedded.Items.Should().OnlyContain(i => i.Category == "proteins");
    }

    [Fact]
    public async Task GetById_ReturnsIngredient()
    {
        var response = await _client.GetAsync($"/api/ingredients/{_fixture.TestIngredientId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var ingredient = await response.Content.ReadFromJsonAsync<IngredientDto>();
        ingredient.Should().NotBeNull();
        ingredient!.Id.Should().Be(_fixture.TestIngredientId);
        ingredient.Code.Should().Be("BEEF-MINCE");
        ingredient.Name.Should().Be("Beef Mince");
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/ingredients/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_CreatesIngredient()
    {
        var request = new CreateIngredientRequest(
            Code: $"CHICKEN-BREAST-{Guid.NewGuid():N}",
            Name: "Chicken Breast",
            UnitOfMeasure: "kg",
            Category: "proteins",
            StorageType: "chilled",
            ReorderLevel: 3m,
            ReorderQuantity: 10m);

        var response = await _client.PostAsJsonAsync("/api/ingredients", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var ingredient = await response.Content.ReadFromJsonAsync<IngredientDto>();
        ingredient.Should().NotBeNull();
        ingredient!.Name.Should().Be("Chicken Breast");
        ingredient.UnitOfMeasure.Should().Be("kg");
        ingredient.Category.Should().Be("proteins");
        ingredient.ReorderLevel.Should().Be(3m);
        ingredient.IsActive.Should().BeTrue();
        ingredient.CurrentStock.Should().Be(0);
    }

    [Fact]
    public async Task Create_DuplicateCode_ReturnsConflict()
    {
        var request = new CreateIngredientRequest(
            Code: "BEEF-MINCE", // Already exists
            Name: "Duplicate Beef",
            UnitOfMeasure: "kg");

        var response = await _client.PostAsJsonAsync("/api/ingredients", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_UpdatesIngredient()
    {
        // First create an ingredient
        var createRequest = new CreateIngredientRequest(
            Code: $"UPDATE-TEST-{Guid.NewGuid():N}",
            Name: "Update Test",
            UnitOfMeasure: "unit");

        var createResponse = await _client.PostAsJsonAsync("/api/ingredients", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<IngredientDto>();

        // Update it
        var updateRequest = new UpdateIngredientRequest(
            Name: "Updated Name",
            Category: "dairy",
            ReorderLevel: 10m);

        var response = await _client.PutAsJsonAsync($"/api/ingredients/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<IngredientDto>();
        updated!.Name.Should().Be("Updated Name");
        updated.Category.Should().Be("dairy");
        updated.ReorderLevel.Should().Be(10m);
    }

    [Fact]
    public async Task Delete_DeactivatesIngredient()
    {
        // First create an ingredient
        var createRequest = new CreateIngredientRequest(
            Code: $"DELETE-TEST-{Guid.NewGuid():N}",
            Name: "Delete Test",
            UnitOfMeasure: "unit");

        var createResponse = await _client.PostAsJsonAsync("/api/ingredients", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<IngredientDto>();

        // Delete (soft delete)
        var response = await _client.DeleteAsync($"/api/ingredients/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's deactivated
        var getResponse = await _client.GetAsync($"/api/ingredients/{created.Id}");
        var deactivated = await getResponse.Content.ReadFromJsonAsync<IngredientDto>();
        deactivated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetById_IncludesLowStockFlag()
    {
        // The test ingredient has CurrentStock = 10 and ReorderLevel = 5
        var response = await _client.GetAsync($"/api/ingredients/{_fixture.TestIngredientId}");

        var ingredient = await response.Content.ReadFromJsonAsync<IngredientDto>();
        ingredient!.IsLowStock.Should().BeFalse();
    }
}
