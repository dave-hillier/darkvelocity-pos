using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Inventory.Api.Entities;

public class Ingredient : BaseEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string UnitOfMeasure { get; set; }
    public string? Category { get; set; }
    public string? StorageType { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal ReorderQuantity { get; set; }
    public decimal? CurrentStock { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
    public ICollection<StockBatch> StockBatches { get; set; } = new List<StockBatch>();
}
