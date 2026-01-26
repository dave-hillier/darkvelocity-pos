using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Procurement.Api.Dtos;

// Supplier DTOs
public class SupplierDto : HalResource
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public int PaymentTermsDays { get; set; }
    public int LeadTimeDays { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public int ProductCount { get; set; }
}

public class SupplierIngredientDto : HalResource
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public Guid IngredientId { get; set; }
    public string? SupplierProductCode { get; set; }
    public string? SupplierProductName { get; set; }
    public decimal PackSize { get; set; }
    public string? PackUnit { get; set; }
    public decimal LastKnownPrice { get; set; }
    public DateTime? LastPriceUpdatedAt { get; set; }
    public bool IsPreferred { get; set; }
    public bool IsActive { get; set; }
}

public record CreateSupplierRequest(
    string Code,
    string Name,
    string? ContactName = null,
    string? ContactEmail = null,
    string? ContactPhone = null,
    string? Address = null,
    int PaymentTermsDays = 30,
    int LeadTimeDays = 3,
    string? Notes = null);

public record UpdateSupplierRequest(
    string? Name = null,
    string? ContactName = null,
    string? ContactEmail = null,
    string? ContactPhone = null,
    string? Address = null,
    int? PaymentTermsDays = null,
    int? LeadTimeDays = null,
    string? Notes = null,
    bool? IsActive = null);

public record AddSupplierIngredientRequest(
    Guid IngredientId,
    decimal LastKnownPrice,
    string? SupplierProductCode = null,
    string? SupplierProductName = null,
    decimal PackSize = 1m,
    string? PackUnit = null,
    bool IsPreferred = false);

public record UpdateSupplierIngredientRequest(
    decimal? LastKnownPrice = null,
    string? SupplierProductCode = null,
    string? SupplierProductName = null,
    decimal? PackSize = null,
    string? PackUnit = null,
    bool? IsPreferred = null,
    bool? IsActive = null);

// Purchase Order DTOs
public class PurchaseOrderDto : HalResource
{
    public Guid Id { get; set; }
    public required string OrderNumber { get; set; }
    public Guid SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public Guid LocationId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public required string Status { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public decimal OrderTotal { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseOrderLineDto> Lines { get; set; } = new();
}

public class PurchaseOrderLineDto : HalResource
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public Guid IngredientId { get; set; }
    public string? IngredientName { get; set; }
    public decimal QuantityOrdered { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string? Notes { get; set; }
}

public record CreatePurchaseOrderRequest(
    Guid SupplierId,
    Guid LocationId,
    Guid? CreatedByUserId = null,
    DateTime? ExpectedDeliveryDate = null,
    string? Notes = null);

public record AddPurchaseOrderLineRequest(
    Guid IngredientId,
    string IngredientName,
    decimal QuantityOrdered,
    decimal UnitPrice,
    string? Notes = null);

public record UpdatePurchaseOrderLineRequest(
    decimal? QuantityOrdered = null,
    decimal? UnitPrice = null,
    string? Notes = null);

public record SubmitPurchaseOrderRequest();

public record CancelPurchaseOrderRequest(
    string Reason);

// Delivery DTOs
public class DeliveryDto : HalResource
{
    public Guid Id { get; set; }
    public required string DeliveryNumber { get; set; }
    public Guid SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public string? PurchaseOrderNumber { get; set; }
    public Guid LocationId { get; set; }
    public Guid? ReceivedByUserId { get; set; }
    public required string Status { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public decimal TotalValue { get; set; }
    public bool HasDiscrepancies { get; set; }
    public string? SupplierInvoiceNumber { get; set; }
    public string? Notes { get; set; }
    public List<DeliveryLineDto> Lines { get; set; } = new();
    public List<DeliveryDiscrepancyDto> Discrepancies { get; set; } = new();
}

public class DeliveryLineDto : HalResource
{
    public Guid Id { get; set; }
    public Guid DeliveryId { get; set; }
    public Guid IngredientId { get; set; }
    public string? IngredientName { get; set; }
    public Guid? PurchaseOrderLineId { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Notes { get; set; }
}

public class DeliveryDiscrepancyDto : HalResource
{
    public Guid Id { get; set; }
    public Guid DeliveryId { get; set; }
    public Guid DeliveryLineId { get; set; }
    public required string DiscrepancyType { get; set; }
    public decimal? QuantityAffected { get; set; }
    public decimal? PriceDifference { get; set; }
    public string? Description { get; set; }
    public required string ActionTaken { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }
}

public record CreateDeliveryRequest(
    Guid SupplierId,
    Guid LocationId,
    Guid? PurchaseOrderId = null,
    Guid? ReceivedByUserId = null,
    string? SupplierInvoiceNumber = null,
    string? Notes = null);

public record AddDeliveryLineRequest(
    Guid IngredientId,
    string IngredientName,
    decimal QuantityReceived,
    decimal UnitCost,
    Guid? PurchaseOrderLineId = null,
    string? BatchNumber = null,
    DateTime? ExpiryDate = null,
    string? Notes = null);

public record AddDeliveryDiscrepancyRequest(
    Guid DeliveryLineId,
    string DiscrepancyType,
    decimal? QuantityAffected = null,
    decimal? PriceDifference = null,
    string? Description = null);

public record ResolveDiscrepancyRequest(
    string ActionTaken,
    Guid? ResolvedByUserId = null,
    string? ResolutionNotes = null);

public record AcceptDeliveryRequest(
    Guid? AcceptedByUserId = null);

public record RejectDeliveryRequest(
    string Reason,
    Guid? RejectedByUserId = null);
