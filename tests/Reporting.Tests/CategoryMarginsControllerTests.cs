using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Reporting.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Reporting.Tests;

public class CategoryMarginsControllerTests : IClassFixture<ReportingApiFixture>
{
    private readonly ReportingApiFixture _fixture;
    private readonly HttpClient _client;

    public CategoryMarginsControllerTests(ReportingApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetRange_ReturnsAggregatedCategoryMargins()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/reports/category-margins");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<CategoryMarginReportDto>();
        result.Should().NotBeNull();
        result!.Categories.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetRange_CalculatesRevenuePercentage()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/reports/category-margins");

        var result = await response.Content.ReadFromJsonAsync<CategoryMarginReportDto>();
        result.Should().NotBeNull();

        // Revenue percentages should sum to approximately 100%
        var totalPercentage = result!.Categories.Sum(c => c.RevenuePercentOfTotal);
        totalPercentage.Should().BeApproximately(100.00m, 1.00m);
    }

    [Fact]
    public async Task GetRange_DefaultSortByRevenue()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/reports/category-margins");

        var result = await response.Content.ReadFromJsonAsync<CategoryMarginReportDto>();
        result.Should().NotBeNull();

        if (result!.Categories.Count > 1)
        {
            result.Categories[0].NetRevenue.Should().BeGreaterThanOrEqualTo(result.Categories[1].NetRevenue);
        }
    }

    [Fact]
    public async Task GetRange_SortByMargin()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/category-margins?sortBy=margin");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<CategoryMarginReportDto>();
        result.Should().NotBeNull();
        result!.Categories.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetRange_SortByItemsSold()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/category-margins?sortBy=itemsSold");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<CategoryMarginReportDto>();
        result.Should().NotBeNull();
        result!.Categories.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetByCategory_ExistingCategory_ReturnsDetails()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/category-margins/{_fixture.TestCategoryId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<CategoryMarginReportDto>();
        result.Should().NotBeNull();
        result!.Categories.Should().HaveCount(1);
        result.Categories[0].CategoryId.Should().Be(_fixture.TestCategoryId);
        result.Categories[0].CategoryName.Should().Be("Food");
    }

    [Fact]
    public async Task GetByCategory_NonExistingCategory_ReturnsNotFound()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/category-margins/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Generate_NewCategorySummary_CreatesRecord()
    {
        var newCategoryId = Guid.NewGuid();
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3));
        var request = new GenerateCategorySummaryRequest(
            Date: newDate,
            CategoryId: newCategoryId,
            CategoryName: "Desserts",
            ItemsSold: 15,
            GrossRevenue: 200.00m,
            DiscountTotal: 5.00m,
            TotalCOGS: 50.00m,
            TotalDayRevenue: 1000.00m
        );

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/category-margins", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CategorySalesSummaryDto>();
        result.Should().NotBeNull();
        result!.CategoryId.Should().Be(newCategoryId);
        result.NetRevenue.Should().Be(195.00m); // 200 - 5
        result.GrossProfit.Should().Be(145.00m); // 195 - 50
    }

    [Fact]
    public async Task Generate_CalculatesMarginMetrics()
    {
        var newCategoryId = Guid.NewGuid();
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-4));
        var request = new GenerateCategorySummaryRequest(
            Date: newDate,
            CategoryId: newCategoryId,
            CategoryName: "Sides",
            ItemsSold: 50,
            GrossRevenue: 250.00m,
            DiscountTotal: 0.00m,
            TotalCOGS: 75.00m, // 30% COGS
            TotalDayRevenue: 1000.00m
        );

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/category-margins", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CategorySalesSummaryDto>();
        result.Should().NotBeNull();
        result!.GrossMarginPercent.Should().Be(70.00m); // (250 - 75) / 250 * 100
        result.RevenuePercentOfTotal.Should().Be(25.00m); // 250 / 1000 * 100
    }

    [Fact]
    public async Task GetRange_IncludesHalLinks()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/reports/category-margins");

        var result = await response.Content.ReadFromJsonAsync<CategoryMarginReportDto>();
        result.Should().NotBeNull();
        result!.Links.Should().ContainKey("self");
    }
}
