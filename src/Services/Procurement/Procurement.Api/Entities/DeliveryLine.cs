using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Procurement.Api.Entities;

public class DeliveryLine : BaseEntity
{
    public Guid DeliveryId { get; set; }
    public Guid IngredientId { get; set; }
    public string? IngredientName { get; set; }
    public Guid? PurchaseOrderLineId { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Notes { get; set; }

    // Navigation properties
    public Delivery? Delivery { get; set; }
}
