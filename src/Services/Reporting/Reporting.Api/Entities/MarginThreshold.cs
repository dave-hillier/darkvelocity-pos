using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Reporting.Api.Entities;

public class MarginThreshold : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }
    public required string ThresholdType { get; set; } // overall, category, item
    public Guid? CategoryId { get; set; }
    public Guid? MenuItemId { get; set; }

    public decimal MinimumMarginPercent { get; set; }
    public decimal WarningMarginPercent { get; set; }

    public bool IsActive { get; set; } = true;
}
