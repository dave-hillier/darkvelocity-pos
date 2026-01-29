using DarkVelocity.PaymentGateway.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.PaymentGateway.Api.Data;

public class PaymentGatewayDbContext : DbContext
{
    public PaymentGatewayDbContext(DbContextOptions<PaymentGatewayDbContext> options) : base(options)
    {
    }

    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<PaymentIntent> PaymentIntents => Set<PaymentIntent>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<Terminal> Terminals => Set<Terminal>();
    public DbSet<WebhookEndpoint> WebhookEndpoints => Set<WebhookEndpoint>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Merchant
        modelBuilder.Entity<Merchant>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Email).HasMaxLength(200);
            entity.Property(e => e.BusinessName).HasMaxLength(200);
            entity.Property(e => e.BusinessType).HasMaxLength(50);
            entity.Property(e => e.Country).HasMaxLength(2);
            entity.Property(e => e.DefaultCurrency).HasMaxLength(3);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.StatementDescriptor).HasMaxLength(22);
            entity.Property(e => e.AddressLine1).HasMaxLength(200);
            entity.Property(e => e.AddressLine2).HasMaxLength(200);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.State).HasMaxLength(100);
            entity.Property(e => e.PostalCode).HasMaxLength(20);
        });

        // ApiKey
        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.HasIndex(e => e.KeyHash).IsUnique();
            entity.HasIndex(e => new { e.MerchantId, e.Name }).IsUnique();
            entity.Property(e => e.KeyType).HasMaxLength(20);
            entity.Property(e => e.KeyPrefix).HasMaxLength(20);
            entity.Property(e => e.KeyHash).HasMaxLength(64);
            entity.Property(e => e.KeyHint).HasMaxLength(4);
            entity.Property(e => e.Name).HasMaxLength(100);

            entity.HasOne(e => e.Merchant)
                .WithMany(m => m.ApiKeys)
                .HasForeignKey(e => e.MerchantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PaymentIntent
        modelBuilder.Entity<PaymentIntent>(entity =>
        {
            entity.HasIndex(e => e.MerchantId);
            entity.HasIndex(e => e.ClientSecret).IsUnique();
            entity.HasIndex(e => e.ExternalOrderId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);

            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.Status).HasMaxLength(30);
            entity.Property(e => e.CaptureMethod).HasMaxLength(20);
            entity.Property(e => e.ConfirmationMethod).HasMaxLength(20);
            entity.Property(e => e.Channel).HasMaxLength(20);
            entity.Property(e => e.ClientSecret).HasMaxLength(100);
            entity.Property(e => e.PaymentMethodType).HasMaxLength(20);
            entity.Property(e => e.CardBrand).HasMaxLength(20);
            entity.Property(e => e.CardLast4).HasMaxLength(4);
            entity.Property(e => e.CardExpMonth).HasMaxLength(2);
            entity.Property(e => e.CardExpYear).HasMaxLength(4);
            entity.Property(e => e.CardFunding).HasMaxLength(20);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.StatementDescriptor).HasMaxLength(22);
            entity.Property(e => e.StatementDescriptorSuffix).HasMaxLength(22);
            entity.Property(e => e.ReceiptEmail).HasMaxLength(200);
            entity.Property(e => e.ExternalOrderId).HasMaxLength(100);
            entity.Property(e => e.ExternalCustomerId).HasMaxLength(100);
            entity.Property(e => e.CancellationReason).HasMaxLength(50);
            entity.Property(e => e.LastErrorCode).HasMaxLength(50);
            entity.Property(e => e.LastErrorMessage).HasMaxLength(500);

            entity.HasOne(e => e.Merchant)
                .WithMany(m => m.PaymentIntents)
                .HasForeignKey(e => e.MerchantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Terminal)
                .WithMany(t => t.PaymentIntents)
                .HasForeignKey(e => e.TerminalId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Transaction
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasIndex(e => e.MerchantId);
            entity.HasIndex(e => e.PaymentIntentId);
            entity.HasIndex(e => e.AuthorizationCode);
            entity.HasIndex(e => e.CreatedAt);

            entity.Property(e => e.Type).HasMaxLength(20);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.CardBrand).HasMaxLength(20);
            entity.Property(e => e.CardLast4).HasMaxLength(4);
            entity.Property(e => e.CardFunding).HasMaxLength(20);
            entity.Property(e => e.AuthorizationCode).HasMaxLength(20);
            entity.Property(e => e.NetworkTransactionId).HasMaxLength(100);
            entity.Property(e => e.ProcessorResponseCode).HasMaxLength(10);
            entity.Property(e => e.ProcessorResponseText).HasMaxLength(200);
            entity.Property(e => e.RiskLevel).HasMaxLength(20);
            entity.Property(e => e.FailureCode).HasMaxLength(50);
            entity.Property(e => e.FailureMessage).HasMaxLength(500);
            entity.Property(e => e.DeclineCode).HasMaxLength(50);

            entity.HasOne(e => e.Merchant)
                .WithMany()
                .HasForeignKey(e => e.MerchantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.PaymentIntent)
                .WithMany(p => p.Transactions)
                .HasForeignKey(e => e.PaymentIntentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Refund
        modelBuilder.Entity<Refund>(entity =>
        {
            entity.HasIndex(e => e.MerchantId);
            entity.HasIndex(e => e.PaymentIntentId);
            entity.HasIndex(e => e.ReceiptNumber).IsUnique();
            entity.HasIndex(e => e.CreatedAt);

            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.Reason).HasMaxLength(50);
            entity.Property(e => e.ReceiptNumber).HasMaxLength(50);
            entity.Property(e => e.FailureReason).HasMaxLength(200);

            entity.HasOne(e => e.Merchant)
                .WithMany()
                .HasForeignKey(e => e.MerchantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.PaymentIntent)
                .WithMany(p => p.Refunds)
                .HasForeignKey(e => e.PaymentIntentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Terminal
        modelBuilder.Entity<Terminal>(entity =>
        {
            entity.HasIndex(e => e.MerchantId);
            entity.HasIndex(e => e.SerialNumber).IsUnique();
            entity.HasIndex(e => e.RegistrationCode).IsUnique();
            entity.HasIndex(e => e.ExternalLocationId);

            entity.Property(e => e.Label).HasMaxLength(200);
            entity.Property(e => e.DeviceType).HasMaxLength(50);
            entity.Property(e => e.SerialNumber).HasMaxLength(100);
            entity.Property(e => e.DeviceSwVersion).HasMaxLength(50);
            entity.Property(e => e.LocationName).HasMaxLength(200);
            entity.Property(e => e.LocationAddress).HasMaxLength(500);
            entity.Property(e => e.RegistrationCode).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.IpAddress).HasMaxLength(45);

            entity.HasOne(e => e.Merchant)
                .WithMany(m => m.Terminals)
                .HasForeignKey(e => e.MerchantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // WebhookEndpoint
        modelBuilder.Entity<WebhookEndpoint>(entity =>
        {
            entity.HasIndex(e => e.MerchantId);
            entity.HasIndex(e => new { e.MerchantId, e.Url }).IsUnique();

            entity.Property(e => e.Url).HasMaxLength(500);
            entity.Property(e => e.Secret).HasMaxLength(64);
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.EnabledEvents).HasMaxLength(1000);
            entity.Property(e => e.ApiVersion).HasMaxLength(20);
            entity.Property(e => e.DisabledReason).HasMaxLength(200);

            entity.HasOne(e => e.Merchant)
                .WithMany(m => m.WebhookEndpoints)
                .HasForeignKey(e => e.MerchantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // WebhookEvent
        modelBuilder.Entity<WebhookEvent>(entity =>
        {
            entity.HasIndex(e => e.MerchantId);
            entity.HasIndex(e => e.WebhookEndpointId);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.NextRetryAt);
            entity.HasIndex(e => e.CreatedAt);

            entity.Property(e => e.EventType).HasMaxLength(50);
            entity.Property(e => e.ObjectType).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.ErrorMessage).HasMaxLength(500);

            entity.HasOne(e => e.Merchant)
                .WithMany()
                .HasForeignKey(e => e.MerchantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.WebhookEndpoint)
                .WithMany(w => w.WebhookEvents)
                .HasForeignKey(e => e.WebhookEndpointId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
