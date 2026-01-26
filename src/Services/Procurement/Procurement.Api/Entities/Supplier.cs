using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Procurement.Api.Entities;

public class Supplier : BaseEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public int PaymentTermsDays { get; set; } = 30;
    public int LeadTimeDays { get; set; } = 3;
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<SupplierIngredient> SupplierIngredients { get; set; } = new List<SupplierIngredient>();
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
    public ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();
}
