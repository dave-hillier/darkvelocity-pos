using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Inventory.Api.Entities;

public class WasteRecord : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }
    public Guid IngredientId { get; set; }
    public Guid? StockBatchId { get; set; }
    public Guid RecordedByUserId { get; set; }

    public decimal Quantity { get; set; }
    public decimal EstimatedCost { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime RecordedAt { get; set; }

    public Ingredient? Ingredient { get; set; }
    public StockBatch? StockBatch { get; set; }
}
