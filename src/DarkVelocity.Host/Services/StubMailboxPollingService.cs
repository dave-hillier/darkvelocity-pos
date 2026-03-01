using Microsoft.Extensions.Logging;

namespace DarkVelocity.Host.Services;

/// <summary>
/// Stub implementation of mailbox polling for development.
/// Returns configurable mock emails. In production, replace with MailKit-based implementation.
/// </summary>
public class StubMailboxPollingService : IMailboxPollingService
{
    private readonly ILogger<StubMailboxPollingService> _logger;
    private int _pollCount;

    public StubMailboxPollingService(ILogger<StubMailboxPollingService> logger)
    {
        _logger = logger;
    }

    public Task<MailboxPollResult> PollAsync(
        MailboxConnectionConfig config,
        DateTime? since = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Stub: Polling mailbox {Host}:{Port}/{Folder} (since: {Since})",
            config.Host, config.Port, config.FolderName, since);

        _pollCount++;

        // Return a mock email on every 3rd poll to simulate realistic behavior
        var messages = new List<ParsedEmail>();
        if (_pollCount % 3 == 1)
        {
            messages.Add(new ParsedEmail
            {
                MessageId = $"stub-poll-{Guid.NewGuid():N}@{config.Host}",
                From = "accounts@acme-foods.com",
                FromName = "Acme Foods Accounts",
                To = config.Username,
                Subject = $"Invoice #INV-{DateTime.UtcNow:yyyyMMdd}-{_pollCount:D4}",
                TextBody = "Please find attached invoice for your recent order.",
                SentAt = DateTime.UtcNow.AddMinutes(-15),
                ReceivedAt = DateTime.UtcNow,
                Attachments =
                [
                    new EmailAttachment
                    {
                        Filename = $"invoice-{_pollCount:D4}.pdf",
                        ContentType = "application/pdf",
                        SizeBytes = 52480,
                        Content = GenerateMockPdfContent()
                    }
                ]
            });
        }

        return Task.FromResult(new MailboxPollResult
        {
            Success = true,
            Messages = messages,
            TotalMessagesInFolder = 42 + _pollCount
        });
    }

    public Task MarkAsProcessedAsync(
        MailboxConnectionConfig config,
        string messageId,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Stub: Marked message {MessageId} as processed", messageId);
        return Task.CompletedTask;
    }

    public Task<MailboxTestResult> TestConnectionAsync(
        MailboxConnectionConfig config,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Stub: Testing connection to {Host}:{Port}",
            config.Host, config.Port);

        return Task.FromResult(new MailboxTestResult
        {
            Success = true,
            MessageCount = 42
        });
    }

    private static byte[] GenerateMockPdfContent()
    {
        var pdfContent = "%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF";
        return System.Text.Encoding.ASCII.GetBytes(pdfContent);
    }
}
