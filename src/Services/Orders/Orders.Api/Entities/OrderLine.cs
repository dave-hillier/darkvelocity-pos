using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Orders.Api.Entities;

public class OrderLine : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid MenuItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineTotal { get; set; }

    public string? Notes { get; set; }
    public int? CourseNumber { get; set; }
    public int? SeatNumber { get; set; }

    public bool IsVoided { get; set; }
    public DateTime? VoidedAt { get; set; }
    public Guid? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }

    public Order? Order { get; set; }
}
