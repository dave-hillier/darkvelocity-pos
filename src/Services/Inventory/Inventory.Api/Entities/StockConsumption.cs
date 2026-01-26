using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Inventory.Api.Entities;

public class StockConsumption : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }
    public Guid StockBatchId { get; set; }
    public Guid IngredientId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? RecipeId { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string ConsumptionType { get; set; } = "sale";
    public DateTime ConsumedAt { get; set; }

    public StockBatch? StockBatch { get; set; }
    public Ingredient? Ingredient { get; set; }
}
