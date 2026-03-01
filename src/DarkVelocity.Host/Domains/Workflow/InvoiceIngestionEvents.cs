using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Events;

// ============================================================================
// Invoice Ingestion Agent Events
// ============================================================================

/// <summary>
/// An ingestion agent was configured for a site.
/// </summary>
[GenerateSerializer]
public sealed record IngestionAgentConfigured : DomainEvent
{
    public override string EventType => "ingestion.agent.configured";
    public override string AggregateType => "IngestionAgent";

    [Id(100)] public required int MailboxCount { get; init; }
    [Id(101)] public required int PollingIntervalMinutes { get; init; }
    [Id(102)] public required bool AutoProcessEnabled { get; init; }
}

/// <summary>
/// An ingestion poll completed.
/// </summary>
[GenerateSerializer]
public sealed record IngestionPollCompleted : DomainEvent
{
    public override string EventType => "ingestion.poll.completed";
    public override string AggregateType => "IngestionAgent";

    [Id(100)] public required int EmailsFetched { get; init; }
    [Id(101)] public required int NewPendingItems { get; init; }
    [Id(102)] public required int AutoProcessedItems { get; init; }
    [Id(103)] public required int DuplicatesSkipped { get; init; }
}

/// <summary>
/// A new item was received and added to the pending queue.
/// </summary>
[GenerateSerializer]
public sealed record IngestionItemReceived : DomainEvent
{
    public override string EventType => "ingestion.item.received";
    public override string AggregateType => "IngestionAgent";

    [Id(100)] public required Guid PlanId { get; init; }
    [Id(101)] public required string EmailFrom { get; init; }
    [Id(102)] public required string EmailSubject { get; init; }
    [Id(103)] public required PurchaseDocumentType SuggestedType { get; init; }
    [Id(104)] public required decimal Confidence { get; init; }
}

/// <summary>
/// An item was auto-processed based on routing rules.
/// </summary>
[GenerateSerializer]
public sealed record IngestionItemAutoProcessed : DomainEvent
{
    public override string EventType => "ingestion.item.auto_processed";
    public override string AggregateType => "IngestionAgent";

    [Id(100)] public required Guid PlanId { get; init; }
    [Id(101)] public required string EmailFrom { get; init; }
    [Id(102)] public required Guid? MatchedRuleId { get; init; }
    [Id(103)] public required IReadOnlyList<Guid> DocumentIds { get; init; }
}

// ============================================================================
// Document Processing Plan Events
// ============================================================================

/// <summary>
/// A processing plan was proposed for an ingested email.
/// </summary>
[GenerateSerializer]
public sealed record ProcessingPlanProposed : DomainEvent
{
    public override string EventType => "processing.plan.proposed";
    public override string AggregateType => "DocumentProcessingPlan";

    [Id(100)] public required Guid PlanId { get; init; }
    [Id(101)] public required string EmailFrom { get; init; }
    [Id(102)] public required string EmailSubject { get; init; }
    [Id(103)] public required PurchaseDocumentType SuggestedType { get; init; }
    [Id(104)] public required SuggestedAction SuggestedAction { get; init; }
}

/// <summary>
/// A processing plan was approved by a user.
/// </summary>
[GenerateSerializer]
public sealed record ProcessingPlanApproved : DomainEvent
{
    public override string EventType => "processing.plan.approved";
    public override string AggregateType => "DocumentProcessingPlan";

    [Id(100)] public required Guid PlanId { get; init; }
    [Id(101)] public required Guid ApprovedBy { get; init; }
}

/// <summary>
/// A processing plan was modified and approved by a user.
/// </summary>
[GenerateSerializer]
public sealed record ProcessingPlanModified : DomainEvent
{
    public override string EventType => "processing.plan.modified";
    public override string AggregateType => "DocumentProcessingPlan";

    [Id(100)] public required Guid PlanId { get; init; }
    [Id(101)] public required Guid ModifiedBy { get; init; }
    [Id(102)] public PurchaseDocumentType? NewDocumentType { get; init; }
    [Id(103)] public string? NewVendorName { get; init; }
}

/// <summary>
/// A processing plan was rejected by a user.
/// </summary>
[GenerateSerializer]
public sealed record ProcessingPlanRejected : DomainEvent
{
    public override string EventType => "processing.plan.rejected";
    public override string AggregateType => "DocumentProcessingPlan";

    [Id(100)] public required Guid PlanId { get; init; }
    [Id(101)] public required Guid RejectedBy { get; init; }
    [Id(102)] public string? Reason { get; init; }
}

/// <summary>
/// A processing plan was executed successfully.
/// </summary>
[GenerateSerializer]
public sealed record ProcessingPlanExecuted : DomainEvent
{
    public override string EventType => "processing.plan.executed";
    public override string AggregateType => "DocumentProcessingPlan";

    [Id(100)] public required Guid PlanId { get; init; }
    [Id(101)] public required IReadOnlyList<Guid> DocumentIds { get; init; }
}
