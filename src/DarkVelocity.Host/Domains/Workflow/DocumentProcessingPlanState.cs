using DarkVelocity.Host.Events;

namespace DarkVelocity.Host.State;

/// <summary>
/// Persistent state for a document processing plan.
/// Plans are ephemeral workflow state: proposed when an email is ingested,
/// then approved/rejected by the user.
/// </summary>
[GenerateSerializer]
public sealed class DocumentProcessingPlanState
{
    [Id(0)] public Guid PlanId { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid SiteId { get; set; }
    [Id(3)] public ProcessingPlanStatus Status { get; set; }

    // Email metadata
    [Id(4)] public string EmailMessageId { get; set; } = string.Empty;
    [Id(5)] public string EmailFrom { get; set; } = string.Empty;
    [Id(6)] public string EmailSubject { get; set; } = string.Empty;
    [Id(7)] public DateTime EmailReceivedAt { get; set; }
    [Id(8)] public int AttachmentCount { get; set; }

    // Suggested plan (from routing rules / heuristics)
    [Id(9)] public PurchaseDocumentType SuggestedDocumentType { get; set; }
    [Id(10)] public Guid? SuggestedVendorId { get; set; }
    [Id(11)] public string? SuggestedVendorName { get; set; }
    [Id(12)] public decimal TypeConfidence { get; set; }
    [Id(13)] public decimal VendorConfidence { get; set; }
    [Id(14)] public SuggestedAction SuggestedAction { get; set; }
    [Id(15)] public Guid? MatchedRuleId { get; set; }
    [Id(16)] public string Reasoning { get; set; } = string.Empty;

    // User overrides (applied on modify)
    [Id(17)] public PurchaseDocumentType? OverrideDocumentType { get; set; }
    [Id(18)] public Guid? OverrideVendorId { get; set; }
    [Id(19)] public string? OverrideVendorName { get; set; }

    // Execution result
    [Id(20)] public List<Guid> DocumentIds { get; set; } = [];
    [Id(21)] public DateTime? ExecutedAt { get; set; }
    [Id(22)] public string? ExecutionError { get; set; }

    // Audit
    [Id(23)] public DateTime ProposedAt { get; set; }
    [Id(24)] public Guid? ReviewedBy { get; set; }
    [Id(25)] public DateTime? ReviewedAt { get; set; }
    [Id(26)] public string? RejectionReason { get; set; }
    [Id(27)] public int Version { get; set; }
}

/// <summary>
/// Lifecycle status of a document processing plan.
/// </summary>
public enum ProcessingPlanStatus
{
    Proposed,
    Approved,
    Executing,
    Executed,
    Modified,
    Rejected,
    Failed
}

/// <summary>
/// Suggested action for a processing plan.
/// </summary>
public enum SuggestedAction
{
    AutoProcess,
    ManualReview
}
