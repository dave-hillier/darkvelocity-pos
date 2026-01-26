using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Orders.Api.Entities;

public class Order : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }
    public Guid? SalesPeriodId { get; set; }
    public Guid UserId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;
    public string OrderType { get; set; } = "direct_sale";
    public string Status { get; set; } = "open";

    public Guid? TableId { get; set; }
    public int? GuestCount { get; set; }
    public string? CustomerName { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }

    public DateTime? SentAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? VoidedAt { get; set; }
    public Guid? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }

    public SalesPeriod? SalesPeriod { get; set; }
    public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();
}
