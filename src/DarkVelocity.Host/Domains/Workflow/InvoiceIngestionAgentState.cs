using DarkVelocity.Host.Events;
using DarkVelocity.Host.Services;

namespace DarkVelocity.Host.State;

/// <summary>
/// Persistent state for the invoice ingestion agent.
/// Tracks mailbox configuration, routing rules, processing history, and stats.
/// </summary>
[GenerateSerializer]
public sealed class InvoiceIngestionAgentState
{
    [Id(0)] public Guid OrganizationId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public bool IsActive { get; set; }

    /// <summary>Configured mailboxes for IMAP polling.</summary>
    [Id(3)] public List<MailboxConfig> Mailboxes { get; set; } = [];

    /// <summary>How often to poll mailboxes (minutes).</summary>
    [Id(4)] public int PollingIntervalMinutes { get; set; } = 5;

    /// <summary>Routing rules for classifying incoming emails.</summary>
    [Id(5)] public List<RoutingRule> RoutingRules { get; set; } = [];

    /// <summary>Whether to auto-process items that match rules with high confidence.</summary>
    [Id(6)] public bool AutoProcessEnabled { get; set; } = true;

    /// <summary>Minimum confidence threshold for auto-processing (0.0 - 1.0).</summary>
    [Id(7)] public decimal AutoProcessConfidenceThreshold { get; set; } = 0.85m;

    /// <summary>IDs of DocumentProcessingPlanGrains awaiting user review.</summary>
    [Id(8)] public List<Guid> PendingPlanIds { get; set; } = [];

    /// <summary>Recent processing history (capped at MaxHistoryEntries).</summary>
    [Id(9)] public List<IngestionHistoryEntry> RecentHistory { get; set; } = [];

    /// <summary>When the last poll occurred.</summary>
    [Id(10)] public DateTime? LastPollAt { get; set; }

    // Stats
    [Id(11)] public int TotalPolls { get; set; }
    [Id(12)] public int TotalEmailsFetched { get; set; }
    [Id(13)] public int TotalDocumentsCreated { get; set; }
    [Id(14)] public int TotalAutoProcessed { get; set; }
    [Id(15)] public int TotalPendingReview { get; set; }

    [Id(16)] public int Version { get; set; }

    /// <summary>Optional Slack webhook URL for notifications.</summary>
    [Id(17)] public string? SlackWebhookUrl { get; set; }

    /// <summary>Whether to notify Slack on each new pending item.</summary>
    [Id(18)] public bool SlackNotifyOnNewItem { get; set; }

    public const int MaxHistoryEntries = 200;
}

/// <summary>
/// Configuration for a single IMAP mailbox.
/// </summary>
[GenerateSerializer]
public sealed record MailboxConfig
{
    [Id(0)] public required Guid ConfigId { get; init; }
    [Id(1)] public required string DisplayName { get; init; }
    [Id(2)] public required string Host { get; init; }
    [Id(3)] public required int Port { get; init; }
    [Id(4)] public required string Username { get; init; }
    [Id(5)] public required string Password { get; init; }
    [Id(6)] public bool UseSsl { get; init; } = true;
    [Id(7)] public string FolderName { get; init; } = "INBOX";
    [Id(8)] public bool IsEnabled { get; init; } = true;
    [Id(9)] public PurchaseDocumentType DefaultDocumentType { get; init; } = PurchaseDocumentType.Invoice;
    [Id(10)] public DateTime? LastPollAt { get; init; }
    [Id(11)] public string? LastSeenMessageId { get; init; }

    public MailboxConnectionConfig ToConnectionConfig() => new()
    {
        Host = Host,
        Port = Port,
        UseSsl = UseSsl,
        Username = Username,
        Password = Password,
        FolderName = FolderName
    };
}

/// <summary>
/// A routing rule that maps email patterns to document classification.
/// </summary>
[GenerateSerializer]
public sealed record RoutingRule
{
    [Id(0)] public required Guid RuleId { get; init; }
    [Id(1)] public required string Name { get; init; }
    [Id(2)] public required RoutingRuleType Type { get; init; }
    [Id(3)] public required string Pattern { get; init; }
    [Id(4)] public PurchaseDocumentType? SuggestedDocumentType { get; init; }
    [Id(5)] public Guid? SuggestedVendorId { get; init; }
    [Id(6)] public string? SuggestedVendorName { get; init; }
    [Id(7)] public bool AutoApprove { get; init; }
    [Id(8)] public int Priority { get; init; }
}

/// <summary>
/// Type of pattern matching for routing rules.
/// </summary>
public enum RoutingRuleType
{
    SenderDomain,
    SenderEmail,
    SubjectPattern
}

/// <summary>
/// Record of a processed email in the ingestion history.
/// </summary>
[GenerateSerializer]
public sealed record IngestionHistoryEntry
{
    [Id(0)] public required Guid EntryId { get; init; }
    [Id(1)] public required string EmailMessageId { get; init; }
    [Id(2)] public required string From { get; init; }
    [Id(3)] public required string Subject { get; init; }
    [Id(4)] public required DateTime ReceivedAt { get; init; }
    [Id(5)] public required DateTime ProcessedAt { get; init; }
    [Id(6)] public required IngestionOutcome Outcome { get; init; }
    [Id(7)] public Guid? PlanId { get; init; }
    [Id(8)] public IReadOnlyList<Guid>? DocumentIds { get; init; }
    [Id(9)] public string? Error { get; init; }
}

/// <summary>
/// Outcome of processing an ingested email.
/// </summary>
public enum IngestionOutcome
{
    PendingReview,
    AutoProcessed,
    Duplicate,
    Rejected,
    Failed
}
