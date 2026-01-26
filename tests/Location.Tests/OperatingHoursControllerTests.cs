using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Location.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Location.Tests;

public class OperatingHoursControllerTests : IClassFixture<LocationApiFixture>
{
    private readonly LocationApiFixture _fixture;
    private readonly HttpClient _client;

    public OperatingHoursControllerTests(LocationApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsAllDays()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/hours");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<OperatingHoursDto>>();
        result.Should().NotBeNull();
        result.Should().HaveCount(7); // All days of week
    }

    [Fact]
    public async Task GetAll_OrderedByDayOfWeek()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/hours");

        var result = await response.Content.ReadFromJsonAsync<List<OperatingHoursDto>>();
        result.Should().NotBeNull();

        var days = result!.Select(h => h.DayOfWeek).ToList();
        days.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetAll_NonExistingLocation_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/locations/{Guid.NewGuid()}/hours");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByDay_ExistingDay_ReturnsHours()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/hours/Monday");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<OperatingHoursDto>();
        result.Should().NotBeNull();
        result!.DayOfWeek.Should().Be(DayOfWeek.Monday);
        result.OpenTime.Should().Be(new TimeOnly(11, 0));
        result.CloseTime.Should().Be(new TimeOnly(22, 0));
    }

    [Fact]
    public async Task GetByDay_WeekendHours_ReturnsDifferentTimes()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/hours/Saturday");

        var result = await response.Content.ReadFromJsonAsync<OperatingHoursDto>();
        result.Should().NotBeNull();
        result!.OpenTime.Should().Be(new TimeOnly(10, 0));
        result.CloseTime.Should().Be(new TimeOnly(23, 0));
    }

    [Fact]
    public async Task GetByDay_NonExistingLocation_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/locations/{Guid.NewGuid()}/hours/Monday");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Set_NewDay_CreatesHours()
    {
        // Create a location without hours
        var createRequest = new CreateLocationRequest(
            Name: "No Hours Location",
            Code: "NHR-01",
            Timezone: "UTC",
            CurrencyCode: "USD"
        );
        var createResponse = await _client.PostAsJsonAsync("/api/locations", createRequest);
        var location = await createResponse.Content.ReadFromJsonAsync<LocationDto>();

        // Set hours for Monday
        var hoursRequest = new SetOperatingHoursRequest(
            DayOfWeek: DayOfWeek.Monday,
            OpenTime: new TimeOnly(9, 0),
            CloseTime: new TimeOnly(17, 0)
        );

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{location!.Id}/hours/Monday", hoursRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<OperatingHoursDto>();
        result.Should().NotBeNull();
        result!.OpenTime.Should().Be(new TimeOnly(9, 0));
        result.CloseTime.Should().Be(new TimeOnly(17, 0));
    }

    [Fact]
    public async Task Set_ExistingDay_UpdatesHours()
    {
        var hoursRequest = new SetOperatingHoursRequest(
            DayOfWeek: DayOfWeek.Monday,
            OpenTime: new TimeOnly(8, 0),
            CloseTime: new TimeOnly(20, 0)
        );

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/hours/Monday", hoursRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<OperatingHoursDto>();
        result.Should().NotBeNull();
        result!.OpenTime.Should().Be(new TimeOnly(8, 0));
        result.CloseTime.Should().Be(new TimeOnly(20, 0));
    }

    [Fact]
    public async Task Set_ClosedDay_SetsIsClosed()
    {
        var hoursRequest = new SetOperatingHoursRequest(
            DayOfWeek: DayOfWeek.Sunday,
            OpenTime: new TimeOnly(0, 0),
            CloseTime: new TimeOnly(0, 0),
            IsClosed: true
        );

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/hours/Sunday", hoursRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<OperatingHoursDto>();
        result.Should().NotBeNull();
        result!.IsClosed.Should().BeTrue();
    }

    [Fact]
    public async Task Set_NonExistingLocation_ReturnsNotFound()
    {
        var hoursRequest = new SetOperatingHoursRequest(
            DayOfWeek: DayOfWeek.Monday,
            OpenTime: new TimeOnly(9, 0),
            CloseTime: new TimeOnly(17, 0)
        );

        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{Guid.NewGuid()}/hours/Monday", hoursRequest);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SetBulk_MultipleDay_SetsAll()
    {
        // Create a location without hours
        var createRequest = new CreateLocationRequest(
            Name: "Bulk Hours Location",
            Code: "BLK-01",
            Timezone: "UTC",
            CurrencyCode: "USD"
        );
        var createResponse = await _client.PostAsJsonAsync("/api/locations", createRequest);
        var location = await createResponse.Content.ReadFromJsonAsync<LocationDto>();

        var bulkRequest = new List<SetOperatingHoursRequest>
        {
            new(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
            new(DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
            new(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(17, 0)),
            new(DayOfWeek.Saturday, new TimeOnly(0, 0), new TimeOnly(0, 0), true),
            new(DayOfWeek.Sunday, new TimeOnly(0, 0), new TimeOnly(0, 0), true)
        };

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{location!.Id}/hours/bulk", bulkRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<OperatingHoursDto>>();
        result.Should().NotBeNull();
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task Delete_ExistingDay_RemovesHours()
    {
        // First ensure hours exist
        var hoursRequest = new SetOperatingHoursRequest(
            DayOfWeek: DayOfWeek.Friday,
            OpenTime: new TimeOnly(11, 0),
            CloseTime: new TimeOnly(22, 0)
        );
        await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/hours/Friday", hoursRequest);

        // Delete
        var response = await _client.DeleteAsync(
            $"/api/locations/{_fixture.TestLocationId}/hours/Friday");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deleted
        var getResponse = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/hours/Friday");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_NonExistingDay_ReturnsNotFound()
    {
        // Create location without hours
        var createRequest = new CreateLocationRequest(
            Name: "Empty Hours Location",
            Code: "EMP-01",
            Timezone: "UTC",
            CurrencyCode: "USD"
        );
        var createResponse = await _client.PostAsJsonAsync("/api/locations", createRequest);
        var location = await createResponse.Content.ReadFromJsonAsync<LocationDto>();

        var response = await _client.DeleteAsync(
            $"/api/locations/{location!.Id}/hours/Monday");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetToday_ReturnsCurrentDayHours()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/hours/today");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<OperatingHoursDto>();
        result.Should().NotBeNull();
        result!.DayName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetToday_NonExistingLocation_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/locations/{Guid.NewGuid()}/hours/today");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByDay_IncludesHalLinks()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/hours/Monday");

        var result = await response.Content.ReadFromJsonAsync<OperatingHoursDto>();
        result.Should().NotBeNull();
        result!.Links.Should().ContainKey("self");
    }

    [Fact]
    public async Task GetByDay_IncludesDayName()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/hours/Monday");

        var result = await response.Content.ReadFromJsonAsync<OperatingHoursDto>();
        result.Should().NotBeNull();
        result!.DayName.Should().Be("Monday");
    }
}
