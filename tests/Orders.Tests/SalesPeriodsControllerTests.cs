using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Orders.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Orders.Tests;

public class SalesPeriodsControllerTests : IClassFixture<OrdersApiFixture>
{
    private readonly OrdersApiFixture _fixture;
    private readonly HttpClient _client;

    public SalesPeriodsControllerTests(OrdersApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsSalesPeriods()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/sales-periods");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<SalesPeriodDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetCurrent_ReturnsOpenPeriod()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/sales-periods/current");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var period = await response.Content.ReadFromJsonAsync<SalesPeriodDto>();
        period.Should().NotBeNull();
        period!.Status.Should().Be("open");
    }

    [Fact]
    public async Task GetById_ReturnsSalesPeriod()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/sales-periods/{_fixture.TestSalesPeriodId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var period = await response.Content.ReadFromJsonAsync<SalesPeriodDto>();
        period.Should().NotBeNull();
        period!.Id.Should().Be(_fixture.TestSalesPeriodId);
        period.Status.Should().Be("open");
    }

    [Fact]
    public async Task Open_WhenPeriodAlreadyOpen_ReturnsConflict()
    {
        var request = new OpenSalesPeriodRequest(
            UserId: _fixture.TestUserId,
            OpeningCashAmount: 200.00m);

        var response = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/sales-periods", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Open_CreatesNewSalesPeriod()
    {
        // Use a different location to avoid conflict with existing open period
        var newLocationId = Guid.NewGuid();
        var request = new OpenSalesPeriodRequest(
            UserId: _fixture.TestUserId,
            OpeningCashAmount: 150.00m);

        var response = await _client.PostAsJsonAsync($"/api/locations/{newLocationId}/sales-periods", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var period = await response.Content.ReadFromJsonAsync<SalesPeriodDto>();
        period.Should().NotBeNull();
        period!.OpeningCashAmount.Should().Be(150.00m);
        period.Status.Should().Be("open");
    }

    [Fact]
    public async Task Close_ClosesSalesPeriod()
    {
        // First create a new period in a different location
        var newLocationId = Guid.NewGuid();
        var openRequest = new OpenSalesPeriodRequest(_fixture.TestUserId, 100.00m);
        var openResponse = await _client.PostAsJsonAsync($"/api/locations/{newLocationId}/sales-periods", openRequest);
        var openedPeriod = await openResponse.Content.ReadFromJsonAsync<SalesPeriodDto>();

        // Now close it
        var closeRequest = new CloseSalesPeriodRequest(
            UserId: _fixture.TestUserId,
            ClosingCashAmount: 250.00m);

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/sales-periods/{openedPeriod!.Id}/close",
            closeRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var closedPeriod = await response.Content.ReadFromJsonAsync<SalesPeriodDto>();
        closedPeriod.Should().NotBeNull();
        closedPeriod!.Status.Should().Be("closed");
        closedPeriod.ClosingCashAmount.Should().Be(250.00m);
        closedPeriod.ClosedAt.Should().NotBeNull();
    }
}
