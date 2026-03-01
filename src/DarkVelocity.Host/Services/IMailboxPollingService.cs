namespace DarkVelocity.Host.Services;

/// <summary>
/// Service for polling IMAP mailboxes to fetch new emails.
/// Implementations can use MailKit or similar libraries.
/// </summary>
public interface IMailboxPollingService
{
    /// <summary>
    /// Poll a mailbox for new messages since the given timestamp.
    /// </summary>
    Task<MailboxPollResult> PollAsync(
        MailboxConnectionConfig config,
        DateTime? since = null,
        CancellationToken ct = default);

    /// <summary>
    /// Mark a message as processed (move to processed folder or flag as read).
    /// </summary>
    Task MarkAsProcessedAsync(
        MailboxConnectionConfig config,
        string messageId,
        CancellationToken ct = default);

    /// <summary>
    /// Test the connection to a mailbox.
    /// </summary>
    Task<MailboxTestResult> TestConnectionAsync(
        MailboxConnectionConfig config,
        CancellationToken ct = default);
}

/// <summary>
/// IMAP/POP mailbox connection configuration.
/// </summary>
[GenerateSerializer]
public record MailboxConnectionConfig
{
    [Id(0)] public required string Host { get; init; }
    [Id(1)] public required int Port { get; init; }
    [Id(2)] public required bool UseSsl { get; init; }
    [Id(3)] public required string Username { get; init; }
    [Id(4)] public required string Password { get; init; }
    [Id(5)] public string FolderName { get; init; } = "INBOX";
}

/// <summary>
/// Result of polling a mailbox.
/// </summary>
[GenerateSerializer]
public record MailboxPollResult
{
    [Id(0)] public required bool Success { get; init; }
    [Id(1)] public required IReadOnlyList<ParsedEmail> Messages { get; init; }
    [Id(2)] public string? Error { get; init; }
    [Id(3)] public int TotalMessagesInFolder { get; init; }
}

/// <summary>
/// Result of testing a mailbox connection.
/// </summary>
[GenerateSerializer]
public record MailboxTestResult
{
    [Id(0)] public required bool Success { get; init; }
    [Id(1)] public string? Error { get; init; }
    [Id(2)] public int MessageCount { get; init; }
}
