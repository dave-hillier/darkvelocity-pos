using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Location.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Location.Tests;

public class LocationsControllerTests : IClassFixture<LocationApiFixture>
{
    private readonly LocationApiFixture _fixture;
    private readonly HttpClient _client;

    public LocationsControllerTests(LocationApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsAllActiveLocations()
    {
        var response = await _client.GetAsync("/api/locations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<LocationSummaryDto>>();
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThanOrEqualTo(2); // At least 2 active locations
    }

    [Fact]
    public async Task GetAll_FilterByActive_ReturnsOnlyActive()
    {
        var response = await _client.GetAsync("/api/locations?isActive=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<LocationSummaryDto>>();
        result.Should().NotBeNull();
        result!.All(l => l.IsActive).Should().BeTrue();
    }

    [Fact]
    public async Task GetAll_FilterByInactive_ReturnsOnlyInactive()
    {
        var response = await _client.GetAsync("/api/locations?isActive=false");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<LocationSummaryDto>>();
        result.Should().NotBeNull();
        result!.All(l => !l.IsActive).Should().BeTrue();
    }

    [Fact]
    public async Task GetById_ExistingLocation_ReturnsDetails()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<LocationDto>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(_fixture.TestLocationId);
        result.Name.Should().Be("Downtown Restaurant");
        result.Code.Should().Be("NYC-01");
        result.CurrencyCode.Should().Be("USD");
        result.CurrencySymbol.Should().Be("$");
    }

    [Fact]
    public async Task GetById_IncludesSettings()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}");

        var result = await response.Content.ReadFromJsonAsync<LocationDto>();
        result.Should().NotBeNull();
        result!.Settings.Should().NotBeNull();
        result.Settings!.DefaultTaxRate.Should().Be(8.875m);
    }

    [Fact]
    public async Task GetById_IncludesOperatingHours()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}");

        var result = await response.Content.ReadFromJsonAsync<LocationDto>();
        result.Should().NotBeNull();
        result!.OperatingHours.Should().NotBeNullOrEmpty();
        result.OperatingHours.Should().HaveCount(7); // All days of week
    }

    [Fact]
    public async Task GetById_IncludesAddress()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}");

        var result = await response.Content.ReadFromJsonAsync<LocationDto>();
        result.Should().NotBeNull();
        result!.Address.Should().NotBeNull();
        result.Address!.Line1.Should().Be("123 Main Street");
        result.Address.City.Should().Be("New York");
    }

    [Fact]
    public async Task GetById_NonExistingLocation_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/locations/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByCode_ExistingCode_ReturnsLocation()
    {
        var response = await _client.GetAsync("/api/locations/by-code/NYC-01");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<LocationDto>();
        result.Should().NotBeNull();
        result!.Code.Should().Be("NYC-01");
    }

    [Fact]
    public async Task GetByCode_NonExistingCode_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/locations/by-code/INVALID");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_ValidLocation_ReturnsCreated()
    {
        var request = new CreateLocationRequest(
            Name: "Test Location",
            Code: "TST-01",
            Timezone: "America/Chicago",
            CurrencyCode: "USD",
            City: "Chicago",
            State: "IL"
        );

        var response = await _client.PostAsJsonAsync("/api/locations", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<LocationDto>();
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Location");
        result.Code.Should().Be("TST-01");
        result.CurrencySymbol.Should().Be("$"); // Auto-detected from USD
    }

    [Fact]
    public async Task Create_AutoCreateSettings()
    {
        var request = new CreateLocationRequest(
            Name: "Auto Settings Location",
            Code: "AUT-01",
            Timezone: "UTC",
            CurrencyCode: "EUR"
        );

        var response = await _client.PostAsJsonAsync("/api/locations", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<LocationDto>();
        result.Should().NotBeNull();
        result!.Settings.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_DuplicateCode_ReturnsConflict()
    {
        var request = new CreateLocationRequest(
            Name: "Duplicate Location",
            Code: "NYC-01", // Already exists
            Timezone: "America/New_York",
            CurrencyCode: "USD"
        );

        var response = await _client.PostAsJsonAsync("/api/locations", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_WithCustomCurrencySymbol_UsesProvided()
    {
        var request = new CreateLocationRequest(
            Name: "Custom Symbol Location",
            Code: "CSL-01",
            Timezone: "UTC",
            CurrencyCode: "BTC",
            CurrencySymbol: "₿"
        );

        var response = await _client.PostAsJsonAsync("/api/locations", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<LocationDto>();
        result.Should().NotBeNull();
        result!.CurrencySymbol.Should().Be("₿");
    }

    [Fact]
    public async Task Update_ExistingLocation_ReturnsUpdated()
    {
        var request = new UpdateLocationRequest(
            Name: "Updated Downtown Restaurant",
            Phone: "+1 212-555-9999"
        );

        var response = await _client.PutAsJsonAsync($"/api/locations/{_fixture.TestLocationId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<LocationDto>();
        result.Should().NotBeNull();
        result!.Name.Should().StartWith("Updated Downtown Restaurant");
        result.Phone.Should().Be("+1 212-555-9999");
    }

    [Fact]
    public async Task Update_NonExistingLocation_ReturnsNotFound()
    {
        var request = new UpdateLocationRequest(Name: "Test");

        var response = await _client.PutAsJsonAsync($"/api/locations/{Guid.NewGuid()}", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_DuplicateCode_ReturnsConflict()
    {
        var request = new UpdateLocationRequest(Code: "LON-01"); // Already exists on location 2

        var response = await _client.PutAsJsonAsync($"/api/locations/{_fixture.TestLocationId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Open_ClosedLocation_SetsIsOpenTrue()
    {
        var response = await _client.PostAsync($"/api/locations/{_fixture.InactiveLocationId}/open", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<LocationDto>();
        result.Should().NotBeNull();
        result!.IsOpen.Should().BeTrue();
    }

    [Fact]
    public async Task Close_OpenLocation_SetsIsOpenFalse()
    {
        var response = await _client.PostAsync($"/api/locations/{_fixture.TestLocationId}/close", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<LocationDto>();
        result.Should().NotBeNull();
        result!.IsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_ExistingLocation_SoftDeletes()
    {
        // Create a location to delete
        var createRequest = new CreateLocationRequest(
            Name: "To Delete",
            Code: "DEL-01",
            Timezone: "UTC",
            CurrencyCode: "USD"
        );
        var createResponse = await _client.PostAsJsonAsync("/api/locations", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<LocationDto>();

        // Delete it
        var response = await _client.DeleteAsync($"/api/locations/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's deactivated, not deleted
        var getResponse = await _client.GetAsync($"/api/locations/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await getResponse.Content.ReadFromJsonAsync<LocationDto>();
        result!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_NonExistingLocation_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync($"/api/locations/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_IncludesHalLinks()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}");

        var result = await response.Content.ReadFromJsonAsync<LocationDto>();
        result.Should().NotBeNull();
        result!.Links.Should().ContainKey("self");
        result.Links.Should().ContainKey("settings");
        result.Links.Should().ContainKey("hours");
    }
}
