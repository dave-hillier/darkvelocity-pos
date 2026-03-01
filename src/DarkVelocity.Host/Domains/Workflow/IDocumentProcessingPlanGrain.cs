using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Commands
// ============================================================================

/// <summary>
/// Command to propose a processing plan for an ingested email.
/// </summary>
[GenerateSerializer]
public record ProposePlanCommand(
    [property: Id(0)] Guid PlanId,
    [property: Id(1)] Guid OrganizationId,
    [property: Id(2)] Guid SiteId,
    [property: Id(3)] string EmailMessageId,
    [property: Id(4)] string EmailFrom,
    [property: Id(5)] string EmailSubject,
    [property: Id(6)] DateTime EmailReceivedAt,
    [property: Id(7)] int AttachmentCount,
    [property: Id(8)] PurchaseDocumentType SuggestedDocumentType,
    [property: Id(9)] Guid? SuggestedVendorId,
    [property: Id(10)] string? SuggestedVendorName,
    [property: Id(11)] decimal TypeConfidence,
    [property: Id(12)] decimal VendorConfidence,
    [property: Id(13)] SuggestedAction SuggestedAction,
    [property: Id(14)] Guid? MatchedRuleId,
    [property: Id(15)] string Reasoning,
    [property: Id(16)] IReadOnlyList<Guid> DocumentIds);

/// <summary>
/// Command to modify a plan and approve it.
/// </summary>
[GenerateSerializer]
public record ModifyPlanCommand(
    [property: Id(0)] Guid ModifiedBy,
    [property: Id(1)] PurchaseDocumentType? DocumentType = null,
    [property: Id(2)] Guid? VendorId = null,
    [property: Id(3)] string? VendorName = null);

// ============================================================================
// Results
// ============================================================================

/// <summary>
/// Snapshot of a document processing plan.
/// </summary>
[GenerateSerializer]
public record PlanSnapshot(
    [property: Id(0)] Guid PlanId,
    [property: Id(1)] Guid OrganizationId,
    [property: Id(2)] Guid SiteId,
    [property: Id(3)] ProcessingPlanStatus Status,
    [property: Id(4)] string EmailMessageId,
    [property: Id(5)] string EmailFrom,
    [property: Id(6)] string EmailSubject,
    [property: Id(7)] DateTime EmailReceivedAt,
    [property: Id(8)] int AttachmentCount,
    [property: Id(9)] PurchaseDocumentType SuggestedDocumentType,
    [property: Id(10)] Guid? SuggestedVendorId,
    [property: Id(11)] string? SuggestedVendorName,
    [property: Id(12)] decimal TypeConfidence,
    [property: Id(13)] decimal VendorConfidence,
    [property: Id(14)] SuggestedAction SuggestedAction,
    [property: Id(15)] string Reasoning,
    [property: Id(16)] PurchaseDocumentType? OverrideDocumentType,
    [property: Id(17)] string? OverrideVendorName,
    [property: Id(18)] IReadOnlyList<Guid> DocumentIds,
    [property: Id(19)] DateTime? ExecutedAt,
    [property: Id(20)] Guid? ReviewedBy,
    [property: Id(21)] DateTime? ReviewedAt,
    [property: Id(22)] string? RejectionReason,
    [property: Id(23)] DateTime ProposedAt);

// ============================================================================
// Grain Interface
// ============================================================================

/// <summary>
/// Grain managing the lifecycle of a document processing plan.
/// Plans are proposed when emails are ingested, then approved/rejected by users.
/// Key: "{orgId}:{siteId}:doc-plan:{planId}"
/// </summary>
public interface IDocumentProcessingPlanGrain : IGrainWithStringKey
{
    /// <summary>
    /// Propose a new plan for an ingested email.
    /// </summary>
    Task<PlanSnapshot> ProposeAsync(ProposePlanCommand command);

    /// <summary>
    /// Approve the plan as-is.
    /// </summary>
    Task<PlanSnapshot> ApproveAsync(Guid approvedBy);

    /// <summary>
    /// Modify the plan and approve it.
    /// </summary>
    Task<PlanSnapshot> ModifyAndApproveAsync(ModifyPlanCommand command);

    /// <summary>
    /// Reject the plan.
    /// </summary>
    Task<PlanSnapshot> RejectAsync(Guid rejectedBy, string? reason = null);

    /// <summary>
    /// Get the plan snapshot.
    /// </summary>
    Task<PlanSnapshot> GetSnapshotAsync();

    /// <summary>
    /// Check if the plan exists.
    /// </summary>
    Task<bool> ExistsAsync();
}
