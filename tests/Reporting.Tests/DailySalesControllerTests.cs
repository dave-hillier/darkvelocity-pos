using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Reporting.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Reporting.Tests;

public class DailySalesControllerTests : IClassFixture<ReportingApiFixture>
{
    private readonly ReportingApiFixture _fixture;
    private readonly HttpClient _client;

    public DailySalesControllerTests(ReportingApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetRange_ReturnsAllDaysInRange()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/reports/daily-sales");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<DailySalesRangeDto>();
        result.Should().NotBeNull();
        result!.Days.Should().NotBeEmpty();
        result.Totals.Should().NotBeNull();
    }

    [Fact]
    public async Task GetRange_WithDateFilter_ReturnsFilteredResults()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayStr = today.ToString("yyyy-MM-dd");

        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/daily-sales?startDate={todayStr}&endDate={todayStr}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<DailySalesRangeDto>();
        result.Should().NotBeNull();
        result!.Days.Should().HaveCount(1);
        result.Days[0].Date.Should().Be(today);
    }

    [Fact]
    public async Task GetRange_CalculatesTotals()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/reports/daily-sales");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<DailySalesRangeDto>();
        result.Should().NotBeNull();
        result!.Totals.GrossRevenue.Should().BeGreaterThan(0);
        result.Totals.NetRevenue.Should().BeGreaterThan(0);
        result.Totals.GrossProfit.Should().BeGreaterThan(0);
        result.Totals.OrderCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetByDate_ExistingDate_ReturnsSummary()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayStr = today.ToString("yyyy-MM-dd");

        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/daily-sales/{todayStr}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<DailySalesSummaryDto>();
        result.Should().NotBeNull();
        result!.Date.Should().Be(today);
        result.GrossRevenue.Should().BeGreaterThanOrEqualTo(1500.00m);
    }

    [Fact]
    public async Task GetByDate_NonExistingDate_ReturnsNotFound()
    {
        var farFuture = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(10));
        var farFutureStr = farFuture.ToString("yyyy-MM-dd");

        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/daily-sales/{farFutureStr}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Generate_NewSummary_CreatesRecord()
    {
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10));
        var request = new GenerateDailySummaryRequest(
            Date: newDate,
            GrossRevenue: 800.00m,
            DiscountTotal: 20.00m,
            TaxCollected: 78.00m,
            TotalCOGS: 250.00m,
            OrderCount: 25,
            ItemsSold: 60,
            TipsCollected: 40.00m,
            CashTotal: 350.00m,
            CardTotal: 420.00m,
            OtherPaymentTotal: 30.00m,
            RefundCount: 1,
            RefundTotal: 10.00m
        );

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/daily-sales", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<DailySalesSummaryDto>();
        result.Should().NotBeNull();
        result!.Date.Should().Be(newDate);
        result.GrossRevenue.Should().Be(800.00m);
        result.NetRevenue.Should().Be(780.00m); // 800 - 20
        result.GrossProfit.Should().Be(530.00m); // 780 - 250
    }

    [Fact]
    public async Task Generate_ExistingSummary_UpdatesRecord()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new GenerateDailySummaryRequest(
            Date: today,
            GrossRevenue: 2000.00m, // Updated value
            DiscountTotal: 100.00m,
            TaxCollected: 190.00m,
            TotalCOGS: 650.00m,
            OrderCount: 65,
            ItemsSold: 150,
            TipsCollected: 100.00m,
            CashTotal: 800.00m,
            CardTotal: 1050.00m,
            OtherPaymentTotal: 50.00m,
            RefundCount: 3,
            RefundTotal: 35.00m
        );

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/daily-sales", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<DailySalesSummaryDto>();
        result.Should().NotBeNull();
        result!.GrossRevenue.Should().Be(2000.00m);
    }

    [Fact]
    public async Task Generate_CalculatesMargins()
    {
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-15));
        var request = new GenerateDailySummaryRequest(
            Date: newDate,
            GrossRevenue: 1000.00m,
            DiscountTotal: 0.00m,
            TaxCollected: 100.00m,
            TotalCOGS: 300.00m, // 30% COGS
            OrderCount: 20,
            ItemsSold: 40,
            TipsCollected: 50.00m,
            CashTotal: 500.00m,
            CardTotal: 500.00m,
            OtherPaymentTotal: 0.00m,
            RefundCount: 0,
            RefundTotal: 0.00m
        );

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/daily-sales", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<DailySalesSummaryDto>();
        result.Should().NotBeNull();
        result!.GrossMarginPercent.Should().Be(70.00m); // (1000 - 300) / 1000 * 100
        result.AverageOrderValue.Should().Be(50.00m); // 1000 / 20
    }

    [Fact]
    public async Task GetRange_IncludesHalLinks()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/reports/daily-sales");

        var result = await response.Content.ReadFromJsonAsync<DailySalesRangeDto>();
        result.Should().NotBeNull();
        result!.Links.Should().ContainKey("self");
    }
}
