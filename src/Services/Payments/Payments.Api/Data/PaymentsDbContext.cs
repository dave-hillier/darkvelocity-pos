using DarkVelocity.Payments.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Payments.Api.Data;

public class PaymentsDbContext : DbContext
{
    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : base(options)
    {
    }

    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Receipt> Receipts => Set<Receipt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // PaymentMethod
        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.HasIndex(e => new { e.LocationId, e.Name }).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.MethodType).HasMaxLength(50);
        });

        // Payment
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.LocationId);
            entity.HasIndex(e => e.StripePaymentIntentId);

            entity.Property(e => e.Amount).HasPrecision(12, 4);
            entity.Property(e => e.TipAmount).HasPrecision(12, 4);
            entity.Property(e => e.ReceivedAmount).HasPrecision(12, 4);
            entity.Property(e => e.ChangeAmount).HasPrecision(12, 4);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.CardBrand).HasMaxLength(50);
            entity.Property(e => e.CardLastFour).HasMaxLength(4);

            entity.HasOne(e => e.PaymentMethod)
                .WithMany(m => m.Payments)
                .HasForeignKey(e => e.PaymentMethodId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Receipt)
                .WithOne(r => r.Payment)
                .HasForeignKey<Receipt>(r => r.PaymentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Receipt
        modelBuilder.Entity<Receipt>(entity =>
        {
            entity.HasIndex(e => e.PaymentId).IsUnique();
            entity.Property(e => e.BusinessName).HasMaxLength(200);
            entity.Property(e => e.LocationName).HasMaxLength(200);
            entity.Property(e => e.AddressLine1).HasMaxLength(200);
            entity.Property(e => e.AddressLine2).HasMaxLength(200);
            entity.Property(e => e.TaxId).HasMaxLength(50);
            entity.Property(e => e.OrderNumber).HasMaxLength(50);
            entity.Property(e => e.ServerName).HasMaxLength(100);
            entity.Property(e => e.PaymentMethodName).HasMaxLength(100);

            entity.Property(e => e.Subtotal).HasPrecision(12, 4);
            entity.Property(e => e.TaxTotal).HasPrecision(12, 4);
            entity.Property(e => e.DiscountTotal).HasPrecision(12, 4);
            entity.Property(e => e.TipAmount).HasPrecision(12, 4);
            entity.Property(e => e.GrandTotal).HasPrecision(12, 4);
            entity.Property(e => e.AmountPaid).HasPrecision(12, 4);
            entity.Property(e => e.ChangeGiven).HasPrecision(12, 4);
        });
    }
}
