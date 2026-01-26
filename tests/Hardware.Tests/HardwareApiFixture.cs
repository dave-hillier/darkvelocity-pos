using DarkVelocity.Hardware.Api.Data;
using DarkVelocity.Hardware.Api.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Hardware.Tests;

public class HardwareApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    // Test data IDs
    public Guid TestLocationId { get; private set; }
    public Guid TestPrinterId { get; private set; }
    public Guid TestKitchenPrinterId { get; private set; }
    public Guid TestCashDrawerId { get; private set; }
    public Guid TestDeviceId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Create SQLite in-memory connection (keep open for test lifetime)
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Add SQLite DbContext
            services.AddDbContext<HardwareDbContext>(options =>
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
        var db = scope.ServiceProvider.GetRequiredService<HardwareDbContext>();

        await db.Database.EnsureCreatedAsync();

        // Create test location ID
        TestLocationId = Guid.NewGuid();

        // Create receipt printer
        var receiptPrinter = new Printer
        {
            LocationId = TestLocationId,
            Name = "Receipt Printer 1",
            PrinterType = "receipt",
            ConnectionType = "network",
            IpAddress = "192.168.1.100",
            Port = 9100,
            PaperWidth = 80,
            IsDefault = true,
            SupportsCashDrawer = true
        };
        db.Printers.Add(receiptPrinter);
        TestPrinterId = receiptPrinter.Id;

        // Create kitchen printer
        var kitchenPrinter = new Printer
        {
            LocationId = TestLocationId,
            Name = "Kitchen Printer",
            PrinterType = "kitchen",
            ConnectionType = "network",
            IpAddress = "192.168.1.101",
            Port = 9100,
            PaperWidth = 80,
            SupportsCashDrawer = false
        };
        db.Printers.Add(kitchenPrinter);
        TestKitchenPrinterId = kitchenPrinter.Id;

        // Create inactive printer
        var inactivePrinter = new Printer
        {
            LocationId = TestLocationId,
            Name = "Old Printer",
            PrinterType = "receipt",
            ConnectionType = "usb",
            IsActive = false
        };
        db.Printers.Add(inactivePrinter);

        await db.SaveChangesAsync();

        // Create cash drawer
        var cashDrawer = new CashDrawer
        {
            LocationId = TestLocationId,
            Name = "Main Drawer",
            PrinterId = TestPrinterId,
            ConnectionType = "printer"
        };
        db.CashDrawers.Add(cashDrawer);
        TestCashDrawerId = cashDrawer.Id;

        await db.SaveChangesAsync();

        // Create POS device
        var device = new PosDevice
        {
            LocationId = TestLocationId,
            Name = "Register 1",
            DeviceId = "device-abc-123",
            DeviceType = "tablet",
            Model = "iPad Pro",
            OsVersion = "iOS 17",
            AppVersion = "1.0.0",
            DefaultPrinterId = TestPrinterId,
            DefaultCashDrawerId = TestCashDrawerId,
            IsOnline = true,
            LastSeenAt = DateTime.UtcNow,
            RegisteredAt = DateTime.UtcNow.AddDays(-30)
        };
        db.PosDevices.Add(device);
        TestDeviceId = device.Id;

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

    public HardwareDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<HardwareDbContext>();
    }
}
