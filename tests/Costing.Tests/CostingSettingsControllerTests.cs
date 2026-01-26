using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Costing.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Costing.Tests;

public class CostingSettingsControllerTests : IClassFixture<CostingApiFixture>
{
    private readonly CostingApiFixture _fixture;
    private readonly HttpClient _client;

    public CostingSettingsControllerTests(CostingApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task Get_ExistingSettings_ReturnsSettings()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/costing-settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var settings = await response.Content.ReadFromJsonAsync<CostingSettingsDto>();
        settings.Should().NotBeNull();
        settings!.LocationId.Should().Be(_fixture.TestLocationId);
        // Values may be modified by other tests, so just check they're within valid range
        settings.TargetFoodCostPercent.Should().BeGreaterThan(0);
        settings.TargetBeverageCostPercent.Should().BeGreaterThan(0);
        settings.MinimumMarginPercent.Should().BeGreaterThan(0);
        settings.WarningMarginPercent.Should().BeGreaterThan(0);
        settings.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Get_IncludesHalLinks()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/costing-settings");

        var settings = await response.Content.ReadFromJsonAsync<CostingSettingsDto>();
        settings!.Links.Should().ContainKey("self");
    }

    [Fact]
    public async Task Get_NewLocation_CreatesDefaultSettings()
    {
        var newLocationId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/locations/{newLocationId}/costing-settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var settings = await response.Content.ReadFromJsonAsync<CostingSettingsDto>();
        settings.Should().NotBeNull();
        settings!.LocationId.Should().Be(newLocationId);
        // Default values
        settings.TargetFoodCostPercent.Should().Be(30m);
        settings.TargetBeverageCostPercent.Should().Be(25m);
        settings.MinimumMarginPercent.Should().Be(50m);
        settings.WarningMarginPercent.Should().Be(60m);
        settings.PriceChangeAlertThreshold.Should().Be(10m);
        settings.CostIncreaseAlertThreshold.Should().Be(5m);
        settings.AutoRecalculateCosts.Should().BeTrue();
        settings.AutoCreateSnapshots.Should().BeTrue();
        settings.SnapshotFrequencyDays.Should().Be(7);
    }

    [Fact]
    public async Task Update_WithValidData_ReturnsUpdatedSettings()
    {
        var request = new UpdateCostingSettingsRequest(
            TargetFoodCostPercent: 28m,
            TargetBeverageCostPercent: 22m
        );

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/costing-settings",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var settings = await response.Content.ReadFromJsonAsync<CostingSettingsDto>();
        settings.Should().NotBeNull();
        settings!.TargetFoodCostPercent.Should().Be(28m);
        settings.TargetBeverageCostPercent.Should().Be(22m);
    }

    [Fact]
    public async Task Update_PartialUpdate_OnlyUpdatesProvidedFields()
    {
        // Get current values
        var getResponse = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/costing-settings");
        var before = await getResponse.Content.ReadFromJsonAsync<CostingSettingsDto>();

        // Update only one field
        var request = new UpdateCostingSettingsRequest(
            MinimumMarginPercent: 45m
        );

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/costing-settings",
            request);

        var after = await response.Content.ReadFromJsonAsync<CostingSettingsDto>();

        // Updated field
        after!.MinimumMarginPercent.Should().Be(45m);
        // Other fields unchanged
        after.WarningMarginPercent.Should().Be(before!.WarningMarginPercent);
        after.AutoRecalculateCosts.Should().Be(before.AutoRecalculateCosts);
    }

    [Fact]
    public async Task Update_NewLocation_CreatesAndUpdatesSettings()
    {
        var newLocationId = Guid.NewGuid();

        var request = new UpdateCostingSettingsRequest(
            TargetFoodCostPercent: 35m,
            AutoRecalculateCosts: false
        );

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{newLocationId}/costing-settings",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var settings = await response.Content.ReadFromJsonAsync<CostingSettingsDto>();
        settings.Should().NotBeNull();
        settings!.LocationId.Should().Be(newLocationId);
        settings.TargetFoodCostPercent.Should().Be(35m);
        settings.AutoRecalculateCosts.Should().BeFalse();
    }

    [Fact]
    public async Task Update_AlertThresholds_UpdatesThresholds()
    {
        var locationId = Guid.NewGuid();

        var request = new UpdateCostingSettingsRequest(
            PriceChangeAlertThreshold: 15m,
            CostIncreaseAlertThreshold: 8m
        );

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{locationId}/costing-settings",
            request);

        var settings = await response.Content.ReadFromJsonAsync<CostingSettingsDto>();
        settings!.PriceChangeAlertThreshold.Should().Be(15m);
        settings.CostIncreaseAlertThreshold.Should().Be(8m);
    }

    [Fact]
    public async Task Update_SnapshotSettings_UpdatesSettings()
    {
        var locationId = Guid.NewGuid();

        var request = new UpdateCostingSettingsRequest(
            AutoCreateSnapshots: false,
            SnapshotFrequencyDays: 14
        );

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{locationId}/costing-settings",
            request);

        var settings = await response.Content.ReadFromJsonAsync<CostingSettingsDto>();
        settings!.AutoCreateSnapshots.Should().BeFalse();
        settings.SnapshotFrequencyDays.Should().Be(14);
    }

    [Fact]
    public async Task Update_AllFields_UpdatesAllFields()
    {
        var locationId = Guid.NewGuid();

        var request = new UpdateCostingSettingsRequest(
            TargetFoodCostPercent: 32m,
            TargetBeverageCostPercent: 28m,
            MinimumMarginPercent: 48m,
            WarningMarginPercent: 55m,
            PriceChangeAlertThreshold: 12m,
            CostIncreaseAlertThreshold: 7m,
            AutoRecalculateCosts: false,
            AutoCreateSnapshots: false,
            SnapshotFrequencyDays: 30
        );

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{locationId}/costing-settings",
            request);

        var settings = await response.Content.ReadFromJsonAsync<CostingSettingsDto>();
        settings!.TargetFoodCostPercent.Should().Be(32m);
        settings.TargetBeverageCostPercent.Should().Be(28m);
        settings.MinimumMarginPercent.Should().Be(48m);
        settings.WarningMarginPercent.Should().Be(55m);
        settings.PriceChangeAlertThreshold.Should().Be(12m);
        settings.CostIncreaseAlertThreshold.Should().Be(7m);
        settings.AutoRecalculateCosts.Should().BeFalse();
        settings.AutoCreateSnapshots.Should().BeFalse();
        settings.SnapshotFrequencyDays.Should().Be(30);
    }

    [Fact]
    public async Task Get_MultipleCalls_ReturnsSameSettings()
    {
        var locationId = Guid.NewGuid();

        // First call creates settings
        var response1 = await _client.GetAsync($"/api/locations/{locationId}/costing-settings");
        var settings1 = await response1.Content.ReadFromJsonAsync<CostingSettingsDto>();

        // Second call returns same settings
        var response2 = await _client.GetAsync($"/api/locations/{locationId}/costing-settings");
        var settings2 = await response2.Content.ReadFromJsonAsync<CostingSettingsDto>();

        settings1!.Id.Should().Be(settings2!.Id);
    }

    [Fact]
    public async Task Update_IncludesHalLinks()
    {
        var request = new UpdateCostingSettingsRequest(
            TargetFoodCostPercent: 29m
        );

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/costing-settings",
            request);

        var settings = await response.Content.ReadFromJsonAsync<CostingSettingsDto>();
        settings!.Links.Should().ContainKey("self");
    }
}
