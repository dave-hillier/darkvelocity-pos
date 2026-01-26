using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Menu.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Menu.Tests;

public class ItemsControllerTests : IClassFixture<MenuApiFixture>
{
    private readonly MenuApiFixture _fixture;
    private readonly HttpClient _client;

    public ItemsControllerTests(MenuApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsItems()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/items");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<MenuItemDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
        collection.Embedded.Items.Should().Contain(i => i.Name == "Test Burger");
    }

    [Fact]
    public async Task GetAll_FilterByCategory_ReturnsFilteredItems()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/items?categoryId={_fixture.TestCategoryId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<MenuItemDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().AllSatisfy(i => i.CategoryId.Should().Be(_fixture.TestCategoryId));
    }

    [Fact]
    public async Task GetById_ReturnsItem()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/items/{_fixture.TestItemId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var item = await response.Content.ReadFromJsonAsync<MenuItemDto>();
        item.Should().NotBeNull();
        item!.Name.Should().Be("Test Burger");
        item.Price.Should().Be(12.50m);
        item.CategoryName.Should().Be("Main Courses");
        item.AccountingGroupName.Should().Be("Food");
        item.TaxRate.Should().Be(0.20m);
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/items/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_CreatesItem()
    {
        var request = new CreateMenuItemRequest(
            Name: "Fish & Chips",
            CategoryId: _fixture.TestCategoryId,
            AccountingGroupId: _fixture.TestAccountingGroupId,
            Price: 14.00m,
            Description: "Classic fish and chips",
            ImageUrl: null,
            Sku: "FISH-001",
            RecipeId: null,
            TrackInventory: true);

        var response = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/items", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var item = await response.Content.ReadFromJsonAsync<MenuItemDto>();
        item.Should().NotBeNull();
        item!.Name.Should().Be("Fish & Chips");
        item.Price.Should().Be(14.00m);
        item.Sku.Should().Be("FISH-001");
        item.LocationId.Should().Be(_fixture.TestLocationId);
    }

    [Fact]
    public async Task Create_InvalidCategory_ReturnsBadRequest()
    {
        var request = new CreateMenuItemRequest(
            Name: "Invalid Item",
            CategoryId: Guid.NewGuid(),
            AccountingGroupId: _fixture.TestAccountingGroupId,
            Price: 10.00m,
            Description: null,
            ImageUrl: null,
            Sku: null,
            RecipeId: null);

        var response = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/items", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_DuplicateSku_ReturnsConflict()
    {
        var request = new CreateMenuItemRequest(
            Name: "Duplicate SKU Item",
            CategoryId: _fixture.TestCategoryId,
            AccountingGroupId: _fixture.TestAccountingGroupId,
            Price: 10.00m,
            Description: null,
            ImageUrl: null,
            Sku: "BURGER-001", // Same as existing
            RecipeId: null);

        var response = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/items", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_UpdatesItem()
    {
        // First create a new item
        var createRequest = new CreateMenuItemRequest(
            Name: "To Update",
            CategoryId: _fixture.TestCategoryId,
            AccountingGroupId: _fixture.TestAccountingGroupId,
            Price: 10.00m,
            Description: null,
            ImageUrl: null,
            Sku: $"UPDATE-{Guid.NewGuid():N}",
            RecipeId: null);
        var createResponse = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/items", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MenuItemDto>();

        // Now update it
        var updateRequest = new UpdateMenuItemRequest(
            Name: "Updated Item",
            CategoryId: null,
            AccountingGroupId: null,
            Price: 15.00m,
            Description: "Updated description",
            ImageUrl: null,
            Sku: null,
            RecipeId: null,
            TrackInventory: null,
            IsActive: null);

        var response = await _client.PutAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/items/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<MenuItemDto>();
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Updated Item");
        updated.Price.Should().Be(15.00m);
    }

    [Fact]
    public async Task Delete_DeactivatesItem()
    {
        // First create a new item
        var createRequest = new CreateMenuItemRequest(
            Name: "To Delete",
            CategoryId: _fixture.TestCategoryId,
            AccountingGroupId: _fixture.TestAccountingGroupId,
            Price: 10.00m,
            Description: null,
            ImageUrl: null,
            Sku: $"DELETE-{Guid.NewGuid():N}",
            RecipeId: null);
        var createResponse = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/items", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MenuItemDto>();

        // Now delete it
        var response = await _client.DeleteAsync($"/api/locations/{_fixture.TestLocationId}/items/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's deactivated
        var getResponse = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/items/{created.Id}");
        var item = await getResponse.Content.ReadFromJsonAsync<MenuItemDto>();
        item!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetCategoryItems_ReturnsItemsInCategory()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/categories/{_fixture.TestCategoryId}/items");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<MenuItemDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().AllSatisfy(i => i.CategoryId.Should().Be(_fixture.TestCategoryId));
    }
}
