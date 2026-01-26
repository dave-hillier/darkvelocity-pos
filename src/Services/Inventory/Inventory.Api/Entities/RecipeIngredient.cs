using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Inventory.Api.Entities;

public class RecipeIngredient : BaseEntity
{
    public Guid RecipeId { get; set; }
    public Guid IngredientId { get; set; }
    public decimal Quantity { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal WastePercentage { get; set; }

    public Recipe? Recipe { get; set; }
    public Ingredient? Ingredient { get; set; }
}
