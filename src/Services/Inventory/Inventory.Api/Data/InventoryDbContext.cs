using DarkVelocity.Inventory.Api.Entities;
using DarkVelocity.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Inventory.Api.Data;

public class InventoryDbContext : BaseDbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
    {
    }

    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<StockBatch> StockBatches => Set<StockBatch>();
    public DbSet<StockConsumption> StockConsumptions => Set<StockConsumption>();
    public DbSet<WasteRecord> WasteRecords => Set<WasteRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.UnitOfMeasure).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(50);
            entity.Property(e => e.StorageType).HasMaxLength(20);
            entity.Property(e => e.ReorderLevel).HasPrecision(12, 4);
            entity.Property(e => e.ReorderQuantity).HasPrecision(12, 4);
            entity.Property(e => e.CurrentStock).HasPrecision(12, 4);

            entity.HasIndex(e => e.Code).IsUnique();
        });

        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Instructions).HasMaxLength(4000);
            entity.Property(e => e.CalculatedCost).HasPrecision(12, 4);

            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.MenuItemId);
        });

        modelBuilder.Entity<RecipeIngredient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Quantity).HasPrecision(12, 4);
            entity.Property(e => e.UnitOfMeasure).HasMaxLength(20);
            entity.Property(e => e.WastePercentage).HasPrecision(5, 2);

            entity.HasOne(e => e.Recipe)
                .WithMany(r => r.Ingredients)
                .HasForeignKey(e => e.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Ingredient)
                .WithMany(i => i.RecipeIngredients)
                .HasForeignKey(e => e.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockBatch>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InitialQuantity).HasPrecision(12, 4);
            entity.Property(e => e.RemainingQuantity).HasPrecision(12, 4);
            entity.Property(e => e.UnitCost).HasPrecision(12, 4);
            entity.Property(e => e.BatchNumber).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(20);

            entity.HasIndex(e => new { e.IngredientId, e.LocationId, e.Status });
            entity.HasIndex(e => new { e.LocationId, e.ReceivedAt });

            entity.HasOne(e => e.Ingredient)
                .WithMany(i => i.StockBatches)
                .HasForeignKey(e => e.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockConsumption>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Quantity).HasPrecision(12, 4);
            entity.Property(e => e.UnitCost).HasPrecision(12, 4);
            entity.Property(e => e.TotalCost).HasPrecision(12, 4);
            entity.Property(e => e.ConsumptionType).HasMaxLength(30);

            entity.HasIndex(e => new { e.LocationId, e.ConsumedAt });
            entity.HasIndex(e => e.OrderId);

            entity.HasOne(e => e.StockBatch)
                .WithMany()
                .HasForeignKey(e => e.StockBatchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Ingredient)
                .WithMany()
                .HasForeignKey(e => e.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WasteRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Quantity).HasPrecision(12, 4);
            entity.Property(e => e.EstimatedCost).HasPrecision(12, 4);
            entity.Property(e => e.Reason).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasIndex(e => new { e.LocationId, e.RecordedAt });

            entity.HasOne(e => e.Ingredient)
                .WithMany()
                .HasForeignKey(e => e.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.StockBatch)
                .WithMany()
                .HasForeignKey(e => e.StockBatchId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
