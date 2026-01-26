using DarkVelocity.Procurement.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Procurement.Api.Data;

public class ProcurementDbContext : DbContext
{
    public ProcurementDbContext(DbContextOptions<ProcurementDbContext> options) : base(options)
    {
    }

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierIngredient> SupplierIngredients => Set<SupplierIngredient>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<DeliveryLine> DeliveryLines => Set<DeliveryLine>();
    public DbSet<DeliveryDiscrepancy> DeliveryDiscrepancies => Set<DeliveryDiscrepancy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Supplier
        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ContactName).HasMaxLength(100);
            entity.Property(e => e.ContactEmail).HasMaxLength(255);
            entity.Property(e => e.ContactPhone).HasMaxLength(50);
            entity.Property(e => e.Address).HasMaxLength(500);

            entity.HasMany(e => e.SupplierIngredients)
                .WithOne(e => e.Supplier)
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.PurchaseOrders)
                .WithOne(e => e.Supplier)
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Deliveries)
                .WithOne(e => e.Supplier)
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // SupplierIngredient
        modelBuilder.Entity<SupplierIngredient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.SupplierId, e.IngredientId }).IsUnique();
            entity.Property(e => e.SupplierProductCode).HasMaxLength(100);
            entity.Property(e => e.SupplierProductName).HasMaxLength(200);
            entity.Property(e => e.PackUnit).HasMaxLength(20);
            entity.Property(e => e.PackSize).HasPrecision(10, 4);
            entity.Property(e => e.LastKnownPrice).HasPrecision(12, 4);
        });

        // PurchaseOrder
        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrderNumber).IsUnique();
            entity.Property(e => e.OrderNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(30);
            entity.Property(e => e.OrderTotal).HasPrecision(12, 4);

            entity.HasMany(e => e.Lines)
                .WithOne(e => e.PurchaseOrder)
                .HasForeignKey(e => e.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Deliveries)
                .WithOne(e => e.PurchaseOrder)
                .HasForeignKey(e => e.PurchaseOrderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // PurchaseOrderLine
        modelBuilder.Entity<PurchaseOrderLine>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IngredientName).HasMaxLength(200);
            entity.Property(e => e.QuantityOrdered).HasPrecision(12, 4);
            entity.Property(e => e.QuantityReceived).HasPrecision(12, 4);
            entity.Property(e => e.UnitPrice).HasPrecision(12, 4);
            entity.Property(e => e.LineTotal).HasPrecision(12, 4);
        });

        // Delivery
        modelBuilder.Entity<Delivery>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DeliveryNumber).IsUnique();
            entity.Property(e => e.DeliveryNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(30);
            entity.Property(e => e.SupplierInvoiceNumber).HasMaxLength(100);
            entity.Property(e => e.TotalValue).HasPrecision(12, 4);

            entity.HasMany(e => e.Lines)
                .WithOne(e => e.Delivery)
                .HasForeignKey(e => e.DeliveryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Discrepancies)
                .WithOne(e => e.Delivery)
                .HasForeignKey(e => e.DeliveryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // DeliveryLine
        modelBuilder.Entity<DeliveryLine>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IngredientName).HasMaxLength(200);
            entity.Property(e => e.QuantityReceived).HasPrecision(12, 4);
            entity.Property(e => e.UnitCost).HasPrecision(12, 4);
            entity.Property(e => e.LineTotal).HasPrecision(12, 4);
            entity.Property(e => e.BatchNumber).HasMaxLength(100);
        });

        // DeliveryDiscrepancy
        modelBuilder.Entity<DeliveryDiscrepancy>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DiscrepancyType).HasMaxLength(30);
            entity.Property(e => e.QuantityAffected).HasPrecision(12, 4);
            entity.Property(e => e.PriceDifference).HasPrecision(12, 4);
            entity.Property(e => e.ActionTaken).HasMaxLength(30);

            entity.HasOne(e => e.DeliveryLine)
                .WithMany()
                .HasForeignKey(e => e.DeliveryLineId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
