using DarkVelocity.PaymentGateway.Api.Data;
using DarkVelocity.PaymentGateway.Api.Entities;
using DarkVelocity.PaymentGateway.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.PaymentGateway.Tests;

public class PaymentGatewayApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;
    public HttpClient UnauthenticatedClient { get; private set; } = null!;

    // Test data
    public Guid TestMerchantId { get; private set; }
    public string TestApiKey { get; private set; } = null!;
    public string TestPublishableKey { get; private set; } = null!;
    public Guid TestTerminalId { get; private set; }
    public Guid TestPaymentIntentId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Create SQLite in-memory connection (keep open for test lifetime)
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Replace DbContext with SQLite
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<PaymentGatewayDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            services.AddDbContext<PaymentGatewayDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    public async Task InitializeAsync()
    {
        UnauthenticatedClient = CreateClient();

        // Seed test data
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentGatewayDbContext>();
        var keyService = scope.ServiceProvider.GetRequiredService<KeyGenerationService>();

        await db.Database.EnsureCreatedAsync();

        // Create test merchant
        var merchant = new Merchant
        {
            Name = "Test Merchant",
            Email = "test@example.com",
            BusinessName = "Test Business LLC",
            BusinessType = "company",
            Country = "US",
            DefaultCurrency = "USD",
            StatementDescriptor = "TEST MERCHANT"
        };
        db.Merchants.Add(merchant);
        TestMerchantId = merchant.Id;

        // Create test API keys
        var (secretKey, secretHash, secretHint) = keyService.GenerateApiKey("sk_test_");
        var secretApiKey = new ApiKey
        {
            MerchantId = merchant.Id,
            Name = "Test Secret Key",
            KeyType = "secret",
            KeyPrefix = "sk_test_",
            KeyHash = secretHash,
            KeyHint = secretHint,
            IsLive = false
        };
        db.ApiKeys.Add(secretApiKey);
        TestApiKey = secretKey;

        var (publishableKey, publishableHash, publishableHint) = keyService.GenerateApiKey("pk_test_");
        var publishableApiKey = new ApiKey
        {
            MerchantId = merchant.Id,
            Name = "Test Publishable Key",
            KeyType = "publishable",
            KeyPrefix = "pk_test_",
            KeyHash = publishableHash,
            KeyHint = publishableHint,
            IsLive = false
        };
        db.ApiKeys.Add(publishableApiKey);
        TestPublishableKey = publishableKey;

        // Create test terminal
        var terminal = new Terminal
        {
            MerchantId = merchant.Id,
            Label = "Test Terminal 1",
            DeviceType = "simulated",
            LocationName = "Test Store",
            Status = "online",
            IsRegistered = true,
            RegisteredAt = DateTime.UtcNow,
            SerialNumber = "SIM-TEST0001",
            DeviceSwVersion = "1.0.0-simulated"
        };
        db.Terminals.Add(terminal);
        TestTerminalId = terminal.Id;

        // Create test payment intent
        var paymentIntent = new PaymentIntent
        {
            MerchantId = merchant.Id,
            Amount = 5000, // $50.00
            Currency = "usd",
            Status = "requires_payment_method",
            CaptureMethod = "automatic",
            ConfirmationMethod = "automatic",
            Channel = "ecommerce",
            ClientSecret = keyService.GenerateClientSecret(Guid.NewGuid()),
            Description = "Test payment intent"
        };
        db.PaymentIntents.Add(paymentIntent);
        TestPaymentIntentId = paymentIntent.Id;

        await db.SaveChangesAsync();

        // Create authenticated client
        Client = CreateClient();
        Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {TestApiKey}");
    }

    public new async Task DisposeAsync()
    {
        Client?.Dispose();
        UnauthenticatedClient?.Dispose();
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }

    public PaymentGatewayDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<PaymentGatewayDbContext>();
    }

    public HttpClient CreateAuthenticatedClient(string apiKey)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        return client;
    }
}
