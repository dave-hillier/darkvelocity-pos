using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Procurement.Api.Entities;

public class PurchaseOrderLine : BaseEntity
{
    public Guid PurchaseOrderId { get; set; }
    public Guid IngredientId { get; set; }
    public string? IngredientName { get; set; }
    public decimal QuantityOrdered { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string? Notes { get; set; }

    // Navigation properties
    public PurchaseOrder? PurchaseOrder { get; set; }
}
