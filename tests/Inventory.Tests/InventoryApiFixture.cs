using DarkVelocity.Inventory.Api.Data;
using DarkVelocity.Inventory.Api.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Inventory.Tests;

public class InventoryApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    // Test data IDs
    public Guid TestLocationId { get; private set; }
    public Guid TestIngredientId { get; private set; }
    public Guid TestRecipeId { get; private set; }
    public Guid TestStockBatchId { get; private set; }
    public Guid TestMenuItemId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Create SQLite in-memory connection (keep open for test lifetime)
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Add SQLite DbContext
            services.AddDbContext<InventoryDbContext>(options =>
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
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        await db.Database.EnsureCreatedAsync();

        // Create test IDs
        TestLocationId = Guid.NewGuid();
        TestMenuItemId = Guid.NewGuid();

        // Create test ingredient
        var ingredient = new Ingredient
        {
            Code = "BEEF-MINCE",
            Name = "Beef Mince",
            UnitOfMeasure = "kg",
            Category = "proteins",
            StorageType = "chilled",
            ReorderLevel = 5m,
            ReorderQuantity = 10m,
            CurrentStock = 10m
        };
        db.Ingredients.Add(ingredient);
        TestIngredientId = ingredient.Id;

        await db.SaveChangesAsync();

        // Create test stock batches (for FIFO testing)
        var batch1 = new StockBatch
        {
            IngredientId = TestIngredientId,
            LocationId = TestLocationId,
            InitialQuantity = 5m,
            RemainingQuantity = 5m,
            UnitCost = 4.00m,
            ReceivedAt = DateTime.UtcNow.AddDays(-10), // Older
            Status = "active"
        };
        db.StockBatches.Add(batch1);
        TestStockBatchId = batch1.Id;

        var batch2 = new StockBatch
        {
            IngredientId = TestIngredientId,
            LocationId = TestLocationId,
            InitialQuantity = 5m,
            RemainingQuantity = 5m,
            UnitCost = 6.00m,
            ReceivedAt = DateTime.UtcNow.AddDays(-5), // Newer
            Status = "active"
        };
        db.StockBatches.Add(batch2);

        // Create test recipe
        var recipe = new Recipe
        {
            Code = "BURGER-CLASSIC",
            Name = "Classic Burger",
            MenuItemId = TestMenuItemId,
            PortionYield = 1
        };
        db.Recipes.Add(recipe);
        TestRecipeId = recipe.Id;

        await db.SaveChangesAsync();

        // Add ingredient to recipe
        var recipeIngredient = new RecipeIngredient
        {
            RecipeId = TestRecipeId,
            IngredientId = TestIngredientId,
            Quantity = 0.15m,
            WastePercentage = 5m
        };
        db.RecipeIngredients.Add(recipeIngredient);

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

    public InventoryDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    }
}
