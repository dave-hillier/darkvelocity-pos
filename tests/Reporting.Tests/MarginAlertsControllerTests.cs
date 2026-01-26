using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Reporting.Api.Controllers;
using DarkVelocity.Reporting.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Reporting.Tests;

public class MarginAlertsControllerTests : IClassFixture<ReportingApiFixture>
{
    private readonly ReportingApiFixture _fixture;
    private readonly HttpClient _client;

    public MarginAlertsControllerTests(ReportingApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsAlerts()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/margin-alerts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<MarginAlertDto>>();
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_FilterByAcknowledged()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/margin-alerts?acknowledged=false");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<MarginAlertDto>>();
        result.Should().NotBeNull();
        result!.All(a => !a.IsAcknowledged).Should().BeTrue();
    }

    [Fact]
    public async Task GetAll_FilterByAlertType()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/margin-alerts?alertType=item_margin_low");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<MarginAlertDto>>();
        result.Should().NotBeNull();
        result!.All(a => a.AlertType == "item_margin_low").Should().BeTrue();
    }

    [Fact]
    public async Task GetById_ExistingAlert_ReturnsDetails()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/margin-alerts/{_fixture.TestAlertId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<MarginAlertDto>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(_fixture.TestAlertId);
        result.AlertType.Should().Be("item_margin_low");
        result.MenuItemName.Should().Be("Burger");
    }

    [Fact]
    public async Task GetById_NonExistingAlert_ReturnsNotFound()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/margin-alerts/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_NewAlert_ReturnsCreated()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new CreateMarginAlertRequest(
            AlertType: "category_margin_low",
            ReportDate: today,
            CurrentMargin: 35.00m,
            ThresholdMargin: 50.00m,
            CategoryId: _fixture.TestCategoryId,
            CategoryName: "Food"
        );

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/margin-alerts", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<MarginAlertDto>();
        result.Should().NotBeNull();
        result!.AlertType.Should().Be("category_margin_low");
        result.CurrentMargin.Should().Be(35.00m);
        result.ThresholdMargin.Should().Be(50.00m);
        result.Variance.Should().Be(-15.00m);
        result.IsAcknowledged.Should().BeFalse();
    }

    [Fact]
    public async Task Acknowledge_ExistingAlert_UpdatesStatus()
    {
        // First create a new alert to acknowledge
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var createRequest = new CreateMarginAlertRequest(
            AlertType: "daily_margin_low",
            ReportDate: today,
            CurrentMargin: 40.00m,
            ThresholdMargin: 50.00m
        );

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/margin-alerts", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MarginAlertDto>();

        // Now acknowledge it
        var ackRequest = new AcknowledgeAlertRequest(Notes: "Reviewed and acceptable for seasonal promotion");

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/margin-alerts/{created!.Id}/acknowledge", ackRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<MarginAlertDto>();
        result.Should().NotBeNull();
        result!.IsAcknowledged.Should().BeTrue();
        result.AcknowledgedAt.Should().NotBeNull();
        result.Notes.Should().Be("Reviewed and acceptable for seasonal promotion");
    }

    [Fact]
    public async Task Acknowledge_NonExistingAlert_ReturnsNotFound()
    {
        var ackRequest = new AcknowledgeAlertRequest(Notes: "test");

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/margin-alerts/{Guid.NewGuid()}/acknowledge", ackRequest);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ExistingAlert_ReturnsNoContent()
    {
        // First create an alert to delete
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var createRequest = new CreateMarginAlertRequest(
            AlertType: "item_margin_low",
            ReportDate: today,
            CurrentMargin: 42.00m,
            ThresholdMargin: 50.00m,
            MenuItemId: Guid.NewGuid(),
            MenuItemName: "Test Item"
        );

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/margin-alerts", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MarginAlertDto>();

        // Now delete it
        var response = await _client.DeleteAsync(
            $"/api/locations/{_fixture.TestLocationId}/margin-alerts/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's gone
        var getResponse = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/margin-alerts/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_NonExistingAlert_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync(
            $"/api/locations/{_fixture.TestLocationId}/margin-alerts/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_IncludesHalLinks()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/margin-alerts/{_fixture.TestAlertId}");

        var result = await response.Content.ReadFromJsonAsync<MarginAlertDto>();
        result.Should().NotBeNull();
        result!.Links.Should().ContainKey("self");
    }
}
