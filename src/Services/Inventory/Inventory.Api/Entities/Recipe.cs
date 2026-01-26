using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Inventory.Api.Entities;

public class Recipe : BaseEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public Guid? MenuItemId { get; set; }
    public int PortionYield { get; set; } = 1;
    public string? Instructions { get; set; }
    public decimal? CalculatedCost { get; set; }
    public DateTime? CostCalculatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
}
