using DarkVelocity.Auth.Api.Data;
using DarkVelocity.Auth.Api.Entities;
using DarkVelocity.Auth.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Auth.Tests;

public class AuthApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    // Test data IDs
    public Guid TestLocationId { get; private set; }
    public Guid TestUserGroupId { get; private set; }
    public Guid TestUserId { get; private set; }
    public string TestUserPin { get; } = "1234";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Create SQLite in-memory connection (keep open for test lifetime)
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Add SQLite DbContext
            services.AddDbContext<AuthDbContext>(options =>
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
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        await db.Database.EnsureCreatedAsync();

        // Create test location
        var location = new Location
        {
            Name = "Test Location",
            Timezone = "Europe/London",
            CurrencyCode = "GBP"
        };
        db.Locations.Add(location);
        TestLocationId = location.Id;

        // Create test user group
        var userGroup = new UserGroup
        {
            Name = "Manager",
            Description = "Test manager group"
        };
        db.UserGroups.Add(userGroup);
        TestUserGroupId = userGroup.Id;

        await db.SaveChangesAsync();

        // Create test user
        var user = new PosUser
        {
            Username = "testuser",
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            PinHash = authService.HashPin(TestUserPin),
            UserGroupId = TestUserGroupId,
            HomeLocationId = TestLocationId,
            QrCodeToken = "test-qr-token"
        };
        db.PosUsers.Add(user);
        TestUserId = user.Id;

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

    public AuthDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    }
}
