using DarkVelocity.Orders.Api.Entities;
using DarkVelocity.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Orders.Api.Data;

public class OrdersDbContext : BaseDbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options)
    {
    }

    public DbSet<SalesPeriod> SalesPeriods => Set<SalesPeriod>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SalesPeriod>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.OpeningCashAmount).HasPrecision(12, 4);
            entity.Property(e => e.ClosingCashAmount).HasPrecision(12, 4);
            entity.Property(e => e.ExpectedCashAmount).HasPrecision(12, 4);
            entity.HasIndex(e => new { e.LocationId, e.Status });
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.OrderType).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.Property(e => e.CustomerName).HasMaxLength(200);
            entity.Property(e => e.VoidReason).HasMaxLength(500);
            entity.Property(e => e.Subtotal).HasPrecision(12, 4);
            entity.Property(e => e.DiscountTotal).HasPrecision(12, 4);
            entity.Property(e => e.TaxTotal).HasPrecision(12, 4);
            entity.Property(e => e.GrandTotal).HasPrecision(12, 4);

            entity.HasIndex(e => new { e.LocationId, e.OrderNumber }).IsUnique();
            entity.HasIndex(e => new { e.LocationId, e.Status });

            entity.HasOne(e => e.SalesPeriod)
                .WithMany(p => p.Orders)
                .HasForeignKey(e => e.SalesPeriodId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<OrderLine>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ItemName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.UnitPrice).HasPrecision(12, 4);
            entity.Property(e => e.DiscountAmount).HasPrecision(12, 4);
            entity.Property(e => e.DiscountReason).HasMaxLength(200);
            entity.Property(e => e.TaxRate).HasPrecision(5, 4);
            entity.Property(e => e.LineTotal).HasPrecision(12, 4);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.VoidReason).HasMaxLength(500);

            entity.HasOne(e => e.Order)
                .WithMany(o => o.Lines)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
