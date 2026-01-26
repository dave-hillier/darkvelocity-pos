using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Procurement.Api.Entities;

public class Delivery : BaseEntity, ILocationScoped
{
    public required string DeliveryNumber { get; set; }
    public Guid SupplierId { get; set; }
    public Guid? PurchaseOrderId { get; set; } // null for ad-hoc deliveries
    public Guid LocationId { get; set; }
    public Guid? ReceivedByUserId { get; set; }
    public string Status { get; set; } = "pending"; // pending, accepted, rejected
    public DateTime? ReceivedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public decimal TotalValue { get; set; }
    public bool HasDiscrepancies { get; set; }
    public string? SupplierInvoiceNumber { get; set; }
    public string? Notes { get; set; }

    // Navigation properties
    public Supplier? Supplier { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public ICollection<DeliveryLine> Lines { get; set; } = new List<DeliveryLine>();
    public ICollection<DeliveryDiscrepancy> Discrepancies { get; set; } = new List<DeliveryDiscrepancy>();
}
