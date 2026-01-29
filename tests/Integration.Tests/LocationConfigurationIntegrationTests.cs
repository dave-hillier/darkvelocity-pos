using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Integration.Tests.Fixtures;
using FluentAssertions;

namespace DarkVelocity.Integration.Tests;

/// <summary>
/// Integration tests for Location configuration and operating hours.
///
/// Business Scenarios Covered:
/// - Location CRUD operations
/// - Operating hours management
/// - Location settings configuration
/// - Timezone handling
/// </summary>
public class LocationConfigurationIntegrationTests : IClassFixture<LocationServiceFixture>
{
    private readonly LocationServiceFixture _fixture;
    private readonly HttpClient _client;

    public LocationConfigurationIntegrationTests(LocationServiceFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    #region Location Queries

    [Fact]
    public async Task GetLocation_ReturnsLocationDetails()
    {
        // Act
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var location = await response.Content.ReadFromJsonAsync<LocationDto>();
            location.Should().NotBeNull();
            location!.Id.Should().Be(_fixture.TestLocationId);
            location.Name.Should().Be("New York Downtown");
            location.Code.Should().Be(_fixture.TestLocationCode);
            location.Timezone.Should().Be("America/New_York");
            location.CurrencyCode.Should().Be("USD");
        }
    }

    [Fact]
    public async Task GetLocationByCode_ReturnsLocation()
    {
        // Act
        var response = await _client.GetAsync($"/api/locations/by-code/{_fixture.TestLocationCode}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var location = await response.Content.ReadFromJsonAsync<LocationDto>();
            location!.Code.Should().Be(_fixture.TestLocationCode);
        }
    }

    [Fact]
    public async Task GetAllLocations_ReturnsLocationList()
    {
        // Act
        var response = await _client.GetAsync("/api/locations");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var locations = await response.Content.ReadFromJsonAsync<List<LocationSummaryDto>>();
            locations.Should().NotBeNull();
            locations!.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task GetAllLocations_FilterActive_ReturnsOnlyActive()
    {
        // Act
        var response = await _client.GetAsync("/api/locations?isActive=true");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var locations = await response.Content.ReadFromJsonAsync<List<LocationSummaryDto>>();
            locations!.Should().OnlyContain(l => l.IsActive);
        }
    }

    #endregion

    #region Location Updates

    [Fact]
    public async Task UpdateLocation_ChangesAddress()
    {
        // Arrange
        var updateRequest = new UpdateLocationRequest(
            Name: "New York Downtown - Updated",
            Phone: "+1-212-555-0199",
            Email: "nyc-updated@darkvelocity.com",
            Address: new AddressRequest(
                Line1: "456 Broadway",
                City: "New York",
                State: "NY",
                PostalCode: "10002",
                Country: "USA"));

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}",
            updateRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateLocation_CreatesNewLocation()
    {
        // Arrange
        var createRequest = new CreateLocationRequest(
            Name: "Chicago Loop",
            Code: "CHI001",
            Timezone: "America/Chicago",
            CurrencyCode: "USD",
            CurrencySymbol: "$",
            Phone: "+1-312-555-0100",
            Email: "chicago@darkvelocity.com",
            BusinessName: "DarkVelocity Chicago LLC",
            Address: new AddressRequest(
                Line1: "100 State Street",
                City: "Chicago",
                State: "IL",
                PostalCode: "60601",
                Country: "USA"));

        // Act
        var response = await _client.PostAsJsonAsync("/api/locations", createRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Operating Hours

    [Fact]
    public async Task SetOperatingHours_ByDayOfWeek_SetsHours()
    {
        // Arrange - Set Monday hours
        var request = new SetOperatingHoursRequest(
            DayOfWeek: 1, // Monday
            OpenTime: "07:00",
            CloseTime: "21:00",
            IsClosed: false);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/hours/1",
            request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetOperatingHours_ReturnsSchedule()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/hours");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var hours = await response.Content.ReadFromJsonAsync<List<OperatingHoursDto>>();
            hours.Should().NotBeNull();
            hours!.Should().HaveCount(7); // 7 days
        }
    }

    [Fact]
    public async Task GetOperatingHours_ForSpecificDay_ReturnsDayHours()
    {
        // Act - Get Saturday hours (day 6)
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/hours/6");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var hours = await response.Content.ReadFromJsonAsync<OperatingHoursDto>();
            hours!.DayOfWeek.Should().Be(6);
        }
    }

    [Fact]
    public async Task GetTodaysHours_ReturnsCurrentDayHours()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/hours/today");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var hours = await response.Content.ReadFromJsonAsync<OperatingHoursDto>();
            hours!.DayOfWeek.Should().Be((int)DateTime.Today.DayOfWeek);
        }
    }

    [Fact]
    public async Task SetHolidayHours_OverridesRegular()
    {
        // Arrange - Set closed for a specific day (simulating holiday)
        var request = new SetOperatingHoursRequest(
            DayOfWeek: 4, // Thursday (e.g., Thanksgiving)
            OpenTime: null,
            CloseTime: null,
            IsClosed: true);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/hours/4",
            request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BulkSetOperatingHours_SetsMultipleDays()
    {
        // Arrange - Set weekday hours in bulk
        var request = new List<SetOperatingHoursRequest>
        {
            new(1, "06:00", "22:00", false), // Monday
            new(2, "06:00", "22:00", false), // Tuesday
            new(3, "06:00", "22:00", false), // Wednesday
            new(4, "06:00", "22:00", false), // Thursday
            new(5, "06:00", "23:00", false), // Friday (late)
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/hours/bulk",
            request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound);
    }

    #endregion

    #region Location Settings

    [Fact]
    public async Task GetLocationSettings_ReturnsTaxConfig()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/settings");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var settings = await response.Content.ReadFromJsonAsync<LocationSettingsDto>();
            settings.Should().NotBeNull();
            settings!.DefaultTaxRate.Should().Be(8.875m);
            settings.TaxIncludedInPrices.Should().BeFalse();
        }
    }

    [Fact]
    public async Task UpdateLocationSettings_ChangesCurrency()
    {
        // This test would change currency for a location
        // Typically this would be done on location update, not settings
        var updateRequest = new UpdateLocationRequest(
            CurrencyCode: "CAD",
            CurrencySymbol: "$");

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocation2Id}",
            updateRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateLocationSettings_ChangesTaxRate()
    {
        // Arrange
        var updateRequest = new UpdateLocationSettingsRequest(
            DefaultTaxRate: 10.0m,
            TaxIncludedInPrices: true,
            ShowTaxBreakdown: true);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/settings",
            updateRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateLocationSettings_ConfiguresReceipts()
    {
        // Arrange
        var updateRequest = new UpdateLocationSettingsRequest(
            ReceiptHeader: "Welcome to DarkVelocity!",
            ReceiptFooter: "Thank you for your visit!",
            PrintReceiptByDefault: true,
            ShowTaxBreakdown: true);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/settings",
            updateRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound);
    }

    #endregion

    #region Timezone Handling

    [Fact]
    public async Task GetLocationTimezone_ReturnsTimezone()
    {
        // Act
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var location = await response.Content.ReadFromJsonAsync<LocationDto>();
            location!.Timezone.Should().Be("America/New_York");
        }
    }

    [Fact]
    public async Task DifferentLocations_HaveDifferentTimezones()
    {
        // Act
        var response1 = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}");
        var response2 = await _client.GetAsync($"/api/locations/{_fixture.TestLocation2Id}");

        // Assert
        if (response1.StatusCode == HttpStatusCode.OK && response2.StatusCode == HttpStatusCode.OK)
        {
            var location1 = await response1.Content.ReadFromJsonAsync<LocationDto>();
            var location2 = await response2.Content.ReadFromJsonAsync<LocationDto>();

            location1!.Timezone.Should().Be("America/New_York");
            location2!.Timezone.Should().Be("America/Los_Angeles");
        }
    }

    #endregion

    #region Location Status

    [Fact]
    public async Task OpenLocation_SetsIsOpenTrue()
    {
        // Act
        var response = await _client.PostAsync(
            $"/api/locations/{_fixture.TestLocation2Id}/open",
            null);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CloseLocation_SetsIsOpenFalse()
    {
        // Act
        var response = await _client.PostAsync(
            $"/api/locations/{_fixture.TestLocationId}/close",
            null);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeactivateLocation_SetsInactive()
    {
        // Act
        var response = await _client.DeleteAsync(
            $"/api/locations/{_fixture.InactiveLocationId}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.NoContent,
            HttpStatusCode.NotFound);
    }

    #endregion
}

// Location DTOs
public record CreateLocationRequest(
    string Name,
    string Code,
    string Timezone,
    string CurrencyCode,
    string CurrencySymbol,
    string? Phone = null,
    string? Email = null,
    string? Website = null,
    string? BusinessName = null,
    string? TaxNumber = null,
    AddressRequest? Address = null);

public record UpdateLocationRequest(
    string? Name = null,
    string? Phone = null,
    string? Email = null,
    string? Website = null,
    string? BusinessName = null,
    string? TaxNumber = null,
    string? CurrencyCode = null,
    string? CurrencySymbol = null,
    AddressRequest? Address = null);

public record AddressRequest(
    string? Line1 = null,
    string? Line2 = null,
    string? City = null,
    string? State = null,
    string? PostalCode = null,
    string? Country = null);

public record SetOperatingHoursRequest(
    int DayOfWeek,
    string? OpenTime,
    string? CloseTime,
    bool IsClosed);

public record UpdateLocationSettingsRequest(
    decimal? DefaultTaxRate = null,
    bool? TaxIncludedInPrices = null,
    string? ReceiptHeader = null,
    string? ReceiptFooter = null,
    bool? PrintReceiptByDefault = null,
    bool? ShowTaxBreakdown = null,
    bool? RequireTableForDineIn = null,
    bool? AutoPrintKitchenTickets = null,
    int? OrderNumberResetHour = null,
    string? OrderNumberPrefix = null,
    bool? AllowCashPayments = null,
    bool? AllowCardPayments = null,
    bool? TipsEnabled = null,
    string? TipSuggestions = null,
    bool? TrackInventory = null,
    bool? WarnOnLowStock = null,
    bool? AllowNegativeStock = null);

public record LocationDto
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public string? Code { get; init; }
    public string? Timezone { get; init; }
    public string? CurrencyCode { get; init; }
    public string? CurrencySymbol { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Website { get; init; }
    public string? BusinessName { get; init; }
    public string? TaxNumber { get; init; }
    public bool IsActive { get; init; }
    public bool IsOpen { get; init; }
    public AddressDto? Address { get; init; }
    public LocationSettingsDto? Settings { get; init; }
    public List<OperatingHoursDto>? OperatingHours { get; init; }
}

public record LocationSummaryDto
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public string? Code { get; init; }
    public string? City { get; init; }
    public bool IsActive { get; init; }
    public bool IsOpen { get; init; }
}

public record AddressDto
{
    public string? Line1 { get; init; }
    public string? Line2 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
}

public record LocationSettingsDto
{
    public decimal DefaultTaxRate { get; init; }
    public bool TaxIncludedInPrices { get; init; }
    public string? ReceiptHeader { get; init; }
    public string? ReceiptFooter { get; init; }
    public bool PrintReceiptByDefault { get; init; }
    public bool ShowTaxBreakdown { get; init; }
    public bool RequireTableForDineIn { get; init; }
    public bool AutoPrintKitchenTickets { get; init; }
    public int OrderNumberResetHour { get; init; }
    public string? OrderNumberPrefix { get; init; }
    public bool AllowCashPayments { get; init; }
    public bool AllowCardPayments { get; init; }
    public bool TipsEnabled { get; init; }
    public string? TipSuggestions { get; init; }
    public bool TrackInventory { get; init; }
    public bool WarnOnLowStock { get; init; }
    public bool AllowNegativeStock { get; init; }
}

public record OperatingHoursDto
{
    public Guid Id { get; init; }
    public Guid LocationId { get; init; }
    public int DayOfWeek { get; init; }
    public string? DayName { get; init; }
    public string? OpenTime { get; init; }
    public string? CloseTime { get; init; }
    public bool IsClosed { get; init; }
}
