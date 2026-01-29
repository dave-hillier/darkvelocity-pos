using DarkVelocity.Location.Api.Data;
using DarkVelocity.Location.Api.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Integration.Tests.Fixtures;

/// <summary>
/// Fixture for the Location service with locations, settings, and operating hours.
/// </summary>
public class LocationServiceFixture : WebApplicationFactory<DarkVelocity.Location.Api.Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    // Locations
    public Guid TestLocationId { get; private set; }
    public Guid TestLocation2Id { get; private set; }
    public Guid InactiveLocationId { get; private set; }

    public string TestLocationCode => "NYC001";
    public string TestLocation2Code => "LA001";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<LocationDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    public async Task InitializeAsync()
    {
        Client = CreateClient();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocationDbContext>();

        await db.Database.EnsureCreatedAsync();

        // Create main test location
        var location1 = new Location.Api.Entities.Location
        {
            Name = "New York Downtown",
            Code = TestLocationCode,
            Timezone = "America/New_York",
            CurrencyCode = "USD",
            CurrencySymbol = "$",
            Phone = "+1-212-555-0100",
            Email = "nyc@darkvelocity.com",
            Website = "https://nyc.darkvelocity.com",
            BusinessName = "DarkVelocity NYC LLC",
            TaxNumber = "12-3456789",
            IsActive = true,
            IsOpen = true
        };
        db.Locations.Add(location1);
        TestLocationId = location1.Id;

        // Create address for location 1
        db.Addresses.Add(new Address
        {
            LocationId = location1.Id,
            Line1 = "123 Main Street",
            Line2 = "Suite 100",
            City = "New York",
            State = "NY",
            PostalCode = "10001",
            Country = "USA"
        });

        // Create settings for location 1
        db.LocationSettings.Add(new LocationSettings
        {
            LocationId = location1.Id,
            DefaultTaxRate = 8.875m,
            TaxIncludedInPrices = false,
            ReceiptHeader = "Welcome to DarkVelocity NYC!",
            ReceiptFooter = "Thank you for dining with us!",
            PrintReceiptByDefault = true,
            ShowTaxBreakdown = true,
            RequireTableForDineIn = true,
            AutoPrintKitchenTickets = true,
            OrderNumberResetHour = 4,
            OrderNumberPrefix = "NYC",
            AllowCashPayments = true,
            AllowCardPayments = true,
            TipsEnabled = true,
            TipSuggestions = "15,18,20,25",
            TrackInventory = true,
            WarnOnLowStock = true,
            AllowNegativeStock = false
        });

        // Create operating hours for location 1 (Mon-Fri 6am-10pm, Sat-Sun 8am-11pm)
        for (int day = 0; day <= 6; day++)
        {
            var isWeekend = day == 0 || day == 6; // Sunday = 0, Saturday = 6
            db.OperatingHours.Add(new OperatingHours
            {
                LocationId = location1.Id,
                DayOfWeek = (DayOfWeek)day,
                OpenTime = isWeekend ? new TimeOnly(8, 0) : new TimeOnly(6, 0),
                CloseTime = isWeekend ? new TimeOnly(23, 0) : new TimeOnly(22, 0),
                IsClosed = false
            });
        }

        // Create second test location
        var location2 = new Location.Api.Entities.Location
        {
            Name = "Los Angeles Beach",
            Code = TestLocation2Code,
            Timezone = "America/Los_Angeles",
            CurrencyCode = "USD",
            CurrencySymbol = "$",
            Phone = "+1-310-555-0200",
            Email = "la@darkvelocity.com",
            BusinessName = "DarkVelocity LA LLC",
            TaxNumber = "98-7654321",
            IsActive = true,
            IsOpen = false // Currently closed
        };
        db.Locations.Add(location2);
        TestLocation2Id = location2.Id;

        db.Addresses.Add(new Address
        {
            LocationId = location2.Id,
            Line1 = "456 Ocean Drive",
            City = "Los Angeles",
            State = "CA",
            PostalCode = "90210",
            Country = "USA"
        });

        db.LocationSettings.Add(new LocationSettings
        {
            LocationId = location2.Id,
            DefaultTaxRate = 9.5m,
            TaxIncludedInPrices = false,
            PrintReceiptByDefault = true,
            AllowCashPayments = true,
            AllowCardPayments = true,
            TipsEnabled = true,
            TipSuggestions = "18,20,22",
            TrackInventory = true
        });

        // LA location hours (later opening)
        for (int day = 0; day <= 6; day++)
        {
            db.OperatingHours.Add(new OperatingHours
            {
                LocationId = location2.Id,
                DayOfWeek = (DayOfWeek)day,
                OpenTime = new TimeOnly(10, 0),
                CloseTime = new TimeOnly(23, 0),
                IsClosed = day == 1 // Closed on Monday
            });
        }

        // Create inactive location
        var inactiveLocation = new Location.Api.Entities.Location
        {
            Name = "Closed Location",
            Code = "CLOSED01",
            Timezone = "America/Chicago",
            CurrencyCode = "USD",
            CurrencySymbol = "$",
            IsActive = false,
            IsOpen = false
        };
        db.Locations.Add(inactiveLocation);
        InactiveLocationId = inactiveLocation.Id;

        await db.SaveChangesAsync();
    }

    public new async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }

    public LocationDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<LocationDbContext>();
    }
}
