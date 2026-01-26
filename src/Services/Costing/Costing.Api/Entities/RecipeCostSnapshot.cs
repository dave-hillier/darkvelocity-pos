using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Costing.Api.Entities;

public class RecipeCostSnapshot : BaseEntity
{
    public Guid RecipeId { get; set; }
    public DateOnly SnapshotDate { get; set; }

    // Cost breakdown
    public decimal TotalIngredientCost { get; set; }
    public decimal CostPerPortion { get; set; }
    public int PortionYield { get; set; }

    // Menu item pricing at time of snapshot
    public decimal MenuPrice { get; set; }
    public decimal CostPercentage { get; set; } // (CostPerPortion / MenuPrice) * 100
    public decimal GrossMarginPercent { get; set; } // 100 - CostPercentage

    // Trigger for snapshot
    public required string SnapshotReason { get; set; } // scheduled, price_change, manual, ingredient_change

    // Navigation
    public Recipe? Recipe { get; set; }
}
