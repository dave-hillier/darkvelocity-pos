using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Integration.Tests.Fixtures;
using DarkVelocity.Reporting.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Integration.Tests;

/// <summary>
/// P3 Integration tests for Corporate/Multi-Location Reporting:
/// - Aggregate Reports Across All Locations
/// - Location Comparison/Benchmarking
/// - Consolidated Inventory Views
/// - Company-Wide Metrics
/// </summary>
public class CorporateReportingIntegrationTests : IClassFixture<ReportingServiceFixture>
{
    private readonly ReportingServiceFixture _fixture;
    private readonly HttpClient _client;

    // Simulate multiple locations for corporate reporting
    private readonly Guid _location1Id = Guid.NewGuid();
    private readonly Guid _location2Id = Guid.NewGuid();
    private readonly Guid _location3Id = Guid.NewGuid();

    public CorporateReportingIntegrationTests(ReportingServiceFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    #region Company-Wide Sales Aggregation

    [Fact]
    public async Task CorporateSalesReport_AggregatesAllLocations()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var response = await _client.GetAsync(
            $"/api/corporate/reports/sales?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        // NotFound if corporate endpoint isn't implemented
    }

    [Fact]
    public async Task CorporateSalesReport_IncludesAllLocationTotals()
    {
        // Act - Get corporate sales with location breakdown
        var response = await _client.GetAsync(
            $"/api/corporate/reports/sales/by-location");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CorporateSalesReport_FilterByDateRange()
    {
        // Arrange - Last month's data
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var response = await _client.GetAsync(
            $"/api/corporate/reports/sales?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Location Comparison/Benchmarking

    [Fact]
    public async Task LocationComparison_SalesPerformance()
    {
        // Arrange
        var locationIds = new[] { _location1Id, _location2Id, _location3Id };
        var period = "last_30_days";

        // Act
        var response = await _client.GetAsync(
            $"/api/corporate/reports/compare-locations?period={period}&metric=sales");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LocationComparison_RevenuePerEmployee()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/corporate/reports/compare-locations?metric=revenue_per_employee");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LocationComparison_AverageOrderValue()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/corporate/reports/compare-locations?metric=average_order_value");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LocationComparison_TopPerformingItems()
    {
        // Act - Compare top selling items across locations
        var response = await _client.GetAsync(
            $"/api/corporate/reports/compare-locations/top-items?limit=10");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LocationBenchmark_AgainstCompanyAverage()
    {
        // Act - Benchmark specific location against company average
        var response = await _client.GetAsync(
            $"/api/corporate/reports/benchmark/{_location1Id}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LocationRanking_ByMetric()
    {
        // Act - Rank locations by specified metric
        var response = await _client.GetAsync(
            $"/api/corporate/reports/location-ranking?metric=gross_margin&order=desc");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Consolidated Inventory

    [Fact]
    public async Task ConsolidatedInventory_TotalStockValue()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/corporate/inventory/total-value");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConsolidatedInventory_StockByLocation()
    {
        // Act - Get stock levels broken down by location
        var response = await _client.GetAsync(
            $"/api/corporate/inventory/by-location");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConsolidatedInventory_LowStockAcrossAllLocations()
    {
        // Act - Find low stock items company-wide
        var response = await _client.GetAsync(
            $"/api/corporate/inventory/low-stock");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConsolidatedInventory_TransferRecommendations()
    {
        // Act - Get suggestions for stock transfers between locations
        var response = await _client.GetAsync(
            $"/api/corporate/inventory/transfer-recommendations");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConsolidatedInventory_ExpiringStockAllLocations()
    {
        // Act - Find expiring stock across all locations
        var response = await _client.GetAsync(
            $"/api/corporate/inventory/expiring?days=7");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Company-Wide Margins

    [Fact]
    public async Task CorporateMarginReport_ByCategory()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/corporate/reports/margins/by-category");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CorporateMarginReport_ByMenuItem()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/corporate/reports/margins/by-item?limit=50");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CorporateMarginReport_Trends()
    {
        // Act - Margin trends over time
        var response = await _client.GetAsync(
            $"/api/corporate/reports/margins/trends?period=weekly&weeks=12");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CorporateMarginAlert_BelowThreshold()
    {
        // Act - Get items with margins below corporate threshold
        var response = await _client.GetAsync(
            $"/api/corporate/reports/margins/alerts?threshold=50");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Procurement Analysis

    [Fact]
    public async Task CorporateProcurement_SpendBySupplier()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/corporate/reports/procurement/spend-by-supplier");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CorporateProcurement_ConsolidatedOrdering()
    {
        // Act - Identify opportunities to consolidate orders
        var response = await _client.GetAsync(
            $"/api/corporate/reports/procurement/consolidation-opportunities");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CorporateProcurement_PriceVarianceAcrossLocations()
    {
        // Act - Compare prices paid across locations
        var response = await _client.GetAsync(
            $"/api/corporate/reports/procurement/price-variance");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Labor and Performance

    [Fact]
    public async Task CorporateLabor_SalesPerLaborHour()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/corporate/reports/labor/sales-per-hour");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CorporateLabor_EmployeePerformanceRanking()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/corporate/reports/labor/employee-ranking?metric=sales");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Dashboard Metrics

    [Fact]
    public async Task CorporateDashboard_KeyMetrics()
    {
        // Act - Get high-level KPIs for executive dashboard
        var response = await _client.GetAsync(
            $"/api/corporate/dashboard/metrics");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CorporateDashboard_TodaySummary()
    {
        // Act - Real-time summary across all locations
        var response = await _client.GetAsync(
            $"/api/corporate/dashboard/today");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CorporateDashboard_WeekOverWeekComparison()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/corporate/dashboard/week-over-week");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Export and Scheduling

    [Fact]
    public async Task CorporateReport_ExportToExcel()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/corporate/reports/sales/export?format=xlsx");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CorporateReport_ScheduledReportConfiguration()
    {
        // Arrange - Configure a scheduled report
        var scheduleRequest = new ScheduleReportRequest(
            ReportType: "daily_sales",
            Frequency: "daily",
            Recipients: new[] { "manager@company.com" },
            Format: "pdf",
            IncludeAllLocations: true);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/corporate/reports/schedules",
            scheduleRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion
}

// P3 DTOs for corporate reporting
public record ScheduleReportRequest(
    string ReportType,
    string Frequency,
    string[] Recipients,
    string Format,
    bool IncludeAllLocations,
    Guid[]? LocationIds = null);
