using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Reporting.Api.Entities;

public class CategorySalesSummary : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }
    public DateOnly Date { get; set; }
    public Guid CategoryId { get; set; }
    public string? CategoryName { get; set; }

    // Sales
    public int ItemsSold { get; set; }
    public decimal GrossRevenue { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal NetRevenue { get; set; }

    // Cost
    public decimal TotalCOGS { get; set; }

    // Margin
    public decimal GrossProfit { get; set; }
    public decimal GrossMarginPercent { get; set; }

    // Percentage of total
    public decimal RevenuePercentOfTotal { get; set; }
}
