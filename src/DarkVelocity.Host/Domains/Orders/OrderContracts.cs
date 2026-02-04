using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Contracts;

public record CreateOrderRequest(
    Guid CreatedBy,
    OrderType Type,
    Guid? TableId = null,
    string? TableNumber = null,
    Guid? CustomerId = null,
    int GuestCount = 1);

/// <summary>
/// Request to add a bundle component selection.
/// </summary>
public record BundleComponentRequest(
    string SlotId,
    string SlotName,
    string ItemDocumentId,
    string ItemName,
    int Quantity = 1,
    decimal PriceAdjustment = 0,
    List<OrderLineModifier>? Modifiers = null);

public record AddLineRequest(
    Guid MenuItemId,
    string Name,
    int Quantity,
    decimal UnitPrice,
    string? Notes = null,
    List<OrderLineModifier>? Modifiers = null,
    /// <summary>
    /// Tax rate as a percentage (e.g., 10.0 for 10% tax).
    /// Should be set based on order type and menu item's contextual tax rates.
    /// Defaults to 0 (no tax) if not specified.
    /// </summary>
    decimal TaxRate = 0,
    /// <summary>
    /// Whether this line is a bundle/combo item.
    /// </summary>
    bool IsBundle = false,
    /// <summary>
    /// Selected components for bundle items.
    /// </summary>
    List<BundleComponentRequest>? BundleComponents = null);

public record SendOrderRequest(Guid SentBy);
public record CloseOrderRequest(Guid ClosedBy);
public record VoidOrderRequest(Guid VoidedBy, string Reason);

public record ApplyDiscountRequest(
    string Name,
    DiscountType Type,
    decimal Value,
    Guid AppliedBy,
    Guid? DiscountId = null,
    string? Reason = null,
    Guid? ApprovedBy = null);

// Bill Splitting Requests
public record SplitByItemsRequest(
    List<Guid> LineIds,
    Guid SplitBy,
    int? GuestCount = null);

public record SplitByAmountsRequest(
    List<decimal> Amounts);

// Bill Splitting Responses
public record SplitByItemsResponse(
    Guid NewOrderId,
    string NewOrderNumber,
    int LinesMoved,
    decimal NewOrderTotal,
    decimal RemainingOrderTotal);

public record SplitPaymentResponse(
    decimal TotalAmount,
    decimal BalanceDue,
    List<SplitShareResponse> Shares,
    bool IsValid);

public record SplitShareResponse(
    int ShareNumber,
    decimal Amount,
    decimal Tax,
    decimal Total,
    string? Label);

// Hold/Fire Workflow Requests
public record HoldItemsRequest(
    List<Guid> LineIds,
    Guid HeldBy,
    string? Reason = null);

public record ReleaseItemsRequest(
    List<Guid> LineIds,
    Guid ReleasedBy);

public record SetItemCourseRequest(
    List<Guid> LineIds,
    int CourseNumber,
    Guid SetBy);

public record FireItemsRequest(
    List<Guid> LineIds,
    Guid FiredBy);

public record FireCourseRequest(
    int CourseNumber,
    Guid FiredBy);

public record FireAllRequest(
    Guid FiredBy);

// Hold/Fire Workflow Responses
public record FireResultResponse(
    int FiredCount,
    List<Guid> FiredLineIds,
    DateTime FiredAt);

public record HoldSummaryResponse(
    int TotalHeldCount,
    Dictionary<int, int> HeldByCourseCounts,
    List<Guid> HeldLineIds);

public record CourseSummaryResponse(
    Dictionary<int, int> ItemCountByCourse);
