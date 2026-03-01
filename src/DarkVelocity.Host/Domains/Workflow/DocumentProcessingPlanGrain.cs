using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Grain managing the lifecycle of a document processing plan.
/// Plans are proposed when emails are ingested, then approved/rejected by users.
/// </summary>
public class DocumentProcessingPlanGrain : Grain, IDocumentProcessingPlanGrain
{
    private readonly IPersistentState<DocumentProcessingPlanState> _state;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<DocumentProcessingPlanGrain> _logger;

    public DocumentProcessingPlanGrain(
        [PersistentState("doc-processing-plan", "OrleansStorage")]
        IPersistentState<DocumentProcessingPlanState> state,
        IGrainFactory grainFactory,
        ILogger<DocumentProcessingPlanGrain> logger)
    {
        _state = state;
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public async Task<PlanSnapshot> ProposeAsync(ProposePlanCommand command)
    {
        if (_state.State.PlanId != Guid.Empty)
            throw new InvalidOperationException("Plan already exists");

        _state.State = new DocumentProcessingPlanState
        {
            PlanId = command.PlanId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            Status = ProcessingPlanStatus.Proposed,
            EmailMessageId = command.EmailMessageId,
            EmailFrom = command.EmailFrom,
            EmailSubject = command.EmailSubject,
            EmailReceivedAt = command.EmailReceivedAt,
            AttachmentCount = command.AttachmentCount,
            SuggestedDocumentType = command.SuggestedDocumentType,
            SuggestedVendorId = command.SuggestedVendorId,
            SuggestedVendorName = command.SuggestedVendorName,
            TypeConfidence = command.TypeConfidence,
            VendorConfidence = command.VendorConfidence,
            SuggestedAction = command.SuggestedAction,
            MatchedRuleId = command.MatchedRuleId,
            Reasoning = command.Reasoning,
            DocumentIds = command.DocumentIds.ToList(),
            ProposedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Processing plan {PlanId} proposed for email from {From}: {Subject}",
            command.PlanId, command.EmailFrom, command.EmailSubject);

        return ToSnapshot();
    }

    public async Task<PlanSnapshot> ApproveAsync(Guid approvedBy)
    {
        EnsureExists();
        EnsureStatus(ProcessingPlanStatus.Proposed);

        _state.State.Status = ProcessingPlanStatus.Approved;
        _state.State.ReviewedBy = approvedBy;
        _state.State.ReviewedAt = DateTime.UtcNow;

        // Trigger document processing
        await ExecutePlanAsync();

        await _state.WriteStateAsync();

        _logger.LogInformation("Processing plan {PlanId} approved by {UserId}", _state.State.PlanId, approvedBy);

        return ToSnapshot();
    }

    public async Task<PlanSnapshot> ModifyAndApproveAsync(ModifyPlanCommand command)
    {
        EnsureExists();
        EnsureStatus(ProcessingPlanStatus.Proposed);

        _state.State.Status = ProcessingPlanStatus.Modified;
        _state.State.ReviewedBy = command.ModifiedBy;
        _state.State.ReviewedAt = DateTime.UtcNow;
        _state.State.OverrideDocumentType = command.DocumentType;
        _state.State.OverrideVendorId = command.VendorId;
        _state.State.OverrideVendorName = command.VendorName;

        // Trigger document processing with overrides
        await ExecutePlanAsync();

        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Processing plan {PlanId} modified and approved by {UserId}",
            _state.State.PlanId, command.ModifiedBy);

        return ToSnapshot();
    }

    public async Task<PlanSnapshot> RejectAsync(Guid rejectedBy, string? reason = null)
    {
        EnsureExists();
        EnsureStatus(ProcessingPlanStatus.Proposed);

        _state.State.Status = ProcessingPlanStatus.Rejected;
        _state.State.ReviewedBy = rejectedBy;
        _state.State.ReviewedAt = DateTime.UtcNow;
        _state.State.RejectionReason = reason;

        // Reject associated documents
        foreach (var documentId in _state.State.DocumentIds)
        {
            try
            {
                var docGrain = _grainFactory.GetGrain<IPurchaseDocumentGrain>(
                    GrainKeys.PurchaseDocument(
                        _state.State.OrganizationId,
                        _state.State.SiteId,
                        documentId));

                await docGrain.RejectAsync(new RejectPurchaseDocumentCommand(
                    rejectedBy,
                    reason ?? "Processing plan rejected"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to reject document {DocumentId}", documentId);
            }
        }

        _state.State.Version++;
        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Processing plan {PlanId} rejected by {UserId}: {Reason}",
            _state.State.PlanId, rejectedBy, reason);

        return ToSnapshot();
    }

    public Task<PlanSnapshot> GetSnapshotAsync()
    {
        return Task.FromResult(ToSnapshot());
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.PlanId != Guid.Empty);
    }

    private async Task ExecutePlanAsync()
    {
        try
        {
            _state.State.Status = ProcessingPlanStatus.Executing;

            foreach (var documentId in _state.State.DocumentIds)
            {
                var docGrain = _grainFactory.GetGrain<IPurchaseDocumentGrain>(
                    GrainKeys.PurchaseDocument(
                        _state.State.OrganizationId,
                        _state.State.SiteId,
                        documentId));

                // Trigger processing if not already processing
                try
                {
                    await docGrain.RequestProcessingAsync();
                }
                catch (InvalidOperationException)
                {
                    // Document may already be processing — that's fine
                }
            }

            _state.State.Status = ProcessingPlanStatus.Executed;
            _state.State.ExecutedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _state.State.Status = ProcessingPlanStatus.Failed;
            _state.State.ExecutionError = ex.Message;
            _logger.LogError(ex, "Failed to execute processing plan {PlanId}", _state.State.PlanId);
        }

        _state.State.Version++;
    }

    private void EnsureExists()
    {
        if (_state.State.PlanId == Guid.Empty)
            throw new InvalidOperationException("Plan not found");
    }

    private void EnsureStatus(ProcessingPlanStatus expected)
    {
        if (_state.State.Status != expected)
            throw new InvalidOperationException(
                $"Plan is in status {_state.State.Status}, expected {expected}");
    }

    private PlanSnapshot ToSnapshot() => new(
        _state.State.PlanId,
        _state.State.OrganizationId,
        _state.State.SiteId,
        _state.State.Status,
        _state.State.EmailMessageId,
        _state.State.EmailFrom,
        _state.State.EmailSubject,
        _state.State.EmailReceivedAt,
        _state.State.AttachmentCount,
        _state.State.SuggestedDocumentType,
        _state.State.SuggestedVendorId,
        _state.State.SuggestedVendorName,
        _state.State.TypeConfidence,
        _state.State.VendorConfidence,
        _state.State.SuggestedAction,
        _state.State.Reasoning,
        _state.State.OverrideDocumentType,
        _state.State.OverrideVendorName,
        _state.State.DocumentIds,
        _state.State.ExecutedAt,
        _state.State.ReviewedBy,
        _state.State.ReviewedAt,
        _state.State.RejectionReason,
        _state.State.ProposedAt);
}
