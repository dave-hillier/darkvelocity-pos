using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Reporting.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Reporting.Tests;

public class MarginThresholdsControllerTests : IClassFixture<ReportingApiFixture>
{
    private readonly ReportingApiFixture _fixture;
    private readonly HttpClient _client;

    public MarginThresholdsControllerTests(ReportingApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsThresholds()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/margin-thresholds");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<MarginThresholdDto>>();
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_FilterByType()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/margin-thresholds?thresholdType=overall");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<MarginThresholdDto>>();
        result.Should().NotBeNull();
        result!.All(t => t.ThresholdType == "overall").Should().BeTrue();
    }

    [Fact]
    public async Task GetAll_FilterByActive()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/margin-thresholds?isActive=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<MarginThresholdDto>>();
        result.Should().NotBeNull();
        result!.All(t => t.IsActive).Should().BeTrue();
    }

    [Fact]
    public async Task GetById_ExistingThreshold_ReturnsDetails()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/margin-thresholds/{_fixture.TestThresholdId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<MarginThresholdDto>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(_fixture.TestThresholdId);
        result.ThresholdType.Should().Be("overall");
        result.MinimumMarginPercent.Should().Be(50.00m);
        result.WarningMarginPercent.Should().Be(60.00m);
    }

    [Fact]
    public async Task GetById_NonExistingThreshold_ReturnsNotFound()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/margin-thresholds/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_NewOverallThreshold_ReturnsCreated()
    {
        var newLocationId = Guid.NewGuid(); // Use new location to avoid conflict
        var request = new CreateMarginThresholdRequest(
            ThresholdType: "overall",
            MinimumMarginPercent: 45.00m,
            WarningMarginPercent: 55.00m
        );

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/margin-thresholds", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<MarginThresholdDto>();
        result.Should().NotBeNull();
        result!.ThresholdType.Should().Be("overall");
        result.MinimumMarginPercent.Should().Be(45.00m);
        result.WarningMarginPercent.Should().Be(55.00m);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Create_CategoryThreshold_ReturnsCreated()
    {
        var categoryId = Guid.NewGuid();
        var request = new CreateMarginThresholdRequest(
            ThresholdType: "category",
            MinimumMarginPercent: 40.00m,
            WarningMarginPercent: 50.00m,
            CategoryId: categoryId
        );

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/margin-thresholds", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<MarginThresholdDto>();
        result.Should().NotBeNull();
        result!.ThresholdType.Should().Be("category");
        result.CategoryId.Should().Be(categoryId);
    }

    [Fact]
    public async Task Create_ItemThreshold_ReturnsCreated()
    {
        var menuItemId = Guid.NewGuid();
        var request = new CreateMarginThresholdRequest(
            ThresholdType: "item",
            MinimumMarginPercent: 35.00m,
            WarningMarginPercent: 45.00m,
            MenuItemId: menuItemId
        );

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/margin-thresholds", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<MarginThresholdDto>();
        result.Should().NotBeNull();
        result!.ThresholdType.Should().Be("item");
        result.MenuItemId.Should().Be(menuItemId);
    }

    [Fact]
    public async Task Create_DuplicateThreshold_ReturnsConflict()
    {
        var request = new CreateMarginThresholdRequest(
            ThresholdType: "overall",
            MinimumMarginPercent: 50.00m,
            WarningMarginPercent: 60.00m
        );

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/margin-thresholds", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_ExistingThreshold_ReturnsUpdated()
    {
        // First create a threshold to update
        var newLocationId = Guid.NewGuid();
        var createRequest = new CreateMarginThresholdRequest(
            ThresholdType: "overall",
            MinimumMarginPercent: 50.00m,
            WarningMarginPercent: 60.00m
        );
        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/margin-thresholds", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MarginThresholdDto>();

        // Now update it
        var updateRequest = new UpdateMarginThresholdRequest(
            MinimumMarginPercent: 55.00m,
            WarningMarginPercent: 65.00m
        );

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{newLocationId}/margin-thresholds/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<MarginThresholdDto>();
        result.Should().NotBeNull();
        result!.MinimumMarginPercent.Should().Be(55.00m);
        result.WarningMarginPercent.Should().Be(65.00m);
    }

    [Fact]
    public async Task Update_DeactivateThreshold_ReturnsUpdated()
    {
        // First create a threshold to deactivate
        var newLocationId = Guid.NewGuid();
        var createRequest = new CreateMarginThresholdRequest(
            ThresholdType: "overall",
            MinimumMarginPercent: 50.00m,
            WarningMarginPercent: 60.00m
        );
        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/margin-thresholds", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MarginThresholdDto>();

        // Now deactivate it
        var updateRequest = new UpdateMarginThresholdRequest(IsActive: false);

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{newLocationId}/margin-thresholds/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<MarginThresholdDto>();
        result.Should().NotBeNull();
        result!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Update_NonExistingThreshold_ReturnsNotFound()
    {
        var updateRequest = new UpdateMarginThresholdRequest(MinimumMarginPercent: 55.00m);

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/margin-thresholds/{Guid.NewGuid()}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ExistingThreshold_ReturnsNoContent()
    {
        // First create a threshold to delete
        var newLocationId = Guid.NewGuid();
        var createRequest = new CreateMarginThresholdRequest(
            ThresholdType: "overall",
            MinimumMarginPercent: 50.00m,
            WarningMarginPercent: 60.00m
        );
        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/margin-thresholds", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MarginThresholdDto>();

        // Now delete it
        var response = await _client.DeleteAsync(
            $"/api/locations/{newLocationId}/margin-thresholds/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's gone
        var getResponse = await _client.GetAsync(
            $"/api/locations/{newLocationId}/margin-thresholds/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_NonExistingThreshold_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync(
            $"/api/locations/{_fixture.TestLocationId}/margin-thresholds/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_IncludesHalLinks()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/margin-thresholds/{_fixture.TestThresholdId}");

        var result = await response.Content.ReadFromJsonAsync<MarginThresholdDto>();
        result.Should().NotBeNull();
        result!.Links.Should().ContainKey("self");
    }
}
