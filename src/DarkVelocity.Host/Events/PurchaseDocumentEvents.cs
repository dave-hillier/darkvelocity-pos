namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all PurchaseDocument events used in event sourcing.
/// </summary>
public interface IPurchaseDocumentEvent
{
    Guid DocumentId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentCreated : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public Guid SiteId { get; init; }
    [Id(3)] public string DocumentType { get; init; } = ""; // Invoice, Receipt, PurchaseOrder
    [Id(4)] public string DocumentNumber { get; init; } = "";
    [Id(5)] public Guid VendorId { get; init; }
    [Id(6)] public string VendorName { get; init; } = "";
    [Id(7)] public DateOnly DocumentDate { get; init; }
    [Id(8)] public string? Source { get; init; }
    [Id(9)] public Guid CreatedBy { get; init; }
    [Id(10)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentLineAdded : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public string Description { get; init; } = "";
    [Id(3)] public decimal Quantity { get; init; }
    [Id(4)] public string Unit { get; init; } = "";
    [Id(5)] public decimal UnitPrice { get; init; }
    [Id(6)] public decimal LineTotal { get; init; }
    [Id(7)] public Guid? IngredientId { get; init; }
    [Id(8)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentLineUpdated : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public decimal? Quantity { get; init; }
    [Id(3)] public decimal? UnitPrice { get; init; }
    [Id(4)] public decimal? LineTotal { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentLineRemoved : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentTotalsUpdated : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public decimal Subtotal { get; init; }
    [Id(2)] public decimal TaxAmount { get; init; }
    [Id(3)] public decimal ShippingAmount { get; init; }
    [Id(4)] public decimal TotalAmount { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentSubmitted : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid SubmittedBy { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentApproved : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid ApprovedBy { get; init; }
    [Id(2)] public string? Notes { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentRejected : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid RejectedBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentReceived : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid ReceivedBy { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentPartiallyReceived : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public List<LineReceiptInfo> LinesReceived { get; init; } = [];
    [Id(2)] public Guid ReceivedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record LineReceiptInfo
{
    [Id(0)] public Guid LineId { get; init; }
    [Id(1)] public decimal QuantityReceived { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentPaid : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public decimal AmountPaid { get; init; }
    [Id(2)] public string PaymentMethod { get; init; } = "";
    [Id(3)] public string? PaymentReference { get; init; }
    [Id(4)] public Guid PaidBy { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentCancelled : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid CancelledBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentLinkedToDelivery : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid DeliveryId { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentProcessingRequested : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentExtractionApplied : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public string? VendorName { get; init; }
    [Id(2)] public DateOnly? DocumentDate { get; init; }
    [Id(3)] public decimal? Total { get; init; }
    [Id(4)] public int LineCount { get; init; }
    [Id(5)] public decimal Confidence { get; init; }
    [Id(6)] public string? ProcessorVersion { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentExtractionFailed : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public string Reason { get; init; } = "";
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentLineMapped : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public int LineIndex { get; init; }
    [Id(2)] public Guid IngredientId { get; init; }
    [Id(3)] public string? IngredientSku { get; init; }
    [Id(4)] public string? IngredientName { get; init; }
    [Id(5)] public string MappingSource { get; init; } = "";
    [Id(6)] public decimal Confidence { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentLineUnmapped : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public int LineIndex { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentLineModified : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public int LineIndex { get; init; }
    [Id(2)] public string? Description { get; init; }
    [Id(3)] public decimal? Quantity { get; init; }
    [Id(4)] public string? Unit { get; init; }
    [Id(5)] public decimal? UnitPrice { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentConfirmed : IPurchaseDocumentEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid ConfirmedBy { get; init; }
    [Id(2)] public Guid? VendorId { get; init; }
    [Id(3)] public string? VendorName { get; init; }
    [Id(4)] public DateOnly? DocumentDate { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}
