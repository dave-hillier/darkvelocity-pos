using DarkVelocity.Auth.Api.Data;
using DarkVelocity.Auth.Api.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Integration.Tests.Fixtures;

/// <summary>
/// Fixture for the Auth service with test users, roles, and tokens.
/// </summary>
public class AuthServiceFixture : WebApplicationFactory<DarkVelocity.Auth.Api.Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    // Shared test data IDs
    public Guid TestLocationId { get; } = Guid.NewGuid();
    public Guid TestLocation2Id { get; } = Guid.NewGuid();

    // Users
    public Guid TestCashierId { get; private set; }
    public Guid TestManagerId { get; private set; }
    public Guid TestAdminId { get; private set; }
    public Guid InactiveUserId { get; private set; }

    // User Groups
    public Guid CashierGroupId { get; private set; }
    public Guid ManagerGroupId { get; private set; }
    public Guid AdminGroupId { get; private set; }

    // Test credentials
    public string TestCashierPin => "1234";
    public string TestManagerPin => "5678";
    public string TestAdminPin => "9999";
    public string InvalidPin => "0000";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<AuthDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    public async Task InitializeAsync()
    {
        Client = CreateClient();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        await db.Database.EnsureCreatedAsync();

        // Create user groups
        var cashierGroup = new UserGroup
        {
            Name = "Cashier",
            Description = "Basic cashier role",
            Permissions = new List<string> { "orders.create", "orders.view", "payments.create" }
        };
        db.UserGroups.Add(cashierGroup);
        CashierGroupId = cashierGroup.Id;

        var managerGroup = new UserGroup
        {
            Name = "Manager",
            Description = "Manager with elevated permissions",
            Permissions = new List<string> { "orders.*", "payments.*", "reports.view", "voids.approve", "discounts.approve" }
        };
        db.UserGroups.Add(managerGroup);
        ManagerGroupId = managerGroup.Id;

        var adminGroup = new UserGroup
        {
            Name = "Admin",
            Description = "Full system access",
            Permissions = new List<string> { "*" }
        };
        db.UserGroups.Add(adminGroup);
        AdminGroupId = adminGroup.Id;

        // Create test users
        var cashier = new User
        {
            Username = "cashier1",
            FirstName = "Test",
            LastName = "Cashier",
            Email = "cashier@test.com",
            PinHash = HashPin(TestCashierPin),
            UserGroupId = CashierGroupId,
            HomeLocationId = TestLocationId,
            IsActive = true
        };
        db.Users.Add(cashier);
        TestCashierId = cashier.Id;

        var manager = new User
        {
            Username = "manager1",
            FirstName = "Test",
            LastName = "Manager",
            Email = "manager@test.com",
            PinHash = HashPin(TestManagerPin),
            UserGroupId = ManagerGroupId,
            HomeLocationId = TestLocationId,
            IsActive = true
        };
        db.Users.Add(manager);
        TestManagerId = manager.Id;

        var admin = new User
        {
            Username = "admin1",
            FirstName = "System",
            LastName = "Admin",
            Email = "admin@test.com",
            PinHash = HashPin(TestAdminPin),
            UserGroupId = AdminGroupId,
            HomeLocationId = TestLocationId,
            IsActive = true
        };
        db.Users.Add(admin);
        TestAdminId = admin.Id;

        var inactiveUser = new User
        {
            Username = "inactive1",
            FirstName = "Inactive",
            LastName = "User",
            Email = "inactive@test.com",
            PinHash = HashPin("1111"),
            UserGroupId = CashierGroupId,
            HomeLocationId = TestLocationId,
            IsActive = false
        };
        db.Users.Add(inactiveUser);
        InactiveUserId = inactiveUser.Id;

        // Create location assignments
        db.UserLocationAssignments.Add(new UserLocationAssignment
        {
            UserId = TestCashierId,
            LocationId = TestLocationId
        });

        db.UserLocationAssignments.Add(new UserLocationAssignment
        {
            UserId = TestManagerId,
            LocationId = TestLocationId
        });

        db.UserLocationAssignments.Add(new UserLocationAssignment
        {
            UserId = TestManagerId,
            LocationId = TestLocation2Id // Manager can access both locations
        });

        db.UserLocationAssignments.Add(new UserLocationAssignment
        {
            UserId = TestAdminId,
            LocationId = TestLocationId
        });

        await db.SaveChangesAsync();
    }

    private static string HashPin(string pin)
    {
        // Simple hash for testing - production would use proper hashing
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(pin + "test_salt");
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
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
