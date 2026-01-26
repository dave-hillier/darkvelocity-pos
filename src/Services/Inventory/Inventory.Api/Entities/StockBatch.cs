using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Inventory.Api.Entities;

public class StockBatch : BaseEntity, ILocationScoped
{
    public Guid IngredientId { get; set; }
    public Guid LocationId { get; set; }
    public Guid? DeliveryId { get; set; }

    public decimal InitialQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? BatchNumber { get; set; }
    public string Status { get; set; } = "active";

    public Ingredient? Ingredient { get; set; }
}
