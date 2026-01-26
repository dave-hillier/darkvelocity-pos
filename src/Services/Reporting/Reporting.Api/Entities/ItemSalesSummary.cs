using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Reporting.Api.Entities;

public class ItemSalesSummary : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }
    public DateOnly Date { get; set; }
    public Guid MenuItemId { get; set; }
    public string? MenuItemName { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }

    // Sales
    public int QuantitySold { get; set; }
    public decimal GrossRevenue { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal NetRevenue { get; set; }

    // Cost
    public decimal TotalCOGS { get; set; }
    public decimal AverageCostPerUnit { get; set; }

    // Margin
    public decimal GrossProfit { get; set; }
    public decimal GrossMarginPercent { get; set; }
    public decimal ProfitPerUnit { get; set; }
}
