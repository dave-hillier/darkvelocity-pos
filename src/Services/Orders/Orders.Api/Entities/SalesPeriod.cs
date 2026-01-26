using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Orders.Api.Entities;

public class SalesPeriod : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }
    public Guid OpenedByUserId { get; set; }
    public Guid? ClosedByUserId { get; set; }

    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public decimal OpeningCashAmount { get; set; }
    public decimal? ClosingCashAmount { get; set; }
    public decimal? ExpectedCashAmount { get; set; }

    public string Status { get; set; } = "open";

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
