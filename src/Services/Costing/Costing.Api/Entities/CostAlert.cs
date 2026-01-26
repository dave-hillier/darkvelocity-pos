using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Costing.Api.Entities;

public class CostAlert : BaseEntity
{
    public required string AlertType { get; set; } // recipe_cost_increase, ingredient_price_increase, margin_below_threshold

    // Related entities
    public Guid? RecipeId { get; set; }
    public string? RecipeName { get; set; }
    public Guid? IngredientId { get; set; }
    public string? IngredientName { get; set; }
    public Guid? MenuItemId { get; set; }
    public string? MenuItemName { get; set; }

    // Alert details
    public decimal PreviousValue { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal ChangePercent { get; set; }
    public decimal? ThresholdValue { get; set; }

    // Impact
    public string? ImpactDescription { get; set; }
    public int AffectedRecipeCount { get; set; }

    // Status
    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public Guid? AcknowledgedByUserId { get; set; }
    public string? Notes { get; set; }
    public string? ActionTaken { get; set; } // price_adjusted, menu_updated, accepted, ignored
}
