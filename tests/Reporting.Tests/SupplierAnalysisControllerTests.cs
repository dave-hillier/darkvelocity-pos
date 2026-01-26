using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Reporting.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Reporting.Tests;

public class SupplierAnalysisControllerTests : IClassFixture<ReportingApiFixture>
{
    private readonly ReportingApiFixture _fixture;
    private readonly HttpClient _client;

    public SupplierAnalysisControllerTests(ReportingApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetRange_ReturnsSupplierAnalysis()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/reports/supplier-analysis");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SupplierAnalysisReportDto>();
        result.Should().NotBeNull();
        result!.Suppliers.Should().NotBeEmpty();
        result.TotalSpend.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetRange_DefaultSortBySpend()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/reports/supplier-analysis");

        var result = await response.Content.ReadFromJsonAsync<SupplierAnalysisReportDto>();
        result.Should().NotBeNull();

        if (result!.Suppliers.Count > 1)
        {
            result.Suppliers[0].TotalSpend.Should().BeGreaterThanOrEqualTo(result.Suppliers[1].TotalSpend);
        }
    }

    [Fact]
    public async Task GetRange_SortByDeliveries()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/supplier-analysis?sortBy=deliveries");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SupplierAnalysisReportDto>();
        result.Should().NotBeNull();
        result!.Suppliers.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetRange_SortByOnTimePercentage()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/supplier-analysis?sortBy=onTime");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SupplierAnalysisReportDto>();
        result.Should().NotBeNull();
        result!.Suppliers.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetBySupplier_ExistingSupplier_ReturnsDetails()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/supplier-analysis/{_fixture.TestSupplierId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SupplierSpendSummaryDto>();
        result.Should().NotBeNull();
        result!.SupplierId.Should().Be(_fixture.TestSupplierId);
        result.SupplierName.Should().Be("Fresh Foods Ltd");
        result.TotalSpend.Should().Be(2500.00m);
    }

    [Fact]
    public async Task GetBySupplier_NonExistingSupplier_ReturnsNotFound()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/supplier-analysis/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Generate_NewSupplierSummary_CreatesRecord()
    {
        var newSupplierId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new GenerateSupplierSummaryRequest(
            PeriodStart: today.AddDays(-7),
            PeriodEnd: today,
            SupplierId: newSupplierId,
            SupplierName: "Quick Supplies",
            TotalSpend: 1500.00m,
            DeliveryCount: 5,
            OnTimeDeliveries: 4,
            LateDeliveries: 1,
            DiscrepancyCount: 0,
            DiscrepancyValue: 0.00m,
            UniqueProductsOrdered: 20
        );

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/supplier-analysis", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<SupplierSpendSummaryDto>();
        result.Should().NotBeNull();
        result!.SupplierId.Should().Be(newSupplierId);
        result.SupplierName.Should().Be("Quick Supplies");
        result.TotalSpend.Should().Be(1500.00m);
    }

    [Fact]
    public async Task Generate_CalculatesPerformanceMetrics()
    {
        var newSupplierId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var request = new GenerateSupplierSummaryRequest(
            PeriodStart: today.AddDays(-14),
            PeriodEnd: today.AddDays(-7),
            SupplierId: newSupplierId,
            SupplierName: "Premium Foods",
            TotalSpend: 3000.00m,
            DeliveryCount: 6,
            OnTimeDeliveries: 5,
            LateDeliveries: 1,
            DiscrepancyCount: 2,
            DiscrepancyValue: 75.00m,
            UniqueProductsOrdered: 25
        );

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/supplier-analysis", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<SupplierSpendSummaryDto>();
        result.Should().NotBeNull();
        result!.AverageDeliveryValue.Should().Be(500.00m); // 3000 / 6
        result.OnTimePercentage.Should().BeApproximately(83.33m, 0.01m); // 5/6 * 100
        result.DiscrepancyRate.Should().BeApproximately(33.33m, 0.01m); // 2/6 * 100
    }

    [Fact]
    public async Task GetRange_IncludesHalLinks()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/reports/supplier-analysis");

        var result = await response.Content.ReadFromJsonAsync<SupplierAnalysisReportDto>();
        result.Should().NotBeNull();
        result!.Links.Should().ContainKey("self");
    }

    [Fact]
    public async Task GetRange_CalculatesTotalSpend()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/reports/supplier-analysis");

        var result = await response.Content.ReadFromJsonAsync<SupplierAnalysisReportDto>();
        result.Should().NotBeNull();
        result!.TotalSpend.Should().Be(result.Suppliers.Sum(s => s.TotalSpend));
    }
}
