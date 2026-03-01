using System.Text.RegularExpressions;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Services;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Orchestrator grain for the invoice ingestion pipeline.
/// Manages IMAP polling, routing rules, processing plans, and the user review queue.
/// </summary>
public class InvoiceIngestionAgentGrain : Grain, IInvoiceIngestionAgentGrain
{
    private readonly IPersistentState<InvoiceIngestionAgentState> _state;
    private readonly IGrainFactory _grainFactory;
    private readonly IMailboxPollingService _mailboxPolling;
    private readonly ILogger<InvoiceIngestionAgentGrain> _logger;

    private const string PollReminderName = "ingestion-poll";

    public InvoiceIngestionAgentGrain(
        [PersistentState("ingestion-agent", "OrleansStorage")]
        IPersistentState<InvoiceIngestionAgentState> state,
        IGrainFactory grainFactory,
        IMailboxPollingService mailboxPolling,
        ILogger<InvoiceIngestionAgentGrain> logger)
    {
        _state = state;
        _grainFactory = grainFactory;
        _mailboxPolling = mailboxPolling;
        _logger = logger;
    }

    public async Task<IngestionAgentSnapshot> ConfigureAsync(ConfigureIngestionAgentCommand command)
    {
        if (_state.State.SiteId != Guid.Empty)
            throw new InvalidOperationException("Agent already configured");

        _state.State = new InvoiceIngestionAgentState
        {
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            IsActive = false,
            PollingIntervalMinutes = command.PollingIntervalMinutes,
            AutoProcessEnabled = command.AutoProcessEnabled,
            AutoProcessConfidenceThreshold = command.AutoProcessConfidenceThreshold,
            Version = 1
        };

        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Ingestion agent configured for site {SiteId}, polling every {Interval}m",
            command.SiteId, command.PollingIntervalMinutes);

        return ToSnapshot();
    }

    public async Task<IngestionAgentSnapshot> AddMailboxAsync(AddMailboxCommand command)
    {
        EnsureExists();

        var config = new MailboxConfig
        {
            ConfigId = Guid.NewGuid(),
            DisplayName = command.DisplayName,
            Host = command.Host,
            Port = command.Port,
            Username = command.Username,
            Password = command.Password,
            UseSsl = command.UseSsl,
            FolderName = command.FolderName,
            IsEnabled = true,
            DefaultDocumentType = command.DefaultDocumentType
        };

        _state.State.Mailboxes.Add(config);
        _state.State.Version++;
        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Mailbox {DisplayName} ({Host}:{Port}) added to agent for site {SiteId}",
            command.DisplayName, command.Host, command.Port, _state.State.SiteId);

        return ToSnapshot();
    }

    public async Task<IngestionAgentSnapshot> RemoveMailboxAsync(Guid configId)
    {
        EnsureExists();

        var removed = _state.State.Mailboxes.RemoveAll(m => m.ConfigId == configId);
        if (removed == 0)
            throw new InvalidOperationException($"Mailbox {configId} not found");

        _state.State.Version++;
        await _state.WriteStateAsync();

        return ToSnapshot();
    }

    public async Task<IngestionAgentSnapshot> UpdateSettingsAsync(UpdateIngestionAgentSettingsCommand command)
    {
        EnsureExists();

        if (command.PollingIntervalMinutes.HasValue)
            _state.State.PollingIntervalMinutes = command.PollingIntervalMinutes.Value;
        if (command.AutoProcessEnabled.HasValue)
            _state.State.AutoProcessEnabled = command.AutoProcessEnabled.Value;
        if (command.AutoProcessConfidenceThreshold.HasValue)
            _state.State.AutoProcessConfidenceThreshold = command.AutoProcessConfidenceThreshold.Value;
        if (command.SlackWebhookUrl != null)
            _state.State.SlackWebhookUrl = command.SlackWebhookUrl;
        if (command.SlackNotifyOnNewItem.HasValue)
            _state.State.SlackNotifyOnNewItem = command.SlackNotifyOnNewItem.Value;

        _state.State.Version++;
        await _state.WriteStateAsync();

        // Re-register reminder if interval changed and agent is active
        if (command.PollingIntervalMinutes.HasValue && _state.State.IsActive)
        {
            await RegisterPollReminderAsync();
        }

        return ToSnapshot();
    }

    public async Task<IngestionAgentSnapshot> SetRoutingRulesAsync(SetRoutingRulesCommand command)
    {
        EnsureExists();

        _state.State.RoutingRules = command.Rules;
        _state.State.Version++;
        await _state.WriteStateAsync();

        return ToSnapshot();
    }

    public async Task<IngestionAgentSnapshot> ActivateAsync()
    {
        EnsureExists();

        _state.State.IsActive = true;
        _state.State.Version++;
        await _state.WriteStateAsync();

        await RegisterPollReminderAsync();

        _logger.LogInformation("Ingestion agent activated for site {SiteId}", _state.State.SiteId);

        return ToSnapshot();
    }

    public async Task<IngestionAgentSnapshot> DeactivateAsync()
    {
        EnsureExists();

        _state.State.IsActive = false;
        _state.State.Version++;
        await _state.WriteStateAsync();

        await UnregisterPollReminderAsync();

        _logger.LogInformation("Ingestion agent deactivated for site {SiteId}", _state.State.SiteId);

        return ToSnapshot();
    }

    public async Task<PollResultSnapshot> TriggerPollAsync(Guid? mailboxConfigId = null)
    {
        EnsureExists();

        var mailboxes = mailboxConfigId.HasValue
            ? _state.State.Mailboxes.Where(m => m.ConfigId == mailboxConfigId.Value).ToList()
            : _state.State.Mailboxes.Where(m => m.IsEnabled).ToList();

        if (mailboxes.Count == 0)
        {
            return new PollResultSnapshot(0, 0, 0, 0, 0);
        }

        return await PollMailboxesAsync(mailboxes);
    }

    public async Task<IReadOnlyList<IngestionQueueItem>> GetQueueAsync()
    {
        EnsureExists();

        var items = new List<IngestionQueueItem>();

        foreach (var planId in _state.State.PendingPlanIds)
        {
            try
            {
                var planGrain = _grainFactory.GetGrain<IDocumentProcessingPlanGrain>(
                    GrainKeys.DocumentProcessingPlan(
                        _state.State.OrganizationId,
                        _state.State.SiteId,
                        planId));

                var snapshot = await planGrain.GetSnapshotAsync();
                if (snapshot.Status == ProcessingPlanStatus.Proposed)
                {
                    items.Add(new IngestionQueueItem(
                        snapshot.PlanId,
                        snapshot.EmailFrom,
                        snapshot.EmailSubject,
                        snapshot.EmailReceivedAt,
                        snapshot.AttachmentCount,
                        snapshot.SuggestedDocumentType,
                        snapshot.SuggestedVendorName,
                        snapshot.TypeConfidence,
                        snapshot.VendorConfidence,
                        snapshot.SuggestedAction,
                        snapshot.Reasoning,
                        snapshot.ProposedAt));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load plan {PlanId}", planId);
            }
        }

        return items;
    }

    public Task<IReadOnlyList<IngestionHistoryEntry>> GetHistoryAsync(int limit = 50)
    {
        EnsureExists();

        var history = _state.State.RecentHistory
            .OrderByDescending(h => h.ProcessedAt)
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<IngestionHistoryEntry>>(history);
    }

    public Task<IngestionAgentSnapshot> GetSnapshotAsync()
    {
        return Task.FromResult(ToSnapshot());
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.SiteId != Guid.Empty);
    }

    // ============================================================================
    // IRemindable — Periodic mailbox polling
    // ============================================================================

    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        if (reminderName != PollReminderName) return;
        if (!_state.State.IsActive) return;

        _logger.LogDebug("Poll reminder fired for site {SiteId}", _state.State.SiteId);

        var enabledMailboxes = _state.State.Mailboxes.Where(m => m.IsEnabled).ToList();
        if (enabledMailboxes.Count == 0) return;

        await PollMailboxesAsync(enabledMailboxes);
    }

    // ============================================================================
    // Private methods
    // ============================================================================

    private async Task<PollResultSnapshot> PollMailboxesAsync(List<MailboxConfig> mailboxes)
    {
        var totalFetched = 0;
        var totalPending = 0;
        var totalAutoProcessed = 0;
        var totalDuplicates = 0;
        var totalErrors = 0;

        foreach (var mailbox in mailboxes)
        {
            try
            {
                var result = await _mailboxPolling.PollAsync(
                    mailbox.ToConnectionConfig(),
                    mailbox.LastPollAt);

                if (!result.Success)
                {
                    _logger.LogWarning(
                        "Failed to poll mailbox {DisplayName}: {Error}",
                        mailbox.DisplayName, result.Error);
                    totalErrors++;
                    continue;
                }

                totalFetched += result.Messages.Count;

                foreach (var email in result.Messages)
                {
                    var outcome = await ProcessFetchedEmailAsync(email, mailbox);
                    switch (outcome)
                    {
                        case IngestionOutcome.PendingReview:
                            totalPending++;
                            break;
                        case IngestionOutcome.AutoProcessed:
                            totalAutoProcessed++;
                            break;
                        case IngestionOutcome.Duplicate:
                            totalDuplicates++;
                            break;
                        case IngestionOutcome.Failed:
                            totalErrors++;
                            break;
                    }

                    // Mark as processed in mailbox
                    await _mailboxPolling.MarkAsProcessedAsync(
                        mailbox.ToConnectionConfig(), email.MessageId);
                }

                // Update mailbox last poll time
                var index = _state.State.Mailboxes.FindIndex(m => m.ConfigId == mailbox.ConfigId);
                if (index >= 0)
                {
                    _state.State.Mailboxes[index] = mailbox with
                    {
                        LastPollAt = DateTime.UtcNow,
                        LastSeenMessageId = result.Messages.LastOrDefault()?.MessageId ?? mailbox.LastSeenMessageId
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling mailbox {DisplayName}", mailbox.DisplayName);
                totalErrors++;
            }
        }

        // Update agent stats
        _state.State.TotalPolls++;
        _state.State.TotalEmailsFetched += totalFetched;
        _state.State.LastPollAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();

        return new PollResultSnapshot(
            totalFetched, totalPending, totalAutoProcessed, totalDuplicates, totalErrors);
    }

    private async Task<IngestionOutcome> ProcessFetchedEmailAsync(
        ParsedEmail email, MailboxConfig mailbox)
    {
        try
        {
            // Check for duplicates via EmailInboxGrain
            var inboxGrain = _grainFactory.GetGrain<IEmailInboxGrain>(
                GrainKeys.EmailInbox(_state.State.OrganizationId, _state.State.SiteId));

            if (await inboxGrain.IsMessageProcessedAsync(email.MessageId))
            {
                AddHistoryEntry(email, IngestionOutcome.Duplicate, null);
                return IngestionOutcome.Duplicate;
            }

            // Evaluate routing rules
            var (suggestedType, suggestedVendorId, suggestedVendorName, confidence, matchedRule, reasoning)
                = EvaluateRoutingRules(email, mailbox);

            // Route to EmailInboxGrain for document creation
            var processingResult = await inboxGrain.ProcessEmailAsync(
                new ProcessIncomingEmailCommand(email, suggestedType));

            if (!processingResult.Accepted)
            {
                AddHistoryEntry(email, IngestionOutcome.Rejected, null,
                    error: processingResult.RejectionDetails);
                return IngestionOutcome.Rejected;
            }

            // Create processing plan
            var planId = Guid.NewGuid();
            var suggestedAction = ShouldAutoProcess(confidence, matchedRule)
                ? SuggestedAction.AutoProcess
                : SuggestedAction.ManualReview;

            var planGrain = _grainFactory.GetGrain<IDocumentProcessingPlanGrain>(
                GrainKeys.DocumentProcessingPlan(
                    _state.State.OrganizationId,
                    _state.State.SiteId,
                    planId));

            await planGrain.ProposeAsync(new ProposePlanCommand(
                planId,
                _state.State.OrganizationId,
                _state.State.SiteId,
                email.MessageId,
                email.From,
                email.Subject,
                email.ReceivedAt,
                email.Attachments.Count,
                suggestedType,
                suggestedVendorId,
                suggestedVendorName,
                confidence,
                suggestedVendorId.HasValue ? 0.8m : 0m,
                suggestedAction,
                matchedRule?.RuleId,
                reasoning,
                processingResult.DocumentIds ?? []));

            // Auto-process if conditions met
            if (suggestedAction == SuggestedAction.AutoProcess)
            {
                await planGrain.ApproveAsync(Guid.Empty); // System-approved
                _state.State.TotalAutoProcessed++;
                AddHistoryEntry(email, IngestionOutcome.AutoProcessed, planId,
                    documentIds: processingResult.DocumentIds);
                return IngestionOutcome.AutoProcessed;
            }

            // Add to pending queue for user review
            _state.State.PendingPlanIds.Add(planId);
            _state.State.TotalPendingReview++;
            AddHistoryEntry(email, IngestionOutcome.PendingReview, planId,
                documentIds: processingResult.DocumentIds);

            return IngestionOutcome.PendingReview;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process email {MessageId}", email.MessageId);
            AddHistoryEntry(email, IngestionOutcome.Failed, null, error: ex.Message);
            return IngestionOutcome.Failed;
        }
    }

    private (PurchaseDocumentType type, Guid? vendorId, string? vendorName, decimal confidence,
        RoutingRule? matchedRule, string reasoning)
        EvaluateRoutingRules(ParsedEmail email, MailboxConfig mailbox)
    {
        // Evaluate rules by priority (lower number = higher priority)
        foreach (var rule in _state.State.RoutingRules.OrderBy(r => r.Priority))
        {
            var matches = rule.Type switch
            {
                RoutingRuleType.SenderDomain => MatchesSenderDomain(email.From, rule.Pattern),
                RoutingRuleType.SenderEmail => email.From.Equals(rule.Pattern, StringComparison.OrdinalIgnoreCase),
                RoutingRuleType.SubjectPattern => Regex.IsMatch(email.Subject, rule.Pattern, RegexOptions.IgnoreCase),
                _ => false
            };

            if (matches)
            {
                return (
                    rule.SuggestedDocumentType ?? mailbox.DefaultDocumentType,
                    rule.SuggestedVendorId,
                    rule.SuggestedVendorName,
                    0.9m,
                    rule,
                    $"Matched rule '{rule.Name}' ({rule.Type}: {rule.Pattern})");
            }
        }

        // Default heuristics based on email content
        var detectedType = DetectDocumentType(email);
        var confidence = 0.5m;
        var reasoning = "No routing rule matched, using heuristic detection";

        return (detectedType, null, null, confidence, null, reasoning);
    }

    private static bool MatchesSenderDomain(string email, string domainPattern)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex < 0) return false;
        var domain = email[(atIndex + 1)..];
        return domain.Equals(domainPattern, StringComparison.OrdinalIgnoreCase);
    }

    private static PurchaseDocumentType DetectDocumentType(ParsedEmail email)
    {
        var subjectLower = email.Subject.ToLowerInvariant();

        if (subjectLower.Contains("receipt") || subjectLower.Contains("purchase confirmation"))
            return PurchaseDocumentType.Receipt;
        if (subjectLower.Contains("credit note") || subjectLower.Contains("credit memo"))
            return PurchaseDocumentType.CreditNote;
        if (subjectLower.Contains("purchase order") || subjectLower.Contains("p.o."))
            return PurchaseDocumentType.PurchaseOrder;

        return PurchaseDocumentType.Invoice;
    }

    private bool ShouldAutoProcess(decimal confidence, RoutingRule? matchedRule)
    {
        if (!_state.State.AutoProcessEnabled) return false;
        if (matchedRule?.AutoApprove == true) return true;
        return confidence >= _state.State.AutoProcessConfidenceThreshold;
    }

    private void AddHistoryEntry(
        ParsedEmail email,
        IngestionOutcome outcome,
        Guid? planId,
        IReadOnlyList<Guid>? documentIds = null,
        string? error = null)
    {
        _state.State.RecentHistory.Add(new IngestionHistoryEntry
        {
            EntryId = Guid.NewGuid(),
            EmailMessageId = email.MessageId,
            From = email.From,
            Subject = email.Subject,
            ReceivedAt = email.ReceivedAt,
            ProcessedAt = DateTime.UtcNow,
            Outcome = outcome,
            PlanId = planId,
            DocumentIds = documentIds,
            Error = error
        });

        // Cap history
        while (_state.State.RecentHistory.Count > InvoiceIngestionAgentState.MaxHistoryEntries)
        {
            _state.State.RecentHistory.RemoveAt(0);
        }
    }

    private async Task RegisterPollReminderAsync()
    {
        var interval = TimeSpan.FromMinutes(_state.State.PollingIntervalMinutes);
        await this.RegisterOrUpdateReminder(PollReminderName, interval, interval);
    }

    private async Task UnregisterPollReminderAsync()
    {
        var reminder = await this.GetReminder(PollReminderName);
        if (reminder != null)
        {
            await this.UnregisterReminder(reminder);
        }
    }

    private void EnsureExists()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Agent not configured");
    }

    private IngestionAgentSnapshot ToSnapshot() => new(
        _state.State.OrganizationId,
        _state.State.SiteId,
        _state.State.IsActive,
        _state.State.Mailboxes.Count,
        _state.State.PollingIntervalMinutes,
        _state.State.AutoProcessEnabled,
        _state.State.AutoProcessConfidenceThreshold,
        _state.State.PendingPlanIds.Count,
        _state.State.LastPollAt,
        _state.State.TotalPolls,
        _state.State.TotalEmailsFetched,
        _state.State.TotalDocumentsCreated,
        _state.State.TotalAutoProcessed,
        _state.State.Mailboxes.Select(m => new MailboxConfigSnapshot(
            m.ConfigId,
            m.DisplayName,
            m.Host,
            m.Port,
            m.Username,
            m.UseSsl,
            m.FolderName,
            m.IsEnabled,
            m.DefaultDocumentType,
            m.LastPollAt)).ToList(),
        _state.State.RoutingRules);
}
