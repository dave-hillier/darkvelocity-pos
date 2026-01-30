using DarkVelocity.Customers.Api.Data;
using DarkVelocity.Customers.Api.Entities;
using DarkVelocity.Shared.Infrastructure.Events;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Customers.Tests;

public class CustomersApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    // Test data IDs
    public Guid TestTenantId { get; private set; }
    public Guid TestCustomerId { get; private set; }
    public Guid TestProgramId { get; private set; }
    public Guid TestRewardId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Create SQLite in-memory connection (keep open for test lifetime)
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Add SQLite DbContext
            services.AddDbContext<CustomersDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    public async Task InitializeAsync()
    {
        Client = CreateClient();
        Client.DefaultRequestHeaders.Add("X-Tenant-Id", "00000000-0000-0000-0000-000000000001");

        // Seed test data
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();

        await db.Database.EnsureCreatedAsync();

        // Create test IDs
        TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        // Create a test loyalty program
        var program = new LoyaltyProgram
        {
            TenantId = TestTenantId,
            Name = "Test Rewards",
            Type = "points",
            Status = "active",
            PointsPerCurrencyUnit = 1,
            PointsValueInCurrency = 0.01m,
            MinimumRedemption = 100,
            WelcomeBonus = 50,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30))
        };
        db.LoyaltyPrograms.Add(program);
        TestProgramId = program.Id;

        // Create a test tier
        var tier = new LoyaltyTier
        {
            ProgramId = program.Id,
            Name = "Bronze",
            MinimumPoints = 0,
            PointsMultiplier = 1.0m,
            SortOrder = 1
        };
        db.Set<LoyaltyTier>().Add(tier);

        var goldTier = new LoyaltyTier
        {
            ProgramId = program.Id,
            Name = "Gold",
            MinimumPoints = 500,
            PointsMultiplier = 1.5m,
            SortOrder = 2
        };
        db.Set<LoyaltyTier>().Add(goldTier);

        await db.SaveChangesAsync();

        // Create a test customer with loyalty membership
        var customer = new Customer
        {
            TenantId = TestTenantId,
            Email = "test.customer@example.com",
            FirstName = "Test",
            LastName = "Customer",
            Source = "pos"
        };
        db.Customers.Add(customer);
        TestCustomerId = customer.Id;

        await db.SaveChangesAsync();

        // Create loyalty membership for the customer
        var loyalty = new CustomerLoyalty
        {
            CustomerId = customer.Id,
            ProgramId = program.Id,
            CurrentPoints = 200,
            LifetimePoints = 200,
            CurrentTierId = tier.Id,
            EnrolledAt = DateTime.UtcNow.AddDays(-10)
        };
        db.CustomerLoyalties.Add(loyalty);

        // Create a test reward
        var reward = new Reward
        {
            TenantId = TestTenantId,
            ProgramId = program.Id,
            Name = "Free Coffee",
            Type = "free_item",
            PointsCost = 100,
            Value = 5.00m,
            IsActive = true
        };
        db.Rewards.Add(reward);
        TestRewardId = reward.Id;

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

    public CustomersDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<CustomersDbContext>();
    }

    public InMemoryEventBus GetEventBus()
    {
        var scope = Services.CreateScope();
        return (InMemoryEventBus)scope.ServiceProvider.GetRequiredService<IEventBus>();
    }

    public void ClearEventLog()
    {
        GetEventBus().ClearEventLog();
    }
}
