using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Commands
// ============================================================================

/// <summary>
/// Command to configure the ingestion agent for a site.
/// </summary>
[GenerateSerializer]
public record ConfigureIngestionAgentCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] int PollingIntervalMinutes = 5,
    [property: Id(3)] bool AutoProcessEnabled = true,
    [property: Id(4)] decimal AutoProcessConfidenceThreshold = 0.85m);

/// <summary>
/// Command to add a mailbox to the agent.
/// </summary>
[GenerateSerializer]
public record AddMailboxCommand(
    [property: Id(0)] string DisplayName,
    [property: Id(1)] string Host,
    [property: Id(2)] int Port,
    [property: Id(3)] string Username,
    [property: Id(4)] string Password,
    [property: Id(5)] bool UseSsl = true,
    [property: Id(6)] string FolderName = "INBOX",
    [property: Id(7)] PurchaseDocumentType DefaultDocumentType = PurchaseDocumentType.Invoice);

/// <summary>
/// Command to update agent settings.
/// </summary>
[GenerateSerializer]
public record UpdateIngestionAgentSettingsCommand(
    [property: Id(0)] int? PollingIntervalMinutes = null,
    [property: Id(1)] bool? AutoProcessEnabled = null,
    [property: Id(2)] decimal? AutoProcessConfidenceThreshold = null,
    [property: Id(3)] string? SlackWebhookUrl = null,
    [property: Id(4)] bool? SlackNotifyOnNewItem = null);

/// <summary>
/// Command to set routing rules.
/// </summary>
[GenerateSerializer]
public record SetRoutingRulesCommand(
    [property: Id(0)] List<RoutingRule> Rules);

// ============================================================================
// Results
// ============================================================================

/// <summary>
/// Snapshot of the ingestion agent state.
/// </summary>
[GenerateSerializer]
public record IngestionAgentSnapshot(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] bool IsActive,
    [property: Id(3)] int MailboxCount,
    [property: Id(4)] int PollingIntervalMinutes,
    [property: Id(5)] bool AutoProcessEnabled,
    [property: Id(6)] decimal AutoProcessConfidenceThreshold,
    [property: Id(7)] int PendingItemCount,
    [property: Id(8)] DateTime? LastPollAt,
    [property: Id(9)] int TotalPolls,
    [property: Id(10)] int TotalEmailsFetched,
    [property: Id(11)] int TotalDocumentsCreated,
    [property: Id(12)] int TotalAutoProcessed,
    [property: Id(13)] IReadOnlyList<MailboxConfigSnapshot> Mailboxes,
    [property: Id(14)] IReadOnlyList<RoutingRule> RoutingRules);

/// <summary>
/// Snapshot of a mailbox configuration (password excluded).
/// </summary>
[GenerateSerializer]
public record MailboxConfigSnapshot(
    [property: Id(0)] Guid ConfigId,
    [property: Id(1)] string DisplayName,
    [property: Id(2)] string Host,
    [property: Id(3)] int Port,
    [property: Id(4)] string Username,
    [property: Id(5)] bool UseSsl,
    [property: Id(6)] string FolderName,
    [property: Id(7)] bool IsEnabled,
    [property: Id(8)] PurchaseDocumentType DefaultDocumentType,
    [property: Id(9)] DateTime? LastPollAt);

/// <summary>
/// Result of a poll operation.
/// </summary>
[GenerateSerializer]
public record PollResultSnapshot(
    [property: Id(0)] int EmailsFetched,
    [property: Id(1)] int NewPendingItems,
    [property: Id(2)] int AutoProcessedItems,
    [property: Id(3)] int DuplicatesSkipped,
    [property: Id(4)] int Errors);

/// <summary>
/// A pending item in the ingestion queue for user review.
/// </summary>
[GenerateSerializer]
public record IngestionQueueItem(
    [property: Id(0)] Guid PlanId,
    [property: Id(1)] string EmailFrom,
    [property: Id(2)] string EmailSubject,
    [property: Id(3)] DateTime EmailReceivedAt,
    [property: Id(4)] int AttachmentCount,
    [property: Id(5)] PurchaseDocumentType SuggestedDocumentType,
    [property: Id(6)] string? SuggestedVendorName,
    [property: Id(7)] decimal TypeConfidence,
    [property: Id(8)] decimal VendorConfidence,
    [property: Id(9)] SuggestedAction SuggestedAction,
    [property: Id(10)] string Reasoning,
    [property: Id(11)] DateTime ProposedAt);

// ============================================================================
// Grain Interface
// ============================================================================

/// <summary>
/// Orchestrator grain for the invoice ingestion pipeline.
/// Manages IMAP polling, routing rules, processing plans, and user review queue.
/// Key: "{orgId}:{siteId}:ingestion-agent"
/// </summary>
public interface IInvoiceIngestionAgentGrain : IGrainWithStringKey, IRemindable
{
    /// <summary>
    /// Configure the agent for a site.
    /// </summary>
    Task<IngestionAgentSnapshot> ConfigureAsync(ConfigureIngestionAgentCommand command);

    /// <summary>
    /// Add a mailbox for polling.
    /// </summary>
    Task<IngestionAgentSnapshot> AddMailboxAsync(AddMailboxCommand command);

    /// <summary>
    /// Remove a mailbox.
    /// </summary>
    Task<IngestionAgentSnapshot> RemoveMailboxAsync(Guid configId);

    /// <summary>
    /// Update agent settings.
    /// </summary>
    Task<IngestionAgentSnapshot> UpdateSettingsAsync(UpdateIngestionAgentSettingsCommand command);

    /// <summary>
    /// Set routing rules (replaces all existing rules).
    /// </summary>
    Task<IngestionAgentSnapshot> SetRoutingRulesAsync(SetRoutingRulesCommand command);

    /// <summary>
    /// Activate the agent (start polling).
    /// </summary>
    Task<IngestionAgentSnapshot> ActivateAsync();

    /// <summary>
    /// Deactivate the agent (stop polling).
    /// </summary>
    Task<IngestionAgentSnapshot> DeactivateAsync();

    /// <summary>
    /// Trigger an immediate poll of all enabled mailboxes.
    /// </summary>
    Task<PollResultSnapshot> TriggerPollAsync(Guid? mailboxConfigId = null);

    /// <summary>
    /// Get the pending items queue for user review.
    /// </summary>
    Task<IReadOnlyList<IngestionQueueItem>> GetQueueAsync();

    /// <summary>
    /// Get processing history.
    /// </summary>
    Task<IReadOnlyList<IngestionHistoryEntry>> GetHistoryAsync(int limit = 50);

    /// <summary>
    /// Get the agent snapshot.
    /// </summary>
    Task<IngestionAgentSnapshot> GetSnapshotAsync();

    /// <summary>
    /// Check if the agent exists.
    /// </summary>
    Task<bool> ExistsAsync();
}
