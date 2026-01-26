using DarkVelocity.Reporting.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Reporting.Api.Data;

public class ReportingDbContext : DbContext
{
    public ReportingDbContext(DbContextOptions<ReportingDbContext> options) : base(options)
    {
    }

    public DbSet<DailySalesSummary> DailySalesSummaries => Set<DailySalesSummary>();
    public DbSet<ItemSalesSummary> ItemSalesSummaries => Set<ItemSalesSummary>();
    public DbSet<CategorySalesSummary> CategorySalesSummaries => Set<CategorySalesSummary>();
    public DbSet<SupplierSpendSummary> SupplierSpendSummaries => Set<SupplierSpendSummary>();
    public DbSet<StockConsumption> StockConsumptions => Set<StockConsumption>();
    public DbSet<MarginAlert> MarginAlerts => Set<MarginAlert>();
    public DbSet<MarginThreshold> MarginThresholds => Set<MarginThreshold>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // DailySalesSummary
        modelBuilder.Entity<DailySalesSummary>(entity =>
        {
            entity.HasIndex(e => new { e.LocationId, e.Date }).IsUnique();
            entity.Property(e => e.GrossRevenue).HasPrecision(12, 4);
            entity.Property(e => e.DiscountTotal).HasPrecision(12, 4);
            entity.Property(e => e.NetRevenue).HasPrecision(12, 4);
            entity.Property(e => e.TaxCollected).HasPrecision(12, 4);
            entity.Property(e => e.TotalCOGS).HasPrecision(12, 4);
            entity.Property(e => e.GrossProfit).HasPrecision(12, 4);
            entity.Property(e => e.GrossMarginPercent).HasPrecision(5, 2);
            entity.Property(e => e.AverageOrderValue).HasPrecision(12, 4);
            entity.Property(e => e.TipsCollected).HasPrecision(12, 4);
            entity.Property(e => e.CashTotal).HasPrecision(12, 4);
            entity.Property(e => e.CardTotal).HasPrecision(12, 4);
            entity.Property(e => e.OtherPaymentTotal).HasPrecision(12, 4);
            entity.Property(e => e.RefundTotal).HasPrecision(12, 4);
        });

        // ItemSalesSummary
        modelBuilder.Entity<ItemSalesSummary>(entity =>
        {
            entity.HasIndex(e => new { e.LocationId, e.Date, e.MenuItemId }).IsUnique();
            entity.HasIndex(e => new { e.LocationId, e.Date });
            entity.Property(e => e.MenuItemName).HasMaxLength(200);
            entity.Property(e => e.CategoryName).HasMaxLength(100);
            entity.Property(e => e.GrossRevenue).HasPrecision(12, 4);
            entity.Property(e => e.DiscountTotal).HasPrecision(12, 4);
            entity.Property(e => e.NetRevenue).HasPrecision(12, 4);
            entity.Property(e => e.TotalCOGS).HasPrecision(12, 4);
            entity.Property(e => e.AverageCostPerUnit).HasPrecision(12, 4);
            entity.Property(e => e.GrossProfit).HasPrecision(12, 4);
            entity.Property(e => e.GrossMarginPercent).HasPrecision(5, 2);
            entity.Property(e => e.ProfitPerUnit).HasPrecision(12, 4);
        });

        // CategorySalesSummary
        modelBuilder.Entity<CategorySalesSummary>(entity =>
        {
            entity.HasIndex(e => new { e.LocationId, e.Date, e.CategoryId }).IsUnique();
            entity.HasIndex(e => new { e.LocationId, e.Date });
            entity.Property(e => e.CategoryName).HasMaxLength(100);
            entity.Property(e => e.GrossRevenue).HasPrecision(12, 4);
            entity.Property(e => e.DiscountTotal).HasPrecision(12, 4);
            entity.Property(e => e.NetRevenue).HasPrecision(12, 4);
            entity.Property(e => e.TotalCOGS).HasPrecision(12, 4);
            entity.Property(e => e.GrossProfit).HasPrecision(12, 4);
            entity.Property(e => e.GrossMarginPercent).HasPrecision(5, 2);
            entity.Property(e => e.RevenuePercentOfTotal).HasPrecision(5, 2);
        });

        // SupplierSpendSummary
        modelBuilder.Entity<SupplierSpendSummary>(entity =>
        {
            entity.HasIndex(e => new { e.LocationId, e.PeriodStart, e.PeriodEnd, e.SupplierId }).IsUnique();
            entity.Property(e => e.SupplierName).HasMaxLength(200);
            entity.Property(e => e.TotalSpend).HasPrecision(12, 4);
            entity.Property(e => e.AverageDeliveryValue).HasPrecision(12, 4);
            entity.Property(e => e.OnTimePercentage).HasPrecision(5, 2);
            entity.Property(e => e.DiscrepancyValue).HasPrecision(12, 4);
            entity.Property(e => e.DiscrepancyRate).HasPrecision(5, 2);
        });

        // StockConsumption
        modelBuilder.Entity<StockConsumption>(entity =>
        {
            entity.HasIndex(e => new { e.LocationId, e.ConsumedAt });
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.MenuItemId);
            entity.HasIndex(e => e.IngredientId);
            entity.Property(e => e.IngredientName).HasMaxLength(200);
            entity.Property(e => e.QuantityConsumed).HasPrecision(12, 4);
            entity.Property(e => e.UnitCost).HasPrecision(12, 4);
            entity.Property(e => e.TotalCost).HasPrecision(12, 4);
        });

        // MarginAlert
        modelBuilder.Entity<MarginAlert>(entity =>
        {
            entity.HasIndex(e => new { e.LocationId, e.ReportDate, e.IsAcknowledged });
            entity.Property(e => e.AlertType).HasMaxLength(50);
            entity.Property(e => e.MenuItemName).HasMaxLength(200);
            entity.Property(e => e.CategoryName).HasMaxLength(100);
            entity.Property(e => e.CurrentMargin).HasPrecision(5, 2);
            entity.Property(e => e.ThresholdMargin).HasPrecision(5, 2);
            entity.Property(e => e.Variance).HasPrecision(5, 2);
            entity.Property(e => e.Notes).HasMaxLength(500);
        });

        // MarginThreshold
        modelBuilder.Entity<MarginThreshold>(entity =>
        {
            entity.HasIndex(e => new { e.LocationId, e.ThresholdType, e.CategoryId, e.MenuItemId }).IsUnique();
            entity.Property(e => e.ThresholdType).HasMaxLength(50);
            entity.Property(e => e.MinimumMarginPercent).HasPrecision(5, 2);
            entity.Property(e => e.WarningMarginPercent).HasPrecision(5, 2);
        });
    }
}
