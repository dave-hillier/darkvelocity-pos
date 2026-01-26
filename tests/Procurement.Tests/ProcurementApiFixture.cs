using DarkVelocity.Procurement.Api.Data;
using DarkVelocity.Procurement.Api.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Procurement.Tests;

public class ProcurementApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    // Test data IDs
    public Guid TestLocationId { get; private set; }
    public Guid TestSupplierId { get; private set; }
    public Guid TestIngredientId { get; private set; }
    public Guid TestPurchaseOrderId { get; private set; }
    public Guid TestDeliveryId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Create SQLite in-memory connection (keep open for test lifetime)
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Add SQLite DbContext
            services.AddDbContext<ProcurementDbContext>(options =>
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
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();

        await db.Database.EnsureCreatedAsync();

        // Create test IDs
        TestLocationId = Guid.NewGuid();
        TestIngredientId = Guid.NewGuid();

        // Create test supplier
        var supplier = new Supplier
        {
            Code = "SUP-001",
            Name = "Fresh Foods Ltd",
            ContactName = "John Smith",
            ContactEmail = "orders@freshfoods.com",
            ContactPhone = "020-1234-5678",
            Address = "123 Food Street, London",
            PaymentTermsDays = 30,
            LeadTimeDays = 3
        };
        db.Suppliers.Add(supplier);
        TestSupplierId = supplier.Id;

        // Create supplier ingredient
        var supplierIngredient = new SupplierIngredient
        {
            SupplierId = supplier.Id,
            IngredientId = TestIngredientId,
            SupplierProductCode = "FF-BEEF-001",
            SupplierProductName = "Premium Beef Mince",
            PackSize = 5m,
            PackUnit = "kg",
            LastKnownPrice = 25.00m,
            LastPriceUpdatedAt = DateTime.UtcNow,
            IsPreferred = true
        };
        db.SupplierIngredients.Add(supplierIngredient);

        await db.SaveChangesAsync();

        // Create test purchase order
        var purchaseOrder = new PurchaseOrder
        {
            OrderNumber = "PO-20260126-0001",
            SupplierId = TestSupplierId,
            LocationId = TestLocationId,
            Status = "draft"
        };
        db.PurchaseOrders.Add(purchaseOrder);
        TestPurchaseOrderId = purchaseOrder.Id;

        // Add line to PO
        var poLine = new PurchaseOrderLine
        {
            PurchaseOrderId = purchaseOrder.Id,
            IngredientId = TestIngredientId,
            IngredientName = "Beef Mince",
            QuantityOrdered = 10m,
            UnitPrice = 5.00m,
            LineTotal = 50m
        };
        db.PurchaseOrderLines.Add(poLine);
        purchaseOrder.OrderTotal = 50m;

        await db.SaveChangesAsync();

        // Create test delivery
        var delivery = new Delivery
        {
            DeliveryNumber = "DEL-20260126-0001",
            SupplierId = TestSupplierId,
            LocationId = TestLocationId,
            Status = "pending",
            ReceivedAt = DateTime.UtcNow
        };
        db.Deliveries.Add(delivery);
        TestDeliveryId = delivery.Id;

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

    public ProcurementDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
    }
}
