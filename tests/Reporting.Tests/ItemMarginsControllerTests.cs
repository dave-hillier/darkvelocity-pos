using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Reporting.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Reporting.Tests;

public class ItemMarginsControllerTests : IClassFixture<ReportingApiFixture>
{
    private readonly ReportingApiFixture _fixture;
    private readonly HttpClient _client;

    public ItemMarginsControllerTests(ReportingApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetRange_ReturnsAggregatedItemMargins()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/reports/item-margins");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ItemMarginReportDto>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetRange_DefaultSortByRevenue()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/reports/item-margins");

        var result = await response.Content.ReadFromJsonAsync<ItemMarginReportDto>();
        result.Should().NotBeNull();

        // Items should be sorted by revenue descending
        if (result!.Items.Count > 1)
        {
            result.Items[0].NetRevenue.Should().BeGreaterThanOrEqualTo(result.Items[1].NetRevenue);
        }
    }

    [Fact]
    public async Task GetRange_SortByMargin()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/item-margins?sortBy=margin");

        var result = await response.Content.ReadFromJsonAsync<ItemMarginReportDto>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetRange_SortByQuantity()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/item-margins?sortBy=quantity");

        var result = await response.Content.ReadFromJsonAsync<ItemMarginReportDto>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetRange_SortByProfit()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/item-margins?sortBy=profit");

        var result = await response.Content.ReadFromJsonAsync<ItemMarginReportDto>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetRange_FilterByCategory()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/item-margins?categoryId={_fixture.TestCategoryId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ItemMarginReportDto>();
        result.Should().NotBeNull();
        result!.Items.All(i => i.CategoryName == "Food").Should().BeTrue();
    }

    [Fact]
    public async Task GetByItem_ExistingItem_ReturnsDetails()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/item-margins/{_fixture.TestMenuItemId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ItemMarginReportDto>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Items[0].MenuItemId.Should().Be(_fixture.TestMenuItemId);
        result.Items[0].MenuItemName.Should().Be("Burger");
    }

    [Fact]
    public async Task GetByItem_NonExistingItem_ReturnsNotFound()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/item-margins/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByItem_AggregatesMultipleDays()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/item-margins/{_fixture.TestMenuItemId}");

        var result = await response.Content.ReadFromJsonAsync<ItemMarginReportDto>();
        result.Should().NotBeNull();

        // Should aggregate two days worth of data
        result!.Items[0].QuantitySold.Should().Be(55); // 30 + 25
        result.Items[0].NetRevenue.Should().Be(810.00m); // 440 + 370
    }

    [Fact]
    public async Task Generate_NewItemSummary_CreatesRecord()
    {
        var newItemId = Guid.NewGuid();
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5));
        var request = new GenerateItemSummaryRequest(
            Date: newDate,
            MenuItemId: newItemId,
            MenuItemName: "Pizza",
            CategoryId: _fixture.TestCategoryId,
            CategoryName: "Food",
            QuantitySold: 20,
            GrossRevenue: 300.00m,
            DiscountTotal: 10.00m,
            TotalCOGS: 100.00m
        );

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/item-margins", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<ItemSalesSummaryDto>();
        result.Should().NotBeNull();
        result!.MenuItemId.Should().Be(newItemId);
        result.NetRevenue.Should().Be(290.00m); // 300 - 10
        result.GrossProfit.Should().Be(190.00m); // 290 - 100
    }

    [Fact]
    public async Task Generate_CalculatesMarginMetrics()
    {
        var newItemId = Guid.NewGuid();
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-6));
        var request = new GenerateItemSummaryRequest(
            Date: newDate,
            MenuItemId: newItemId,
            MenuItemName: "Salad",
            CategoryId: _fixture.TestCategoryId,
            CategoryName: "Food",
            QuantitySold: 10,
            GrossRevenue: 100.00m,
            DiscountTotal: 0.00m,
            TotalCOGS: 25.00m // 25% COGS
        );

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/item-margins", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<ItemSalesSummaryDto>();
        result.Should().NotBeNull();
        result!.GrossMarginPercent.Should().Be(75.00m); // (100 - 25) / 100 * 100
        result.ProfitPerUnit.Should().Be(7.50m); // 75 / 10
        result.AverageCostPerUnit.Should().Be(2.50m); // 25 / 10
    }

    [Fact]
    public async Task GetRange_IncludesHalLinks()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/reports/item-margins");

        var result = await response.Content.ReadFromJsonAsync<ItemMarginReportDto>();
        result.Should().NotBeNull();
        result!.Links.Should().ContainKey("self");
    }
}
