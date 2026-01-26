using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Reporting.Api.Dtos;

// Daily Sales Summary DTOs
public class DailySalesSummaryDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public DateOnly Date { get; set; }
    public decimal GrossRevenue { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal TaxCollected { get; set; }
    public decimal TotalCOGS { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossMarginPercent { get; set; }
    public int OrderCount { get; set; }
    public int ItemsSold { get; set; }
    public decimal AverageOrderValue { get; set; }
    public decimal TipsCollected { get; set; }
    public decimal CashTotal { get; set; }
    public decimal CardTotal { get; set; }
    public decimal OtherPaymentTotal { get; set; }
    public int RefundCount { get; set; }
    public decimal RefundTotal { get; set; }
}

public class DailySalesRangeDto : HalResource
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public List<DailySalesSummaryDto> Days { get; set; } = new();
    public DailySalesTotalsDto Totals { get; set; } = new();
}

public class DailySalesTotalsDto
{
    public decimal GrossRevenue { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal TotalCOGS { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossMarginPercent { get; set; }
    public int OrderCount { get; set; }
    public int ItemsSold { get; set; }
}

// Item Sales Summary DTOs
public class ItemSalesSummaryDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public DateOnly Date { get; set; }
    public Guid MenuItemId { get; set; }
    public string? MenuItemName { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public int QuantitySold { get; set; }
    public decimal GrossRevenue { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal TotalCOGS { get; set; }
    public decimal AverageCostPerUnit { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossMarginPercent { get; set; }
    public decimal ProfitPerUnit { get; set; }
}

public class ItemMarginReportDto : HalResource
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public List<ItemMarginSummaryDto> Items { get; set; } = new();
}

public class ItemMarginSummaryDto
{
    public Guid MenuItemId { get; set; }
    public string? MenuItemName { get; set; }
    public string? CategoryName { get; set; }
    public int QuantitySold { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal TotalCOGS { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossMarginPercent { get; set; }
    public decimal ProfitPerUnit { get; set; }
}

// Category Sales Summary DTOs
public class CategorySalesSummaryDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public DateOnly Date { get; set; }
    public Guid CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public int ItemsSold { get; set; }
    public decimal GrossRevenue { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal TotalCOGS { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossMarginPercent { get; set; }
    public decimal RevenuePercentOfTotal { get; set; }
}

public class CategoryMarginReportDto : HalResource
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public List<CategoryMarginSummaryDto> Categories { get; set; } = new();
}

public class CategoryMarginSummaryDto
{
    public Guid CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public int ItemsSold { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal TotalCOGS { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossMarginPercent { get; set; }
    public decimal RevenuePercentOfTotal { get; set; }
}

// Supplier Spend Summary DTOs
public class SupplierSpendSummaryDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public Guid SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public decimal TotalSpend { get; set; }
    public int DeliveryCount { get; set; }
    public decimal AverageDeliveryValue { get; set; }
    public int OnTimeDeliveries { get; set; }
    public int LateDeliveries { get; set; }
    public decimal OnTimePercentage { get; set; }
    public int DiscrepancyCount { get; set; }
    public decimal DiscrepancyValue { get; set; }
    public decimal DiscrepancyRate { get; set; }
    public int UniqueProductsOrdered { get; set; }
}

public class SupplierAnalysisReportDto : HalResource
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public List<SupplierSpendSummaryDto> Suppliers { get; set; } = new();
    public decimal TotalSpend { get; set; }
}

// Stock Consumption DTOs
public class StockConsumptionDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public Guid OrderId { get; set; }
    public Guid OrderLineId { get; set; }
    public Guid MenuItemId { get; set; }
    public Guid IngredientId { get; set; }
    public string? IngredientName { get; set; }
    public Guid StockBatchId { get; set; }
    public decimal QuantityConsumed { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public DateTime ConsumedAt { get; set; }
}

public record RecordConsumptionRequest(
    Guid OrderId,
    Guid OrderLineId,
    Guid MenuItemId,
    Guid IngredientId,
    string? IngredientName,
    Guid StockBatchId,
    decimal QuantityConsumed,
    decimal UnitCost);

// Margin Alert DTOs
public class MarginAlertDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public string? AlertType { get; set; }
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
    public string? Notes { get; set; }
}

public record AcknowledgeAlertRequest(
    string? Notes = null);

// Margin Threshold DTOs
public class MarginThresholdDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public string? ThresholdType { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? MenuItemId { get; set; }
    public decimal MinimumMarginPercent { get; set; }
    public decimal WarningMarginPercent { get; set; }
    public bool IsActive { get; set; }
}

public record CreateMarginThresholdRequest(
    string ThresholdType,
    decimal MinimumMarginPercent,
    decimal WarningMarginPercent,
    Guid? CategoryId = null,
    Guid? MenuItemId = null);

public record UpdateMarginThresholdRequest(
    decimal? MinimumMarginPercent = null,
    decimal? WarningMarginPercent = null,
    bool? IsActive = null);

// Summary Report Request
public record GenerateDailySummaryRequest(
    DateOnly Date,
    decimal GrossRevenue,
    decimal DiscountTotal,
    decimal TaxCollected,
    decimal TotalCOGS,
    int OrderCount,
    int ItemsSold,
    decimal TipsCollected,
    decimal CashTotal,
    decimal CardTotal,
    decimal OtherPaymentTotal,
    int RefundCount,
    decimal RefundTotal);

public record GenerateItemSummaryRequest(
    DateOnly Date,
    Guid MenuItemId,
    string? MenuItemName,
    Guid? CategoryId,
    string? CategoryName,
    int QuantitySold,
    decimal GrossRevenue,
    decimal DiscountTotal,
    decimal TotalCOGS);

public record GenerateCategorySummaryRequest(
    DateOnly Date,
    Guid CategoryId,
    string? CategoryName,
    int ItemsSold,
    decimal GrossRevenue,
    decimal DiscountTotal,
    decimal TotalCOGS,
    decimal TotalDayRevenue);

public record GenerateSupplierSummaryRequest(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    Guid SupplierId,
    string? SupplierName,
    decimal TotalSpend,
    int DeliveryCount,
    int OnTimeDeliveries,
    int LateDeliveries,
    int DiscrepancyCount,
    decimal DiscrepancyValue,
    int UniqueProductsOrdered);
