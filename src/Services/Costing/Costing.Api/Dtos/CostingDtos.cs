using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Costing.Api.Dtos;

// Recipe DTOs
public class RecipeDto : HalResource
{
    public Guid Id { get; set; }
    public Guid MenuItemId { get; set; }
    public string MenuItemName { get; set; } = "";
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string Code { get; set; } = "";
    public string? Description { get; set; }
    public int PortionYield { get; set; }
    public string? PrepInstructions { get; set; }
    public decimal CurrentCostPerPortion { get; set; }
    public DateTime? CostCalculatedAt { get; set; }
    public bool IsActive { get; set; }
    public List<RecipeIngredientDto> Ingredients { get; set; } = new();
}

public class RecipeSummaryDto : HalResource
{
    public Guid Id { get; set; }
    public Guid MenuItemId { get; set; }
    public string MenuItemName { get; set; } = "";
    public string Code { get; set; } = "";
    public decimal CurrentCostPerPortion { get; set; }
    public int IngredientCount { get; set; }
    public bool IsActive { get; set; }
}

public class RecipeIngredientDto : HalResource
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = "";
    public decimal Quantity { get; set; }
    public string UnitOfMeasure { get; set; } = "";
    public decimal WastePercentage { get; set; }
    public decimal CurrentUnitCost { get; set; }
    public decimal CurrentLineCost { get; set; }
    public decimal EffectiveQuantity => Quantity * (1 + WastePercentage / 100);
}

// Recipe Cost Snapshot DTOs
public class RecipeCostSnapshotDto : HalResource
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public DateOnly SnapshotDate { get; set; }
    public decimal TotalIngredientCost { get; set; }
    public decimal CostPerPortion { get; set; }
    public int PortionYield { get; set; }
    public decimal MenuPrice { get; set; }
    public decimal CostPercentage { get; set; }
    public decimal GrossMarginPercent { get; set; }
    public string SnapshotReason { get; set; } = "";
}

// Ingredient Price DTOs
public class IngredientPriceDto : HalResource
{
    public Guid Id { get; set; }
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = "";
    public decimal CurrentPrice { get; set; }
    public string UnitOfMeasure { get; set; } = "";
    public decimal PackSize { get; set; }
    public decimal PricePerUnit { get; set; }
    public Guid? PreferredSupplierId { get; set; }
    public string? PreferredSupplierName { get; set; }
    public decimal? PreviousPrice { get; set; }
    public DateTime? PriceChangedAt { get; set; }
    public decimal PriceChangePercent { get; set; }
    public bool IsActive { get; set; }
}

// Cost Alert DTOs
public class CostAlertDto : HalResource
{
    public Guid Id { get; set; }
    public string AlertType { get; set; } = "";
    public Guid? RecipeId { get; set; }
    public string? RecipeName { get; set; }
    public Guid? IngredientId { get; set; }
    public string? IngredientName { get; set; }
    public Guid? MenuItemId { get; set; }
    public string? MenuItemName { get; set; }
    public decimal PreviousValue { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal ChangePercent { get; set; }
    public decimal? ThresholdValue { get; set; }
    public string? ImpactDescription { get; set; }
    public int AffectedRecipeCount { get; set; }
    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? Notes { get; set; }
    public string? ActionTaken { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Costing Settings DTOs
public class CostingSettingsDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public decimal TargetFoodCostPercent { get; set; }
    public decimal TargetBeverageCostPercent { get; set; }
    public decimal MinimumMarginPercent { get; set; }
    public decimal WarningMarginPercent { get; set; }
    public decimal PriceChangeAlertThreshold { get; set; }
    public decimal CostIncreaseAlertThreshold { get; set; }
    public bool AutoRecalculateCosts { get; set; }
    public bool AutoCreateSnapshots { get; set; }
    public int SnapshotFrequencyDays { get; set; }
}

// Cost Calculation DTOs
public class RecipeCostCalculationDto
{
    public Guid RecipeId { get; set; }
    public string RecipeName { get; set; } = "";
    public decimal TotalIngredientCost { get; set; }
    public decimal CostPerPortion { get; set; }
    public int PortionYield { get; set; }
    public decimal? MenuPrice { get; set; }
    public decimal? CostPercentage { get; set; }
    public decimal? GrossMarginPercent { get; set; }
    public List<IngredientCostLineDto> IngredientCosts { get; set; } = new();
}

public class IngredientCostLineDto
{
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = "";
    public decimal Quantity { get; set; }
    public string UnitOfMeasure { get; set; } = "";
    public decimal WastePercentage { get; set; }
    public decimal EffectiveQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineCost { get; set; }
    public decimal CostPercentOfTotal { get; set; }
}

// Request DTOs
public record CreateRecipeRequest(
    Guid MenuItemId,
    string MenuItemName,
    string Code,
    Guid? CategoryId = null,
    string? CategoryName = null,
    string? Description = null,
    int PortionYield = 1,
    string? PrepInstructions = null);

public record UpdateRecipeRequest(
    string? MenuItemName = null,
    string? Code = null,
    Guid? CategoryId = null,
    string? CategoryName = null,
    string? Description = null,
    int? PortionYield = null,
    string? PrepInstructions = null,
    bool? IsActive = null);

public record AddRecipeIngredientRequest(
    Guid IngredientId,
    string IngredientName,
    decimal Quantity,
    string UnitOfMeasure,
    decimal WastePercentage = 0);

public record UpdateRecipeIngredientRequest(
    decimal? Quantity = null,
    decimal? WastePercentage = null);

public record UpdateIngredientPriceRequest(
    decimal CurrentPrice,
    decimal PackSize = 1,
    Guid? PreferredSupplierId = null,
    string? PreferredSupplierName = null);

public record CreateIngredientPriceRequest(
    Guid IngredientId,
    string IngredientName,
    decimal CurrentPrice,
    string UnitOfMeasure,
    decimal PackSize = 1,
    Guid? PreferredSupplierId = null,
    string? PreferredSupplierName = null);

public record AcknowledgeCostAlertRequest(
    string? Notes = null,
    string? ActionTaken = null);

public record UpdateCostingSettingsRequest(
    decimal? TargetFoodCostPercent = null,
    decimal? TargetBeverageCostPercent = null,
    decimal? MinimumMarginPercent = null,
    decimal? WarningMarginPercent = null,
    decimal? PriceChangeAlertThreshold = null,
    decimal? CostIncreaseAlertThreshold = null,
    bool? AutoRecalculateCosts = null,
    bool? AutoCreateSnapshots = null,
    int? SnapshotFrequencyDays = null);

public record CreateSnapshotRequest(
    decimal MenuPrice,
    string SnapshotReason = "manual");
