using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Contracts;

/// <summary>
/// Request to configure the ingestion agent.
/// </summary>
public record ConfigureIngestionAgentRequest(
    int? PollingIntervalMinutes = null,
    bool? AutoProcessEnabled = null,
    decimal? AutoProcessConfidenceThreshold = null);

/// <summary>
/// Request to add a mailbox.
/// </summary>
public record AddMailboxRequest(
    string DisplayName,
    string Host,
    int Port,
    string Username,
    string Password,
    bool? UseSsl = null,
    string? FolderName = null,
    PurchaseDocumentType? DefaultDocumentType = null);

/// <summary>
/// Request to update agent settings.
/// </summary>
public record UpdateIngestionAgentSettingsRequest(
    int? PollingIntervalMinutes = null,
    bool? AutoProcessEnabled = null,
    decimal? AutoProcessConfidenceThreshold = null,
    string? SlackWebhookUrl = null,
    bool? SlackNotifyOnNewItem = null);

/// <summary>
/// Request to set routing rules.
/// </summary>
public record SetRoutingRulesRequest(
    List<RoutingRuleRequest> Rules);

/// <summary>
/// A routing rule in an API request.
/// </summary>
public record RoutingRuleRequest(
    string Name,
    RoutingRuleType Type,
    string Pattern,
    PurchaseDocumentType? SuggestedDocumentType = null,
    Guid? SuggestedVendorId = null,
    string? SuggestedVendorName = null,
    bool AutoApprove = false,
    int Priority = 100);

/// <summary>
/// Request to approve a processing plan.
/// </summary>
public record ApprovePlanRequest(
    Guid ApprovedBy);

/// <summary>
/// Request to modify and approve a processing plan.
/// </summary>
public record ModifyPlanRequest(
    Guid ModifiedBy,
    PurchaseDocumentType? DocumentType = null,
    Guid? VendorId = null,
    string? VendorName = null);

/// <summary>
/// Request to reject a processing plan.
/// </summary>
public record RejectPlanRequest(
    Guid RejectedBy,
    string? Reason = null);
