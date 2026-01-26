using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Costing.Api.Entities;

public class RecipeIngredient : BaseEntity
{
    public Guid RecipeId { get; set; }
    public Guid IngredientId { get; set; }
    public required string IngredientName { get; set; }

    // Quantity required for the full recipe batch
    public decimal Quantity { get; set; }
    public required string UnitOfMeasure { get; set; } // kg, litre, unit, gram, ml

    // Waste factor (percentage of ingredient lost in preparation)
    public decimal WastePercentage { get; set; } = 0; // e.g., 20% for potato peeling

    // Cost tracking (denormalized for quick access)
    public decimal CurrentUnitCost { get; set; }
    public decimal CurrentLineCost { get; set; } // Quantity * UnitCost * (1 + WastePercentage/100)

    // Navigation
    public Recipe? Recipe { get; set; }
}
