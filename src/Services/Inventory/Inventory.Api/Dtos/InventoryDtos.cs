using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Inventory.Api.Dtos;

public class IngredientDto : HalResource
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string UnitOfMeasure { get; set; }
    public string? Category { get; set; }
    public string? StorageType { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal ReorderQuantity { get; set; }
    public decimal? CurrentStock { get; set; }
    public bool IsActive { get; set; }
    public bool IsLowStock { get; set; }
}

public class RecipeDto : HalResource
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public Guid? MenuItemId { get; set; }
    public int PortionYield { get; set; }
    public string? Instructions { get; set; }
    public decimal? CalculatedCost { get; set; }
    public decimal? CostPerPortion { get; set; }
    public DateTime? CostCalculatedAt { get; set; }
    public bool IsActive { get; set; }
    public List<RecipeIngredientDto> Ingredients { get; set; } = new();
}

public class RecipeIngredientDto : HalResource
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Guid IngredientId { get; set; }
    public string? IngredientName { get; set; }
    public string? IngredientCode { get; set; }
    public decimal Quantity { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal WastePercentage { get; set; }
    public decimal EffectiveQuantity { get; set; }
}

public class StockBatchDto : HalResource
{
    public Guid Id { get; set; }
    public Guid IngredientId { get; set; }
    public Guid LocationId { get; set; }
    public Guid? DeliveryId { get; set; }
    public string? IngredientName { get; set; }
    public decimal InitialQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? BatchNumber { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class StockLevelDto : HalResource
{
    public Guid IngredientId { get; set; }
    public required string IngredientCode { get; set; }
    public required string IngredientName { get; set; }
    public required string UnitOfMeasure { get; set; }
    public decimal TotalStock { get; set; }
    public decimal ReorderLevel { get; set; }
    public bool IsLowStock { get; set; }
    public int ActiveBatchCount { get; set; }
    public decimal AverageUnitCost { get; set; }
    public decimal TotalValue { get; set; }
}

public class ConsumptionResultDto
{
    public decimal TotalQuantityConsumed { get; set; }
    public decimal TotalCost { get; set; }
    public List<BatchConsumptionDto> BatchConsumptions { get; set; } = new();
}

public class BatchConsumptionDto
{
    public Guid BatchId { get; set; }
    public decimal QuantityConsumed { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Cost { get; set; }
}

public class WasteRecordDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public Guid IngredientId { get; set; }
    public string? IngredientName { get; set; }
    public Guid? StockBatchId { get; set; }
    public Guid RecordedByUserId { get; set; }
    public decimal Quantity { get; set; }
    public decimal EstimatedCost { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime RecordedAt { get; set; }
}

public record CreateIngredientRequest(
    string Code,
    string Name,
    string UnitOfMeasure,
    string? Category = null,
    string? StorageType = null,
    decimal ReorderLevel = 0,
    decimal ReorderQuantity = 0);

public record UpdateIngredientRequest(
    string? Name = null,
    string? UnitOfMeasure = null,
    string? Category = null,
    string? StorageType = null,
    decimal? ReorderLevel = null,
    decimal? ReorderQuantity = null,
    bool? IsActive = null);

public record CreateRecipeRequest(
    string Code,
    string Name,
    Guid? MenuItemId = null,
    int PortionYield = 1,
    string? Instructions = null);

public record UpdateRecipeRequest(
    string? Name = null,
    Guid? MenuItemId = null,
    int? PortionYield = null,
    string? Instructions = null,
    bool? IsActive = null);

public record AddRecipeIngredientRequest(
    Guid IngredientId,
    decimal Quantity,
    string? UnitOfMeasure = null,
    decimal WastePercentage = 0);

public record UpdateRecipeIngredientRequest(
    decimal? Quantity = null,
    string? UnitOfMeasure = null,
    decimal? WastePercentage = null);

public record CreateStockBatchRequest(
    Guid IngredientId,
    decimal Quantity,
    decimal UnitCost,
    Guid? DeliveryId = null,
    string? BatchNumber = null,
    DateTime? ExpiryDate = null);

public record ConsumeStockRequest(
    Guid IngredientId,
    decimal Quantity,
    Guid? OrderId = null,
    Guid? RecipeId = null,
    string ConsumptionType = "sale");

public record RecordWasteRequest(
    Guid IngredientId,
    decimal Quantity,
    string Reason,
    Guid RecordedByUserId,
    Guid? StockBatchId = null,
    string? Notes = null);
