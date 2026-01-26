using DarkVelocity.Payments.Api.Data;
using DarkVelocity.Payments.Api.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Payments.Tests;

public class PaymentsApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    // Test data IDs
    public Guid TestLocationId { get; private set; }
    public Guid TestOrderId { get; private set; }
    public Guid TestUserId { get; private set; }
    public Guid TestCashPaymentMethodId { get; private set; }
    public Guid TestCardPaymentMethodId { get; private set; }
    public Guid TestPaymentId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Create SQLite in-memory connection (keep open for test lifetime)
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Add SQLite DbContext
            services.AddDbContext<PaymentsDbContext>(options =>
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
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();

        await db.Database.EnsureCreatedAsync();

        // Create test IDs
        TestLocationId = Guid.NewGuid();
        TestOrderId = Guid.NewGuid();
        TestUserId = Guid.NewGuid();

        // Create cash payment method
        var cashMethod = new PaymentMethod
        {
            LocationId = TestLocationId,
            Name = "Cash",
            MethodType = "cash",
            OpensDrawer = true,
            RequiresTip = false,
            DisplayOrder = 1
        };
        db.PaymentMethods.Add(cashMethod);
        TestCashPaymentMethodId = cashMethod.Id;

        // Create card payment method
        var cardMethod = new PaymentMethod
        {
            LocationId = TestLocationId,
            Name = "Credit Card",
            MethodType = "card",
            OpensDrawer = false,
            RequiresTip = true,
            RequiresExternalTerminal = true,
            DisplayOrder = 2
        };
        db.PaymentMethods.Add(cardMethod);
        TestCardPaymentMethodId = cardMethod.Id;

        // Create inactive payment method
        var inactiveMethod = new PaymentMethod
        {
            LocationId = TestLocationId,
            Name = "Voucher",
            MethodType = "voucher",
            IsActive = false,
            DisplayOrder = 3
        };
        db.PaymentMethods.Add(inactiveMethod);

        await db.SaveChangesAsync();

        // Create a test payment
        var payment = new Payment
        {
            LocationId = TestLocationId,
            OrderId = TestOrderId,
            UserId = TestUserId,
            PaymentMethodId = TestCashPaymentMethodId,
            Amount = 50.00m,
            TipAmount = 5.00m,
            ReceivedAmount = 60.00m,
            ChangeAmount = 5.00m,
            Status = "completed",
            CompletedAt = DateTime.UtcNow
        };
        db.Payments.Add(payment);
        TestPaymentId = payment.Id;

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

    public PaymentsDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
    }
}
