using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Costing.Api.Entities;

public class Recipe : BaseEntity
{
    public Guid MenuItemId { get; set; }
    public required string MenuItemName { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }

    // Recipe details
    public required string Code { get; set; }
    public string? Description { get; set; }
    public int PortionYield { get; set; } = 1; // Number of portions per recipe batch
    public string? PrepInstructions { get; set; }

    // Current cost (denormalized for quick access)
    public decimal CurrentCostPerPortion { get; set; }
    public DateTime? CostCalculatedAt { get; set; }

    // Status
    public bool IsActive { get; set; } = true;

    // Navigation
    public List<RecipeIngredient> Ingredients { get; set; } = new();
    public List<RecipeCostSnapshot> CostSnapshots { get; set; } = new();
}
