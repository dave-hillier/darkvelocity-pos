using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Procurement.Api.Entities;

public class SupplierIngredient : BaseEntity
{
    public Guid SupplierId { get; set; }
    public Guid IngredientId { get; set; }
    public string? SupplierProductCode { get; set; }
    public string? SupplierProductName { get; set; }
    public decimal PackSize { get; set; } = 1m;
    public string? PackUnit { get; set; }
    public decimal LastKnownPrice { get; set; }
    public DateTime? LastPriceUpdatedAt { get; set; }
    public bool IsPreferred { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Supplier? Supplier { get; set; }
}
