using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Reporting.Api.Entities;

public class SupplierSpendSummary : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public Guid SupplierId { get; set; }
    public string? SupplierName { get; set; }

    // Spend
    public decimal TotalSpend { get; set; }
    public int DeliveryCount { get; set; }
    public decimal AverageDeliveryValue { get; set; }

    // Performance
    public int OnTimeDeliveries { get; set; }
    public int LateDeliveries { get; set; }
    public decimal OnTimePercentage { get; set; }

    // Quality
    public int DiscrepancyCount { get; set; }
    public decimal DiscrepancyValue { get; set; }
    public decimal DiscrepancyRate { get; set; }

    // Product count
    public int UniqueProductsOrdered { get; set; }
}
