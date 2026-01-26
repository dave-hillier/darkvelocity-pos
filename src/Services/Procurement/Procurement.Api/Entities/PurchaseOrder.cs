using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Procurement.Api.Entities;

public class PurchaseOrder : BaseEntity, ILocationScoped
{
    public required string OrderNumber { get; set; }
    public Guid SupplierId { get; set; }
    public Guid LocationId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string Status { get; set; } = "draft"; // draft, submitted, partially_received, received, cancelled
    public DateTime? ExpectedDeliveryDate { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public decimal OrderTotal { get; set; }
    public string? Notes { get; set; }

    // Navigation properties
    public Supplier? Supplier { get; set; }
    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
    public ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();
}
