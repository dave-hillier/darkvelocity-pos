using DarkVelocity.Hardware.Api.Data;
using DarkVelocity.Hardware.Api.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Integration.Tests.Fixtures;

/// <summary>
/// Fixture for the Hardware service with POS devices, printers, and cash drawers.
/// </summary>
public class HardwareServiceFixture : WebApplicationFactory<DarkVelocity.Hardware.Api.Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    // Shared test data IDs
    public Guid TestLocationId { get; } = Guid.NewGuid();
    public Guid TestLocation2Id { get; } = Guid.NewGuid();

    // Devices
    public Guid TestDeviceId { get; private set; }
    public Guid TestDevice2Id { get; private set; }
    public Guid InactiveDeviceId { get; private set; }
    public string TestDeviceDeviceId => "POS-001";

    // Printers
    public Guid ReceiptPrinterId { get; private set; }
    public Guid KitchenPrinterId { get; private set; }
    public Guid InactivePrinterId { get; private set; }

    // Cash Drawers
    public Guid TestCashDrawerId { get; private set; }
    public Guid InactiveCashDrawerId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<HardwareDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    public async Task InitializeAsync()
    {
        Client = CreateClient();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HardwareDbContext>();

        await db.Database.EnsureCreatedAsync();

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
            IsActive = true,
            CharacterSet = "UTF-8",
            SupportsCut = true,
            SupportsCashDrawer = true
        };
        db.Printers.Add(receiptPrinter);
        ReceiptPrinterId = receiptPrinter.Id;

        var kitchenPrinter = new Printer
        {
            LocationId = TestLocationId,
            Name = "Kitchen Printer",
            PrinterType = "kitchen",
            ConnectionType = "network",
            IpAddress = "192.168.1.101",
            Port = 9100,
            PaperWidth = 80,
            IsDefault = false,
            IsActive = true,
            CharacterSet = "UTF-8",
            SupportsCut = true,
            SupportsCashDrawer = false
        };
        db.Printers.Add(kitchenPrinter);
        KitchenPrinterId = kitchenPrinter.Id;

        var inactivePrinter = new Printer
        {
            LocationId = TestLocationId,
            Name = "Old Printer",
            PrinterType = "receipt",
            ConnectionType = "usb",
            PaperWidth = 58,
            IsDefault = false,
            IsActive = false
        };
        db.Printers.Add(inactivePrinter);
        InactivePrinterId = inactivePrinter.Id;

        // Create cash drawers
        var cashDrawer = new CashDrawer
        {
            LocationId = TestLocationId,
            Name = "Main Drawer",
            PrinterId = ReceiptPrinterId,
            ConnectionType = "printer",
            IsActive = true,
            KickPulsePin = 0,
            KickPulseOnTime = 100,
            KickPulseOffTime = 100
        };
        db.CashDrawers.Add(cashDrawer);
        TestCashDrawerId = cashDrawer.Id;

        var inactiveCashDrawer = new CashDrawer
        {
            LocationId = TestLocationId,
            Name = "Old Drawer",
            PrinterId = InactivePrinterId,
            ConnectionType = "printer",
            IsActive = false
        };
        db.CashDrawers.Add(inactiveCashDrawer);
        InactiveCashDrawerId = inactiveCashDrawer.Id;

        // Create POS devices
        var device1 = new PosDevice
        {
            LocationId = TestLocationId,
            Name = "Register 1",
            DeviceId = TestDeviceDeviceId,
            DeviceType = "tablet",
            Model = "iPad Pro",
            OsVersion = "iOS 17.0",
            AppVersion = "2.1.0",
            DefaultPrinterId = ReceiptPrinterId,
            DefaultCashDrawerId = TestCashDrawerId,
            AutoPrintReceipts = true,
            OpenDrawerOnCash = true,
            IsActive = true,
            IsOnline = true,
            LastSeenAt = DateTime.UtcNow,
            RegisteredAt = DateTime.UtcNow.AddMonths(-6)
        };
        db.PosDevices.Add(device1);
        TestDeviceId = device1.Id;

        var device2 = new PosDevice
        {
            LocationId = TestLocationId,
            Name = "Register 2",
            DeviceId = "POS-002",
            DeviceType = "tablet",
            Model = "iPad Air",
            OsVersion = "iOS 16.5",
            AppVersion = "2.0.5",
            DefaultPrinterId = ReceiptPrinterId,
            AutoPrintReceipts = true,
            OpenDrawerOnCash = false,
            IsActive = true,
            IsOnline = false,
            LastSeenAt = DateTime.UtcNow.AddHours(-2),
            RegisteredAt = DateTime.UtcNow.AddMonths(-3)
        };
        db.PosDevices.Add(device2);
        TestDevice2Id = device2.Id;

        var inactiveDevice = new PosDevice
        {
            LocationId = TestLocationId,
            Name = "Old Register",
            DeviceId = "POS-OLD",
            DeviceType = "terminal",
            Model = "Clover Station",
            IsActive = false,
            IsOnline = false,
            RegisteredAt = DateTime.UtcNow.AddYears(-2)
        };
        db.PosDevices.Add(inactiveDevice);
        InactiveDeviceId = inactiveDevice.Id;

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
