using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Reporting.Api.Entities;

public class MarginAlert : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }
    public required string AlertType { get; set; } // item_margin_low, category_margin_low, daily_margin_low
    public Guid? MenuItemId { get; set; }
    public string? MenuItemName { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }

    public decimal CurrentMargin { get; set; }
    public decimal ThresholdMargin { get; set; }
    public decimal Variance { get; set; }

    public DateOnly ReportDate { get; set; }
    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public Guid? AcknowledgedByUserId { get; set; }
    public string? Notes { get; set; }
}
