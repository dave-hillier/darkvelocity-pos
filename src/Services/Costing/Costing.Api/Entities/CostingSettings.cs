using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Costing.Api.Entities;

public class CostingSettings : BaseEntity
{
    public Guid LocationId { get; set; }

    // Target margins
    public decimal TargetFoodCostPercent { get; set; } = 30; // Food cost should be 30% of price
    public decimal TargetBeverageCostPercent { get; set; } = 25;
    public decimal MinimumMarginPercent { get; set; } = 50; // Alert if margin drops below 50%
    public decimal WarningMarginPercent { get; set; } = 60; // Warn if margin drops below 60%

    // Alert thresholds
    public decimal PriceChangeAlertThreshold { get; set; } = 10; // Alert if price changes > 10%
    public decimal CostIncreaseAlertThreshold { get; set; } = 5; // Alert if recipe cost increases > 5%

    // Automation
    public bool AutoRecalculateCosts { get; set; } = true;
    public bool AutoCreateSnapshots { get; set; } = true;
    public int SnapshotFrequencyDays { get; set; } = 7; // Weekly snapshots
}
