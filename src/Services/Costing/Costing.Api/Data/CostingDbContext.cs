using DarkVelocity.Costing.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Costing.Api.Data;

public class CostingDbContext : DbContext
{
    public CostingDbContext(DbContextOptions<CostingDbContext> options) : base(options)
    {
    }

    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeCostSnapshot> RecipeCostSnapshots => Set<RecipeCostSnapshot>();
    public DbSet<IngredientPrice> IngredientPrices => Set<IngredientPrice>();
    public DbSet<CostAlert> CostAlerts => Set<CostAlert>();
    public DbSet<CostingSettings> CostingSettings => Set<CostingSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Recipe
        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasIndex(e => e.MenuItemId).IsUnique();
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.MenuItemName).HasMaxLength(200);
            entity.Property(e => e.CategoryName).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.PrepInstructions).HasMaxLength(2000);
            entity.Property(e => e.CurrentCostPerPortion).HasPrecision(12, 4);

            entity.HasMany(e => e.Ingredients)
                .WithOne(i => i.Recipe)
                .HasForeignKey(i => i.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.CostSnapshots)
                .WithOne(s => s.Recipe)
                .HasForeignKey(s => s.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // RecipeIngredient
        modelBuilder.Entity<RecipeIngredient>(entity =>
        {
            entity.HasIndex(e => new { e.RecipeId, e.IngredientId }).IsUnique();
            entity.Property(e => e.IngredientName).HasMaxLength(200);
            entity.Property(e => e.UnitOfMeasure).HasMaxLength(20);
            entity.Property(e => e.Quantity).HasPrecision(12, 4);
            entity.Property(e => e.WastePercentage).HasPrecision(5, 2);
            entity.Property(e => e.CurrentUnitCost).HasPrecision(12, 4);
            entity.Property(e => e.CurrentLineCost).HasPrecision(12, 4);
        });

        // RecipeCostSnapshot
        modelBuilder.Entity<RecipeCostSnapshot>(entity =>
        {
            entity.HasIndex(e => new { e.RecipeId, e.SnapshotDate });
            entity.Property(e => e.TotalIngredientCost).HasPrecision(12, 4);
            entity.Property(e => e.CostPerPortion).HasPrecision(12, 4);
            entity.Property(e => e.MenuPrice).HasPrecision(12, 4);
            entity.Property(e => e.CostPercentage).HasPrecision(5, 2);
            entity.Property(e => e.GrossMarginPercent).HasPrecision(5, 2);
            entity.Property(e => e.SnapshotReason).HasMaxLength(50);
        });

        // IngredientPrice
        modelBuilder.Entity<IngredientPrice>(entity =>
        {
            entity.HasIndex(e => e.IngredientId).IsUnique();
            entity.Property(e => e.IngredientName).HasMaxLength(200);
            entity.Property(e => e.UnitOfMeasure).HasMaxLength(20);
            entity.Property(e => e.PreferredSupplierName).HasMaxLength(200);
            entity.Property(e => e.CurrentPrice).HasPrecision(12, 4);
            entity.Property(e => e.PackSize).HasPrecision(12, 4);
            entity.Property(e => e.PricePerUnit).HasPrecision(12, 4);
            entity.Property(e => e.PreviousPrice).HasPrecision(12, 4);
            entity.Property(e => e.PriceChangePercent).HasPrecision(5, 2);
        });

        // CostAlert
        modelBuilder.Entity<CostAlert>(entity =>
        {
            entity.HasIndex(e => new { e.IsAcknowledged, e.CreatedAt });
            entity.HasIndex(e => e.RecipeId);
            entity.HasIndex(e => e.IngredientId);
            entity.Property(e => e.AlertType).HasMaxLength(50);
            entity.Property(e => e.RecipeName).HasMaxLength(200);
            entity.Property(e => e.IngredientName).HasMaxLength(200);
            entity.Property(e => e.MenuItemName).HasMaxLength(200);
            entity.Property(e => e.PreviousValue).HasPrecision(12, 4);
            entity.Property(e => e.CurrentValue).HasPrecision(12, 4);
            entity.Property(e => e.ChangePercent).HasPrecision(5, 2);
            entity.Property(e => e.ThresholdValue).HasPrecision(12, 4);
            entity.Property(e => e.ImpactDescription).HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.ActionTaken).HasMaxLength(50);
        });

        // CostingSettings
        modelBuilder.Entity<CostingSettings>(entity =>
        {
            entity.HasIndex(e => e.LocationId).IsUnique();
            entity.Property(e => e.TargetFoodCostPercent).HasPrecision(5, 2);
            entity.Property(e => e.TargetBeverageCostPercent).HasPrecision(5, 2);
            entity.Property(e => e.MinimumMarginPercent).HasPrecision(5, 2);
            entity.Property(e => e.WarningMarginPercent).HasPrecision(5, 2);
            entity.Property(e => e.PriceChangeAlertThreshold).HasPrecision(5, 2);
            entity.Property(e => e.CostIncreaseAlertThreshold).HasPrecision(5, 2);
        });
    }
}
