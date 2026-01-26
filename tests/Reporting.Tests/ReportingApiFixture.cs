using DarkVelocity.Reporting.Api.Data;
using DarkVelocity.Reporting.Api.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Reporting.Tests;

public class ReportingApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    // Test data IDs
    public Guid TestLocationId { get; private set; }
    public Guid TestMenuItemId { get; private set; }
    public Guid TestCategoryId { get; private set; }
    public Guid TestSupplierId { get; private set; }
    public Guid TestIngredientId { get; private set; }
    public Guid TestOrderId { get; private set; }
    public Guid TestThresholdId { get; private set; }
    public Guid TestAlertId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Create SQLite in-memory connection (keep open for test lifetime)
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Add SQLite DbContext
            services.AddDbContext<ReportingDbContext>(options =>
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
        var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

        await db.Database.EnsureCreatedAsync();

        // Create test IDs
        TestLocationId = Guid.NewGuid();
        TestMenuItemId = Guid.NewGuid();
        TestCategoryId = Guid.NewGuid();
        TestSupplierId = Guid.NewGuid();
        TestIngredientId = Guid.NewGuid();
        TestOrderId = Guid.NewGuid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);

        // Create daily sales summaries
        var dailySummary1 = new DailySalesSummary
        {
            LocationId = TestLocationId,
            Date = today,
            GrossRevenue = 1500.00m,
            DiscountTotal = 50.00m,
            NetRevenue = 1450.00m,
            TaxCollected = 145.00m,
            TotalCOGS = 500.00m,
            GrossProfit = 950.00m,
            GrossMarginPercent = 65.52m,
            OrderCount = 50,
            ItemsSold = 120,
            AverageOrderValue = 29.00m,
            TipsCollected = 75.00m,
            CashTotal = 600.00m,
            CardTotal = 800.00m,
            OtherPaymentTotal = 50.00m,
            RefundCount = 2,
            RefundTotal = 25.00m
        };
        db.DailySalesSummaries.Add(dailySummary1);

        var dailySummary2 = new DailySalesSummary
        {
            LocationId = TestLocationId,
            Date = yesterday,
            GrossRevenue = 1200.00m,
            DiscountTotal = 30.00m,
            NetRevenue = 1170.00m,
            TaxCollected = 117.00m,
            TotalCOGS = 400.00m,
            GrossProfit = 770.00m,
            GrossMarginPercent = 65.81m,
            OrderCount = 40,
            ItemsSold = 95,
            AverageOrderValue = 29.25m,
            TipsCollected = 60.00m,
            CashTotal = 500.00m,
            CardTotal = 650.00m,
            OtherPaymentTotal = 20.00m,
            RefundCount = 1,
            RefundTotal = 15.00m
        };
        db.DailySalesSummaries.Add(dailySummary2);

        await db.SaveChangesAsync();

        // Create item sales summaries
        var itemSummary1 = new ItemSalesSummary
        {
            LocationId = TestLocationId,
            Date = today,
            MenuItemId = TestMenuItemId,
            MenuItemName = "Burger",
            CategoryId = TestCategoryId,
            CategoryName = "Food",
            QuantitySold = 30,
            GrossRevenue = 450.00m,
            DiscountTotal = 10.00m,
            NetRevenue = 440.00m,
            TotalCOGS = 150.00m,
            GrossProfit = 290.00m,
            GrossMarginPercent = 65.91m,
            AverageCostPerUnit = 5.00m,
            ProfitPerUnit = 9.67m
        };
        db.ItemSalesSummaries.Add(itemSummary1);

        var itemSummary2 = new ItemSalesSummary
        {
            LocationId = TestLocationId,
            Date = yesterday,
            MenuItemId = TestMenuItemId,
            MenuItemName = "Burger",
            CategoryId = TestCategoryId,
            CategoryName = "Food",
            QuantitySold = 25,
            GrossRevenue = 375.00m,
            DiscountTotal = 5.00m,
            NetRevenue = 370.00m,
            TotalCOGS = 125.00m,
            GrossProfit = 245.00m,
            GrossMarginPercent = 66.22m,
            AverageCostPerUnit = 5.00m,
            ProfitPerUnit = 9.80m
        };
        db.ItemSalesSummaries.Add(itemSummary2);

        await db.SaveChangesAsync();

        // Create category sales summaries
        var categorySummary1 = new CategorySalesSummary
        {
            LocationId = TestLocationId,
            Date = today,
            CategoryId = TestCategoryId,
            CategoryName = "Food",
            ItemsSold = 80,
            GrossRevenue = 1000.00m,
            DiscountTotal = 30.00m,
            NetRevenue = 970.00m,
            TotalCOGS = 350.00m,
            GrossProfit = 620.00m,
            GrossMarginPercent = 63.92m,
            RevenuePercentOfTotal = 66.90m
        };
        db.CategorySalesSummaries.Add(categorySummary1);

        var categorySummary2 = new CategorySalesSummary
        {
            LocationId = TestLocationId,
            Date = today,
            CategoryId = Guid.NewGuid(),
            CategoryName = "Beverages",
            ItemsSold = 40,
            GrossRevenue = 500.00m,
            DiscountTotal = 20.00m,
            NetRevenue = 480.00m,
            TotalCOGS = 150.00m,
            GrossProfit = 330.00m,
            GrossMarginPercent = 68.75m,
            RevenuePercentOfTotal = 33.10m
        };
        db.CategorySalesSummaries.Add(categorySummary2);

        await db.SaveChangesAsync();

        // Create supplier spend summary
        var supplierSummary = new SupplierSpendSummary
        {
            LocationId = TestLocationId,
            PeriodStart = today.AddDays(-7),
            PeriodEnd = today,
            SupplierId = TestSupplierId,
            SupplierName = "Fresh Foods Ltd",
            TotalSpend = 2500.00m,
            DeliveryCount = 3,
            AverageDeliveryValue = 833.33m,
            OnTimeDeliveries = 2,
            LateDeliveries = 1,
            OnTimePercentage = 66.67m,
            DiscrepancyCount = 1,
            DiscrepancyValue = 50.00m,
            DiscrepancyRate = 33.33m,
            UniqueProductsOrdered = 15
        };
        db.SupplierSpendSummaries.Add(supplierSummary);

        await db.SaveChangesAsync();

        // Create stock consumption
        var consumption = new StockConsumption
        {
            LocationId = TestLocationId,
            OrderId = TestOrderId,
            OrderLineId = Guid.NewGuid(),
            MenuItemId = TestMenuItemId,
            IngredientId = TestIngredientId,
            IngredientName = "Beef Mince",
            StockBatchId = Guid.NewGuid(),
            QuantityConsumed = 0.15m,
            UnitCost = 5.00m,
            TotalCost = 0.75m,
            ConsumedAt = DateTime.UtcNow.AddHours(-1)
        };
        db.StockConsumptions.Add(consumption);

        await db.SaveChangesAsync();

        // Create margin threshold
        var threshold = new MarginThreshold
        {
            LocationId = TestLocationId,
            ThresholdType = "overall",
            MinimumMarginPercent = 50.00m,
            WarningMarginPercent = 60.00m
        };
        db.MarginThresholds.Add(threshold);
        TestThresholdId = threshold.Id;

        await db.SaveChangesAsync();

        // Create margin alert
        var alert = new MarginAlert
        {
            LocationId = TestLocationId,
            AlertType = "item_margin_low",
            MenuItemId = TestMenuItemId,
            MenuItemName = "Burger",
            CurrentMargin = 45.00m,
            ThresholdMargin = 50.00m,
            Variance = -5.00m,
            ReportDate = today
        };
        db.MarginAlerts.Add(alert);
        TestAlertId = alert.Id;

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

    public ReportingDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
    }
}
