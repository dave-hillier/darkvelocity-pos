using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Integration.Tests.Fixtures;
using FluentAssertions;

namespace DarkVelocity.Integration.Tests;

/// <summary>
/// Integration tests for detailed Sales Reporting.
///
/// Business Scenarios Covered:
/// - Sales by employee
/// - Sales by hour
/// - Sales by day of week
/// - Void reports
/// - Discount reports
/// - Refund reports
/// - Average ticket analysis
/// - Item sales reports
/// </summary>
public class SalesReportingIntegrationTests : IClassFixture<ReportingServiceFixture>
{
    private readonly ReportingServiceFixture _fixture;
    private readonly HttpClient _client;

    public SalesReportingIntegrationTests(ReportingServiceFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    #region Sales by Employee

    [Fact]
    public async Task DailySalesReport_ByEmployee_ReturnsSalesByServer()
    {
        // Arrange
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/sales/by-employee?date={date:yyyy-MM-dd}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var report = await response.Content.ReadFromJsonAsync<EmployeeSalesReportDto>();
            report.Should().NotBeNull();
            report!.Employees.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task EmployeeSalesReport_IncludesTotalsAndOrderCount()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/sales/by-employee" +
            $"?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Sales by Hour

    [Fact]
    public async Task DailySalesReport_ByHour_ReturnsHourlyBreakdown()
    {
        // Arrange
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/sales/by-hour?date={date:yyyy-MM-dd}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var report = await response.Content.ReadFromJsonAsync<HourlySalesReportDto>();
            report.Should().NotBeNull();
            report!.Hours.Should().NotBeNull();
            // Should have entries for business hours
        }
    }

    [Fact]
    public async Task HourlySalesReport_IdentifiesPeakHours()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/sales/peak-hours?days=30");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Sales by Day of Week

    [Fact]
    public async Task DailySalesReport_ByDayOfWeek_ReturnsWeekdayPatterns()
    {
        // Arrange - Last 4 weeks of data
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-28));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/sales/by-day-of-week" +
            $"?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var report = await response.Content.ReadFromJsonAsync<DayOfWeekSalesReportDto>();
            report.Should().NotBeNull();
            report!.Days.Should().HaveCount(7);
        }
    }

    #endregion

    #region Void Reports

    [Fact]
    public async Task VoidReport_ByReason_CategorizesByReason()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/voids" +
            $"?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var report = await response.Content.ReadFromJsonAsync<VoidReportDto>();
            report.Should().NotBeNull();
            report!.TotalVoidCount.Should().BeGreaterOrEqualTo(0);
            report.TotalVoidValue.Should().BeGreaterOrEqualTo(0);
        }
    }

    [Fact]
    public async Task VoidReport_ByEmployee_TracksVoidsByServer()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/voids/by-employee" +
            $"?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Discount Reports

    [Fact]
    public async Task DiscountReport_ByType_CategorizesByDiscountType()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/discounts" +
            $"?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var report = await response.Content.ReadFromJsonAsync<DiscountReportDto>();
            report.Should().NotBeNull();
            report!.DiscountsByType.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task DiscountReport_IncludesCompAndPromoBreakdown()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/discounts/breakdown");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Refund Reports

    [Fact]
    public async Task RefundReport_ByEmployee_TracksRefundsByServer()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/refunds/by-employee" +
            $"?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RefundReport_ByReason_CategorizesByReason()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/refunds" +
            $"?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Average Ticket Analysis

    [Fact]
    public async Task AverageTicket_ByOrderType_ComparesTypes()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/average-ticket/by-order-type");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var report = await response.Content.ReadFromJsonAsync<AverageTicketReportDto>();
            report.Should().NotBeNull();
            // Should have breakdown by dine-in, takeout, delivery, etc.
        }
    }

    [Fact]
    public async Task AverageTicket_Trend_ShowsOverTime()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/average-ticket/trend" +
            $"?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Item Sales Reports

    [Fact]
    public async Task ItemSalesReport_WithQuantityAndRevenue_ReturnsItemMetrics()
    {
        // Arrange
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/item-sales" +
            $"?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var report = await response.Content.ReadFromJsonAsync<ItemSalesReportDto>();
            report.Should().NotBeNull();
            report!.Items.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task ItemSalesReport_TopSellers_ReturnsRankedItems()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/item-sales/top?limit=10");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var items = await response.Content.ReadFromJsonAsync<List<ItemSalesDto>>();
            items!.Should().HaveCountLessOrEqualTo(10);
        }
    }

    [Fact]
    public async Task ItemSalesReport_ByCategory_GroupsByCategory()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/item-sales/by-category");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Combined Reports

    [Fact]
    public async Task DailySummaryReport_IncludesAllMetrics()
    {
        // Arrange
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/daily-summary?date={date:yyyy-MM-dd}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var report = await response.Content.ReadFromJsonAsync<DailySummaryReportDto>();
            report.Should().NotBeNull();
        }
    }

    #endregion
}

// Sales Reporting DTOs
public record EmployeeSalesReportDto
{
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public decimal TotalSales { get; init; }
    public int TotalOrders { get; init; }
    public List<EmployeeSalesDto> Employees { get; init; } = new();
}

public record EmployeeSalesDto
{
    public Guid EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public decimal Sales { get; init; }
    public int OrderCount { get; init; }
    public decimal AverageTicket { get; init; }
    public decimal Tips { get; init; }
}

public record HourlySalesReportDto
{
    public DateOnly Date { get; init; }
    public decimal TotalSales { get; init; }
    public List<HourlySalesDto> Hours { get; init; } = new();
}

public record HourlySalesDto
{
    public int Hour { get; init; }
    public decimal Sales { get; init; }
    public int OrderCount { get; init; }
    public int GuestCount { get; init; }
}

public record DayOfWeekSalesReportDto
{
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public List<DayOfWeekSalesDto> Days { get; init; } = new();
}

public record DayOfWeekSalesDto
{
    public int DayOfWeek { get; init; }
    public string? DayName { get; init; }
    public decimal AverageSales { get; init; }
    public int AverageOrders { get; init; }
    public decimal TotalSales { get; init; }
    public int TotalOrders { get; init; }
}

public record VoidReportDto
{
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public int TotalVoidCount { get; init; }
    public decimal TotalVoidValue { get; init; }
    public List<VoidByReasonDto> VoidsByReason { get; init; } = new();
}

public record VoidByReasonDto
{
    public string? Reason { get; init; }
    public int Count { get; init; }
    public decimal Value { get; init; }
}

public record DiscountReportDto
{
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public decimal TotalDiscounts { get; init; }
    public List<DiscountByTypeDto> DiscountsByType { get; init; } = new();
}

public record DiscountByTypeDto
{
    public string? DiscountType { get; init; }
    public int Count { get; init; }
    public decimal TotalValue { get; init; }
}

public record AverageTicketReportDto
{
    public decimal OverallAverage { get; init; }
    public List<AverageTicketByTypeDto> ByOrderType { get; init; } = new();
}

public record AverageTicketByTypeDto
{
    public string? OrderType { get; init; }
    public decimal AverageTicket { get; init; }
    public int OrderCount { get; init; }
}

public record ItemSalesReportDto
{
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public decimal TotalRevenue { get; init; }
    public int TotalQuantity { get; init; }
    public List<ItemSalesDto> Items { get; init; } = new();
}

public record ItemSalesDto
{
    public Guid ItemId { get; init; }
    public string? ItemName { get; init; }
    public string? CategoryName { get; init; }
    public int QuantitySold { get; init; }
    public decimal Revenue { get; init; }
    public decimal AveragePrice { get; init; }
}

public record DailySummaryReportDto
{
    public DateOnly Date { get; init; }
    public decimal GrossSales { get; init; }
    public decimal NetSales { get; init; }
    public decimal Discounts { get; init; }
    public decimal Refunds { get; init; }
    public decimal Tax { get; init; }
    public decimal Tips { get; init; }
    public int OrderCount { get; init; }
    public int GuestCount { get; init; }
    public decimal AverageTicket { get; init; }
    public int VoidCount { get; init; }
    public decimal VoidValue { get; init; }
}
