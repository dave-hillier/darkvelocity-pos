using DarkVelocity.Menu.Api.Data;
using DarkVelocity.Menu.Api.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Menu.Tests;

public class MenuApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    // Test data IDs
    public Guid TestLocationId { get; private set; }
    public Guid TestAccountingGroupId { get; private set; }
    public Guid TestCategoryId { get; private set; }
    public Guid TestItemId { get; private set; }
    public Guid TestMenuId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Create SQLite in-memory connection (keep open for test lifetime)
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Add SQLite DbContext
            services.AddDbContext<MenuDbContext>(options =>
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
        var db = scope.ServiceProvider.GetRequiredService<MenuDbContext>();

        await db.Database.EnsureCreatedAsync();

        // Create test location ID (would normally come from Location service)
        TestLocationId = Guid.NewGuid();

        // Create test accounting group
        var accountingGroup = new AccountingGroup
        {
            Name = "Food",
            Description = "Food items",
            TaxRate = 0.20m
        };
        db.AccountingGroups.Add(accountingGroup);
        TestAccountingGroupId = accountingGroup.Id;

        // Create test category
        var category = new MenuCategory
        {
            LocationId = TestLocationId,
            Name = "Main Courses",
            Description = "Main course dishes",
            DisplayOrder = 1,
            Color = "#FF6600"
        };
        db.Categories.Add(category);
        TestCategoryId = category.Id;

        await db.SaveChangesAsync();

        // Create test item
        var item = new MenuItem
        {
            LocationId = TestLocationId,
            CategoryId = TestCategoryId,
            AccountingGroupId = TestAccountingGroupId,
            Name = "Test Burger",
            Description = "A delicious test burger",
            Price = 12.50m,
            Sku = "BURGER-001"
        };
        db.Items.Add(item);
        TestItemId = item.Id;

        // Create test menu
        var menu = new MenuDefinition
        {
            LocationId = TestLocationId,
            Name = "Default Menu",
            Description = "The default menu",
            IsDefault = true
        };
        db.Menus.Add(menu);
        TestMenuId = menu.Id;

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

    public MenuDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<MenuDbContext>();
    }
}
