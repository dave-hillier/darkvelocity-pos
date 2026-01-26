using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Menu.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Menu.Tests;

public class CategoriesControllerTests : IClassFixture<MenuApiFixture>
{
    private readonly MenuApiFixture _fixture;
    private readonly HttpClient _client;

    public CategoriesControllerTests(MenuApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsCategories()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<CategoryDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
        collection.Embedded.Items.Should().Contain(c => c.Name == "Main Courses");
    }

    [Fact]
    public async Task GetById_ReturnsCategory()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/categories/{_fixture.TestCategoryId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var category = await response.Content.ReadFromJsonAsync<CategoryDto>();
        category.Should().NotBeNull();
        category!.Name.Should().Be("Main Courses");
        category.DisplayOrder.Should().Be(1);
    }

    [Fact]
    public async Task GetById_DifferentLocation_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/locations/{Guid.NewGuid()}/categories/{_fixture.TestCategoryId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_CreatesCategory()
    {
        var request = new CreateCategoryRequest(
            Name: "Desserts",
            Description: "Sweet dishes",
            DisplayOrder: 2,
            Color: "#00FF00");

        var response = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/categories", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var category = await response.Content.ReadFromJsonAsync<CategoryDto>();
        category.Should().NotBeNull();
        category!.Name.Should().Be("Desserts");
        category.DisplayOrder.Should().Be(2);
        category.LocationId.Should().Be(_fixture.TestLocationId);
    }

    [Fact]
    public async Task Update_UpdatesCategory()
    {
        // First create a new category
        var createRequest = new CreateCategoryRequest("Starters", null, 3, null);
        var createResponse = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/categories", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CategoryDto>();

        // Now update it
        var updateRequest = new UpdateCategoryRequest(
            Name: "Appetizers",
            Description: "Opening dishes",
            DisplayOrder: 0,
            Color: "#0000FF",
            IsActive: null);

        var response = await _client.PutAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/categories/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<CategoryDto>();
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Appetizers");
        updated.DisplayOrder.Should().Be(0);
    }

    [Fact]
    public async Task Delete_DeactivatesCategory()
    {
        // First create a new category
        var createRequest = new CreateCategoryRequest("To Delete", null, 99, null);
        var createResponse = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/categories", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CategoryDto>();

        // Now delete it
        var response = await _client.DeleteAsync($"/api/locations/{_fixture.TestLocationId}/categories/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's deactivated
        var getResponse = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/categories/{created.Id}");
        var category = await getResponse.Content.ReadFromJsonAsync<CategoryDto>();
        category!.IsActive.Should().BeFalse();
    }
}
