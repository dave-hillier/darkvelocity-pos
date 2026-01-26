using DarkVelocity.Location.Api.Data;
using DarkVelocity.Location.Api.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Location.Tests;

public class LocationApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    // Test data IDs
    public Guid TestLocationId { get; private set; }
    public Guid TestLocation2Id { get; private set; }
    public Guid InactiveLocationId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Create SQLite in-memory connection (keep open for test lifetime)
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Add SQLite DbContext
            services.AddDbContext<LocationDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    public async Task InitializeAsync()
    {
        Client = CreateClient();

        // Seed test data
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocationDbContext>();

        await db.Database.EnsureCreatedAsync();

        // Create main test location
        var location1 = new DarkVelocity.Location.Api.Entities.Location
        {
            Name = "Downtown Restaurant",
            Code = "NYC-01",
            Timezone = "America/New_York",
            CurrencyCode = "USD",
            CurrencySymbol = "$",
            Phone = "+1 212-555-1234",
            Email = "downtown@restaurant.com",
            Website = "https://restaurant.com/downtown",
            AddressLine1 = "123 Main Street",
            City = "New York",
            State = "NY",
            PostalCode = "10001",
            Country = "USA",
            TaxNumber = "12-3456789",
            BusinessName = "Restaurant Group LLC"
        };
        db.Locations.Add(location1);
        TestLocationId = location1.Id;

        await db.SaveChangesAsync();

        // Create settings for location 1
        var settings1 = new LocationSettings
        {
            LocationId = TestLocationId,
            DefaultTaxRate = 8.875m,
            TaxIncludedInPrices = false,
            ReceiptHeader = "Welcome to Downtown Restaurant",
            ReceiptFooter = "Thank you for dining with us!",
            TipSuggestions = [15, 18, 20, 25]
        };
        db.LocationSettings.Add(settings1);

        await db.SaveChangesAsync();

        // Create operating hours for location 1
        var weekdayHours = new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday
        };
        foreach (var day in weekdayHours)
        {
            db.OperatingHours.Add(new OperatingHours
            {
                LocationId = TestLocationId,
                DayOfWeek = day,
                OpenTime = new TimeOnly(11, 0),
                CloseTime = new TimeOnly(22, 0)
            });
        }

        db.OperatingHours.Add(new OperatingHours
        {
            LocationId = TestLocationId,
            DayOfWeek = DayOfWeek.Saturday,
            OpenTime = new TimeOnly(10, 0),
            CloseTime = new TimeOnly(23, 0)
        });

        db.OperatingHours.Add(new OperatingHours
        {
            LocationId = TestLocationId,
            DayOfWeek = DayOfWeek.Sunday,
            OpenTime = new TimeOnly(10, 0),
            CloseTime = new TimeOnly(21, 0)
        });

        await db.SaveChangesAsync();

        // Create second location (London)
        var location2 = new DarkVelocity.Location.Api.Entities.Location
        {
            Name = "London Branch",
            Code = "LON-01",
            Timezone = "Europe/London",
            CurrencyCode = "GBP",
            CurrencySymbol = "£",
            City = "London",
            Country = "UK"
        };
        db.Locations.Add(location2);
        TestLocation2Id = location2.Id;

        await db.SaveChangesAsync();

        // Create settings for location 2
        var settings2 = new LocationSettings
        {
            LocationId = TestLocation2Id,
            DefaultTaxRate = 20.00m,
            TaxIncludedInPrices = true
        };
        db.LocationSettings.Add(settings2);

        await db.SaveChangesAsync();

        // Create inactive location
        var inactiveLocation = new DarkVelocity.Location.Api.Entities.Location
        {
            Name = "Closed Branch",
            Code = "CLS-01",
            Timezone = "America/Los_Angeles",
            CurrencyCode = "USD",
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
