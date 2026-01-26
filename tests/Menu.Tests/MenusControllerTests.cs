using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Menu.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Menu.Tests;

public class MenusControllerTests : IClassFixture<MenuApiFixture>
{
    private readonly MenuApiFixture _fixture;
    private readonly HttpClient _client;

    public MenusControllerTests(MenuApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsMenus()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/menus");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<MenuDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
        collection.Embedded.Items.Should().Contain(m => m.Name == "Default Menu");
    }

    [Fact]
    public async Task GetById_ReturnsMenu()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/menus/{_fixture.TestMenuId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var menu = await response.Content.ReadFromJsonAsync<MenuDto>();
        menu.Should().NotBeNull();
        menu!.Name.Should().Be("Default Menu");
        // Note: IsDefault may be changed by other tests, so we don't assert on it here
    }

    [Fact]
    public async Task GetById_WithScreens_ReturnsMenuWithScreens()
    {
        // First add a screen to the menu
        var screenRequest = new CreateScreenRequest(
            Name: "Screen 1",
            Position: 1,
            Color: "#FF0000",
            Rows: 4,
            Columns: 5);
        await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/menus/{_fixture.TestMenuId}/screens", screenRequest);

        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/menus/{_fixture.TestMenuId}?includeScreens=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var menu = await response.Content.ReadFromJsonAsync<MenuDto>();
        menu.Should().NotBeNull();
        menu!.Screens.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Create_CreatesMenu()
    {
        var request = new CreateMenuRequest(
            Name: "Lunch Menu",
            Description: "Menu for lunch service",
            IsDefault: false);

        var response = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/menus", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var menu = await response.Content.ReadFromJsonAsync<MenuDto>();
        menu.Should().NotBeNull();
        menu!.Name.Should().Be("Lunch Menu");
        menu.IsDefault.Should().BeFalse();
        menu.LocationId.Should().Be(_fixture.TestLocationId);
    }

    [Fact]
    public async Task Create_NewDefault_UnsetsPreviousDefault()
    {
        var request = new CreateMenuRequest(
            Name: "New Default Menu",
            Description: "This should become default",
            IsDefault: true);

        var response = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/menus", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var menu = await response.Content.ReadFromJsonAsync<MenuDto>();
        menu!.IsDefault.Should().BeTrue();

        // Check original default is no longer default
        var originalResponse = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/menus/{_fixture.TestMenuId}");
        var originalMenu = await originalResponse.Content.ReadFromJsonAsync<MenuDto>();
        originalMenu!.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task Update_UpdatesMenu()
    {
        // First create a new menu
        var createRequest = new CreateMenuRequest("To Update", null, false);
        var createResponse = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/menus", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MenuDto>();

        // Now update it
        var updateRequest = new UpdateMenuRequest(
            Name: "Updated Menu",
            Description: "Updated description",
            IsDefault: null,
            IsActive: null);

        var response = await _client.PutAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/menus/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<MenuDto>();
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Updated Menu");
    }

    [Fact]
    public async Task Delete_DeactivatesMenu()
    {
        // First create a new menu
        var createRequest = new CreateMenuRequest("To Delete", null, false);
        var createResponse = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/menus", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MenuDto>();

        // Now delete it
        var response = await _client.DeleteAsync($"/api/locations/{_fixture.TestLocationId}/menus/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's deactivated
        var getResponse = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/menus/{created.Id}");
        var menu = await getResponse.Content.ReadFromJsonAsync<MenuDto>();
        menu!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task AddScreen_AddsScreenToMenu()
    {
        // First create a new menu
        var createRequest = new CreateMenuRequest("Menu With Screens", null, false);
        var createResponse = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/menus", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MenuDto>();

        // Add a screen
        var screenRequest = new CreateScreenRequest(
            Name: "Test Screen",
            Position: 1,
            Color: "#00FF00",
            Rows: 3,
            Columns: 4);

        var response = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/menus/{created!.Id}/screens", screenRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var screen = await response.Content.ReadFromJsonAsync<MenuScreenDto>();
        screen.Should().NotBeNull();
        screen!.Name.Should().Be("Test Screen");
        screen.Rows.Should().Be(3);
        screen.Columns.Should().Be(4);
    }

    [Fact]
    public async Task AddButton_AddsButtonToScreen()
    {
        // First create a new menu with a screen
        var createMenuRequest = new CreateMenuRequest("Menu For Buttons", null, false);
        var createMenuResponse = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/menus", createMenuRequest);
        var menu = await createMenuResponse.Content.ReadFromJsonAsync<MenuDto>();

        var screenRequest = new CreateScreenRequest("Button Screen", 1, null, 4, 5);
        var screenResponse = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/menus/{menu!.Id}/screens", screenRequest);
        var screen = await screenResponse.Content.ReadFromJsonAsync<MenuScreenDto>();

        // Add a button
        var buttonRequest = new CreateButtonRequest(
            Row: 0,
            Column: 0,
            ItemId: _fixture.TestItemId,
            Label: null,
            Color: "#FF0000",
            RowSpan: 1,
            ColumnSpan: 1,
            ButtonType: "item");

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/menus/{menu.Id}/screens/{screen!.Id}/buttons",
            buttonRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var button = await response.Content.ReadFromJsonAsync<MenuButtonDto>();
        button.Should().NotBeNull();
        button!.ItemId.Should().Be(_fixture.TestItemId);
        button.Row.Should().Be(0);
        button.Column.Should().Be(0);
    }
}
