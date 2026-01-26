using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Costing.Api.Controllers;
using DarkVelocity.Costing.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Costing.Tests;

public class CostAlertsControllerTests : IClassFixture<CostingApiFixture>
{
    private readonly CostingApiFixture _fixture;
    private readonly HttpClient _client;

    public CostAlertsControllerTests(CostingApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsAllAlerts()
    {
        var response = await _client.GetAsync("/api/cost-alerts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var alerts = await response.Content.ReadFromJsonAsync<List<CostAlertDto>>();
        alerts.Should().NotBeNull();
        alerts!.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAll_WithAcknowledgedFilter_ReturnsFilteredAlerts()
    {
        var response = await _client.GetAsync("/api/cost-alerts?acknowledged=false");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var alerts = await response.Content.ReadFromJsonAsync<List<CostAlertDto>>();
        alerts.Should().NotBeNull();
        alerts!.Should().OnlyContain(a => !a.IsAcknowledged);
    }

    [Fact]
    public async Task GetAll_WithAlertTypeFilter_ReturnsFilteredAlerts()
    {
        var response = await _client.GetAsync("/api/cost-alerts?alertType=cost_increase");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var alerts = await response.Content.ReadFromJsonAsync<List<CostAlertDto>>();
        alerts.Should().NotBeNull();
        alerts!.Should().OnlyContain(a => a.AlertType == "cost_increase");
    }

    [Fact]
    public async Task GetAll_WithRecipeIdFilter_ReturnsFilteredAlerts()
    {
        var response = await _client.GetAsync($"/api/cost-alerts?recipeId={_fixture.BurgerRecipeId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var alerts = await response.Content.ReadFromJsonAsync<List<CostAlertDto>>();
        alerts.Should().NotBeNull();
        alerts!.Should().OnlyContain(a => a.RecipeId == _fixture.BurgerRecipeId);
    }

    [Fact]
    public async Task GetAll_WithIngredientIdFilter_ReturnsFilteredAlerts()
    {
        var response = await _client.GetAsync($"/api/cost-alerts?ingredientId={_fixture.CheeseId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var alerts = await response.Content.ReadFromJsonAsync<List<CostAlertDto>>();
        alerts.Should().NotBeNull();
        alerts!.Should().Contain(a => a.IngredientId == _fixture.CheeseId);
    }

    [Fact]
    public async Task GetAll_OrderedByCreatedAtDescending()
    {
        var response = await _client.GetAsync("/api/cost-alerts");

        var alerts = await response.Content.ReadFromJsonAsync<List<CostAlertDto>>();

        var dates = alerts!.Select(a => a.CreatedAt).ToList();
        dates.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GetById_ReturnsAlertWithDetails()
    {
        var response = await _client.GetAsync($"/api/cost-alerts/{_fixture.TestAlertId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var alert = await response.Content.ReadFromJsonAsync<CostAlertDto>();
        alert.Should().NotBeNull();
        alert!.Id.Should().Be(_fixture.TestAlertId);
        alert.AlertType.Should().Be("cost_increase");
        alert.RecipeName.Should().Be("Classic Cheeseburger");
        alert.IngredientName.Should().Be("Cheddar Cheese");
        alert.PreviousValue.Should().Be(18.00m);
        alert.CurrentValue.Should().Be(20.00m);
        alert.ChangePercent.Should().Be(11.11m);
        alert.IsAcknowledged.Should().BeFalse();
    }

    [Fact]
    public async Task GetById_IncludesHalLinks()
    {
        var response = await _client.GetAsync($"/api/cost-alerts/{_fixture.TestAlertId}");

        var alert = await response.Content.ReadFromJsonAsync<CostAlertDto>();
        alert!.Links.Should().ContainKey("self");
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/cost-alerts/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUnacknowledgedCount_ReturnsCountByType()
    {
        var response = await _client.GetAsync("/api/cost-alerts/unacknowledged/count");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<UnacknowledgedCountDto>();
        result.Should().NotBeNull();
        result!.Total.Should().BeGreaterThanOrEqualTo(1);
        result.ByType.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Acknowledge_UpdatesAlertStatus()
    {
        // Create a fresh alert to acknowledge
        var createRequest = new CreateCostAlertRequest(
            AlertType: "test_alert",
            PreviousValue: 1.00m,
            CurrentValue: 2.00m,
            ChangePercent: 100m
        );
        var createResponse = await _client.PostAsJsonAsync("/api/cost-alerts", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CostAlertDto>();

        // Acknowledge it
        var acknowledgeRequest = new AcknowledgeCostAlertRequest(
            Notes: "Reviewed and acceptable",
            ActionTaken: "no_action_required"
        );
        var response = await _client.PostAsJsonAsync(
            $"/api/cost-alerts/{created!.Id}/acknowledge",
            acknowledgeRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var alert = await response.Content.ReadFromJsonAsync<CostAlertDto>();
        alert.Should().NotBeNull();
        alert!.IsAcknowledged.Should().BeTrue();
        alert.AcknowledgedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        alert.Notes.Should().Be("Reviewed and acceptable");
        alert.ActionTaken.Should().Be("no_action_required");
    }

    [Fact]
    public async Task Acknowledge_NotFound_Returns404()
    {
        var request = new AcknowledgeCostAlertRequest(Notes: "Test");

        var response = await _client.PostAsJsonAsync(
            $"/api/cost-alerts/{Guid.NewGuid()}/acknowledge",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AcknowledgeBulk_AcknowledgesMultipleAlerts()
    {
        // Create alerts to acknowledge
        var alertIds = new List<Guid>();
        for (int i = 0; i < 3; i++)
        {
            var createRequest = new CreateCostAlertRequest(
                AlertType: "bulk_test",
                PreviousValue: 1.00m,
                CurrentValue: 2.00m,
                ChangePercent: 100m
            );
            var createResponse = await _client.PostAsJsonAsync("/api/cost-alerts", createRequest);
            var created = await createResponse.Content.ReadFromJsonAsync<CostAlertDto>();
            alertIds.Add(created!.Id);
        }

        // Bulk acknowledge
        var response = await _client.PostAsJsonAsync(
            "/api/cost-alerts/acknowledge-bulk?actionTaken=batch_reviewed",
            alertIds);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<BulkAcknowledgeResultDto>();
        result.Should().NotBeNull();
        result!.AcknowledgedCount.Should().Be(3);

        // Verify all are acknowledged
        foreach (var id in alertIds)
        {
            var getResponse = await _client.GetAsync($"/api/cost-alerts/{id}");
            var alert = await getResponse.Content.ReadFromJsonAsync<CostAlertDto>();
            alert!.IsAcknowledged.Should().BeTrue();
            alert.ActionTaken.Should().Be("batch_reviewed");
        }
    }

    [Fact]
    public async Task AcknowledgeBulk_SkipsAlreadyAcknowledged()
    {
        var alertIds = new List<Guid> { _fixture.AcknowledgedAlertId };

        var response = await _client.PostAsJsonAsync("/api/cost-alerts/acknowledge-bulk", alertIds);

        var result = await response.Content.ReadFromJsonAsync<BulkAcknowledgeResultDto>();
        result!.AcknowledgedCount.Should().Be(0);
    }

    [Fact]
    public async Task Create_WithValidData_ReturnsCreatedAlert()
    {
        var request = new CreateCostAlertRequest(
            AlertType: "margin_warning",
            PreviousValue: 65.00m,
            CurrentValue: 55.00m,
            ChangePercent: -15.38m,
            RecipeId: _fixture.BurgerRecipeId,
            RecipeName: "Classic Cheeseburger",
            ThresholdValue: 60.00m,
            ImpactDescription: "Margin dropped below warning threshold"
        );

        var response = await _client.PostAsJsonAsync("/api/cost-alerts", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var alert = await response.Content.ReadFromJsonAsync<CostAlertDto>();
        alert.Should().NotBeNull();
        alert!.AlertType.Should().Be("margin_warning");
        alert.RecipeId.Should().Be(_fixture.BurgerRecipeId);
        alert.PreviousValue.Should().Be(65.00m);
        alert.CurrentValue.Should().Be(55.00m);
        alert.IsAcknowledged.Should().BeFalse();
    }

    [Fact]
    public async Task Create_WithIngredientAlert_IncludesAffectedRecipeCount()
    {
        var request = new CreateCostAlertRequest(
            AlertType: "price_increase",
            PreviousValue: 10.00m,
            CurrentValue: 15.00m,
            ChangePercent: 50.00m,
            IngredientId: _fixture.BeefId,
            IngredientName: "Ground Beef",
            AffectedRecipeCount: 5
        );

        var response = await _client.PostAsJsonAsync("/api/cost-alerts", request);

        var alert = await response.Content.ReadFromJsonAsync<CostAlertDto>();
        alert!.AffectedRecipeCount.Should().Be(5);
    }

    [Fact]
    public async Task Delete_RemovesAlert()
    {
        // Create an alert to delete
        var createRequest = new CreateCostAlertRequest(
            AlertType: "to_delete",
            PreviousValue: 1.00m,
            CurrentValue: 2.00m,
            ChangePercent: 100m
        );
        var createResponse = await _client.PostAsJsonAsync("/api/cost-alerts", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CostAlertDto>();

        // Delete it
        var response = await _client.DeleteAsync($"/api/cost-alerts/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deleted
        var getResponse = await _client.GetAsync($"/api/cost-alerts/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_NotFound_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/cost-alerts/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

// Helper DTOs for responses
public class UnacknowledgedCountDto
{
    public int Total { get; set; }
    public List<AlertTypeCount> ByType { get; set; } = new();
}

public class AlertTypeCount
{
    public string AlertType { get; set; } = "";
    public int Count { get; set; }
}

public class BulkAcknowledgeResultDto
{
    public int AcknowledgedCount { get; set; }
}
