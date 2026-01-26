using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Costing.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Costing.Tests;

public class RecipeSnapshotsControllerTests : IClassFixture<CostingApiFixture>
{
    private readonly CostingApiFixture _fixture;
    private readonly HttpClient _client;

    public RecipeSnapshotsControllerTests(CostingApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsAllSnapshotsForRecipe()
    {
        var response = await _client.GetAsync($"/api/recipes/{_fixture.BurgerRecipeId}/snapshots");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var snapshots = await response.Content.ReadFromJsonAsync<List<RecipeCostSnapshotDto>>();
        snapshots.Should().NotBeNull();
        snapshots!.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAll_RecipeNotFound_Returns404()
    {
        var response = await _client.GetAsync($"/api/recipes/{Guid.NewGuid()}/snapshots");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAll_OrderedByDateDescending()
    {
        var response = await _client.GetAsync($"/api/recipes/{_fixture.BurgerRecipeId}/snapshots");

        var snapshots = await response.Content.ReadFromJsonAsync<List<RecipeCostSnapshotDto>>();

        var dates = snapshots!.Select(s => s.SnapshotDate).ToList();
        dates.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GetAll_WithDateFilter_ReturnsFilteredSnapshots()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = today.AddDays(-10).ToString("yyyy-MM-dd");
        var endDate = today.ToString("yyyy-MM-dd");

        var response = await _client.GetAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/snapshots?startDate={startDate}&endDate={endDate}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var snapshots = await response.Content.ReadFromJsonAsync<List<RecipeCostSnapshotDto>>();
        snapshots.Should().NotBeNull();
        snapshots!.Should().OnlyContain(s =>
            s.SnapshotDate >= DateOnly.Parse(startDate) &&
            s.SnapshotDate <= DateOnly.Parse(endDate));
    }

    [Fact]
    public async Task GetById_ReturnsSnapshotWithDetails()
    {
        var response = await _client.GetAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/snapshots/{_fixture.SnapshotId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var snapshot = await response.Content.ReadFromJsonAsync<RecipeCostSnapshotDto>();
        snapshot.Should().NotBeNull();
        snapshot!.Id.Should().Be(_fixture.SnapshotId);
        snapshot.RecipeId.Should().Be(_fixture.BurgerRecipeId);
        snapshot.TotalIngredientCost.Should().Be(3.75m);
        snapshot.CostPerPortion.Should().Be(3.75m);
        snapshot.MenuPrice.Should().Be(12.00m);
        snapshot.SnapshotReason.Should().Be("price_change");
    }

    [Fact]
    public async Task GetById_IncludesHalLinks()
    {
        var response = await _client.GetAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/snapshots/{_fixture.SnapshotId}");

        var snapshot = await response.Content.ReadFromJsonAsync<RecipeCostSnapshotDto>();
        snapshot!.Links.Should().ContainKey("self");
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        var response = await _client.GetAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/snapshots/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetLatest_ReturnsLatestSnapshot()
    {
        var response = await _client.GetAsync($"/api/recipes/{_fixture.BurgerRecipeId}/snapshots/latest");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var snapshot = await response.Content.ReadFromJsonAsync<RecipeCostSnapshotDto>();
        snapshot.Should().NotBeNull();
        snapshot!.RecipeId.Should().Be(_fixture.BurgerRecipeId);
    }

    [Fact]
    public async Task GetLatest_NoSnapshots_Returns404()
    {
        // Create a new recipe without snapshots
        var menuItemId = Guid.NewGuid();
        var createRequest = new CreateRecipeRequest(
            MenuItemId: menuItemId,
            MenuItemName: "No Snapshot Recipe",
            Code: "RCP-NOSNAPSHOT-001"
        );
        var createResponse = await _client.PostAsJsonAsync("/api/recipes", createRequest);
        var recipe = await createResponse.Content.ReadFromJsonAsync<RecipeDto>();

        var response = await _client.GetAsync($"/api/recipes/{recipe!.Id}/snapshots/latest");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_WithValidData_ReturnsCreatedSnapshot()
    {
        var request = new CreateSnapshotRequest(
            MenuPrice: 15.00m,
            SnapshotReason: "manual_review"
        );

        var response = await _client.PostAsJsonAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/snapshots",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var snapshot = await response.Content.ReadFromJsonAsync<RecipeCostSnapshotDto>();
        snapshot.Should().NotBeNull();
        snapshot!.RecipeId.Should().Be(_fixture.BurgerRecipeId);
        snapshot.MenuPrice.Should().Be(15.00m);
        snapshot.SnapshotReason.Should().Be("manual_review");
        snapshot.SnapshotDate.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));
    }

    [Fact]
    public async Task Create_CalculatesMarginFromCurrentIngredients()
    {
        var request = new CreateSnapshotRequest(
            MenuPrice: 10.00m,
            SnapshotReason: "test"
        );

        var response = await _client.PostAsJsonAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/snapshots",
            request);

        var snapshot = await response.Content.ReadFromJsonAsync<RecipeCostSnapshotDto>();
        snapshot!.TotalIngredientCost.Should().BeGreaterThan(0);
        snapshot.CostPercentage.Should().BeGreaterThan(0);
        snapshot.GrossMarginPercent.Should().Be(100 - snapshot.CostPercentage);
    }

    [Fact]
    public async Task Create_RecipeNotFound_Returns404()
    {
        var request = new CreateSnapshotRequest(MenuPrice: 10.00m);

        var response = await _client.PostAsJsonAsync(
            $"/api/recipes/{Guid.NewGuid()}/snapshots",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Compare_ReturnsComparisonData()
    {
        var date1 = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-14)).ToString("yyyy-MM-dd");
        var date2 = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)).ToString("yyyy-MM-dd");

        var response = await _client.GetAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/snapshots/compare?date1={date1}&date2={date2}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var comparison = await response.Content.ReadFromJsonAsync<SnapshotComparisonDto>();
        comparison.Should().NotBeNull();
        comparison!.Date1.Should().Be(DateOnly.Parse(date1));
        comparison.Date2.Should().Be(DateOnly.Parse(date2));
        comparison.Snapshot1.Should().NotBeNull();
        comparison.Snapshot2.Should().NotBeNull();
    }

    [Fact]
    public async Task Compare_CalculatesCostChange()
    {
        var date1 = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-14)).ToString("yyyy-MM-dd");
        var date2 = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)).ToString("yyyy-MM-dd");

        var response = await _client.GetAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/snapshots/compare?date1={date1}&date2={date2}");

        var comparison = await response.Content.ReadFromJsonAsync<SnapshotComparisonDto>();
        // Cost went from 3.50 to 3.75
        comparison!.CostChange.Should().Be(0.25m);
        comparison.CostChangePercent.Should().BeApproximately(7.14m, 0.1m);
    }

    [Fact]
    public async Task Compare_SnapshotNotFound_Returns404()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var date1 = today.AddDays(-100).ToString("yyyy-MM-dd"); // No snapshot exists
        var date2 = today.AddDays(-7).ToString("yyyy-MM-dd");

        var response = await _client.GetAsync(
            $"/api/recipes/{_fixture.BurgerRecipeId}/snapshots/compare?date1={date1}&date2={date2}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

// Helper DTO for comparison response
public class SnapshotComparisonDto
{
    public DateOnly Date1 { get; set; }
    public DateOnly Date2 { get; set; }
    public RecipeCostSnapshotDto? Snapshot1 { get; set; }
    public RecipeCostSnapshotDto? Snapshot2 { get; set; }
    public decimal CostChange { get; set; }
    public decimal CostChangePercent { get; set; }
    public decimal MarginChange { get; set; }
}
