using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Reporting.Api.Entities;

public class DailySalesSummary : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }
    public DateOnly Date { get; set; }

    // Revenue
    public decimal GrossRevenue { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal TaxCollected { get; set; }

    // COGS
    public decimal TotalCOGS { get; set; }

    // Calculated margins
    public decimal GrossProfit { get; set; }
    public decimal GrossMarginPercent { get; set; }

    // Counts
    public int OrderCount { get; set; }
    public int ItemsSold { get; set; }
    public decimal AverageOrderValue { get; set; }

    // Tips
    public decimal TipsCollected { get; set; }

    // Payment breakdown
    public decimal CashTotal { get; set; }
    public decimal CardTotal { get; set; }
    public decimal OtherPaymentTotal { get; set; }

    // Refunds
    public int RefundCount { get; set; }
    public decimal RefundTotal { get; set; }
}
