using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Procurement.Api.Entities;

public class DeliveryDiscrepancy : BaseEntity
{
    public Guid DeliveryId { get; set; }
    public Guid DeliveryLineId { get; set; }
    public string DiscrepancyType { get; set; } = string.Empty; // quantity_short, quantity_over, quality_issue, damaged, wrong_item, price_difference
    public decimal? QuantityAffected { get; set; }
    public decimal? PriceDifference { get; set; }
    public string? Description { get; set; }
    public string ActionTaken { get; set; } = "pending"; // pending, accepted, rejected, credit_requested
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? ResolutionNotes { get; set; }

    // Navigation properties
    public Delivery? Delivery { get; set; }
    public DeliveryLine? DeliveryLine { get; set; }
}
