using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// CMS Undo Grain Implementation
// ============================================================================

/// <summary>
/// Companion grain for managing undo/redo operations on a CMS document.
/// Key pattern: "{orgId}:{docType}:{docId}:undo"
/// </summary>
public class CmsUndoGrain : Grain, ICmsUndoGrain
{
    private readonly IPersistentState<CmsUndoState> _state;

    public CmsUndoGrain(
        [PersistentState("cmsUndo", "OrleansStorage")]
        IPersistentState<CmsUndoState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (!_state.State.IsInitialized)
        {
            ParseKeyAndInitialize();
        }
        return base.OnActivateAsync(cancellationToken);
    }

    private void ParseKeyAndInitialize()
    {
        // Key format: "{orgId}:{docType}:{docId}:undo"
        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        if (parts.Length >= 4)
        {
            _state.State.OrgId = Guid.Parse(parts[0]);
            _state.State.DocumentType = parts[1];
            _state.State.DocumentId = parts[2];
            _state.State.IsInitialized = true;
        }
    }

    public async Task PushAsync(CmsContentChanged change)
    {
        EnsureInitialized();

        var operation = new UndoableOperation
        {
            PerformedAt = change.OccurredAt,
            PerformedBy = change.ChangedBy,
            ChangeType = change.ChangeType,
            FromVersion = change.FromVersion,
            ToVersion = change.ToVersion,
            Changes = change.Changes.ToList(),
            Description = change.ChangeNote ?? $"{change.ChangeType} - Version {change.ToVersion}",
            CrossedPublishBoundary = false
        };

        _state.State.Push(operation);

        // Track draft state
        if (change.ChangeType == CmsChangeType.DraftCreated)
        {
            _state.State.HasDraft = true;
        }

        await _state.WriteStateAsync();
    }

    public async Task<UndoResult> UndoAsync(int count = 1, Guid? userId = null)
    {
        EnsureInitialized();

        if (count < 1)
            return UndoResult.Failed("Count must be at least 1");

        if (_state.State.UndoCount < count)
            return UndoResult.Failed($"Only {_state.State.UndoCount} operations available to undo");

        var allChanges = new List<FieldChange>();
        var operations = new List<UndoableOperation>();

        for (int i = 0; i < count; i++)
        {
            var operation = _state.State.PopUndo();
            if (operation == null)
                break;

            operations.Add(operation);

            // Add inverse changes
            foreach (var change in operation.Changes)
            {
                allChanges.Add(change.Inverse());
            }
        }

        if (operations.Count == 0)
            return UndoResult.Failed("No operations to undo");

        // Determine the target version
        var targetVersion = operations[^1].FromVersion;

        // Check if we're crossing a publish boundary
        var crossesPublish = operations.Any(o => o.CrossedPublishBoundary);

        await _state.WriteStateAsync();

        return UndoResult.Succeeded(targetVersion, allChanges);
    }

    public async Task<UndoResult> RedoAsync(int count = 1, Guid? userId = null)
    {
        EnsureInitialized();

        if (count < 1)
            return UndoResult.Failed("Count must be at least 1");

        if (_state.State.RedoCount < count)
            return UndoResult.Failed($"Only {_state.State.RedoCount} operations available to redo");

        var allChanges = new List<FieldChange>();
        var operations = new List<UndoableOperation>();

        for (int i = 0; i < count; i++)
        {
            var operation = _state.State.PopRedo();
            if (operation == null)
                break;

            operations.Add(operation);

            // Add the original changes (not inverse)
            allChanges.AddRange(operation.Changes);
        }

        if (operations.Count == 0)
            return UndoResult.Failed("No operations to redo");

        // Determine the target version
        var targetVersion = operations[^1].ToVersion;

        await _state.WriteStateAsync();

        return UndoResult.Succeeded(targetVersion, allChanges);
    }

    public Task<IReadOnlyList<FieldChange>> PreviewUndoAsync(int count = 1)
    {
        EnsureInitialized();

        var operations = _state.State.PeekUndo(count);
        var changes = operations
            .SelectMany(o => o.Changes.Select(c => c.Inverse()))
            .ToList();

        return Task.FromResult<IReadOnlyList<FieldChange>>(changes);
    }

    public Task<IReadOnlyList<FieldChange>> PreviewRedoAsync(int count = 1)
    {
        EnsureInitialized();

        var operations = _state.State.PeekRedo(count);
        var changes = operations
            .SelectMany(o => o.Changes)
            .ToList();

        return Task.FromResult<IReadOnlyList<FieldChange>>(changes);
    }

    public Task<UndoStackSummary> GetStackSummaryAsync()
    {
        EnsureInitialized();

        return Task.FromResult(new UndoStackSummary(
            UndoCount: _state.State.UndoCount,
            RedoCount: _state.State.RedoCount,
            HasDraft: _state.State.HasDraft,
            LastPublishedVersion: _state.State.LastPublishedVersion));
    }

    public Task<IReadOnlyList<UndoableOperation>> GetUndoStackAsync(int count = 10)
    {
        EnsureInitialized();

        var operations = _state.State.UndoStack
            .Reverse()
            .Take(count)
            .ToList();

        return Task.FromResult<IReadOnlyList<UndoableOperation>>(operations);
    }

    public Task<IReadOnlyList<UndoableOperation>> GetRedoStackAsync(int count = 10)
    {
        EnsureInitialized();

        var operations = _state.State.RedoStack
            .Reverse()
            .Take(count)
            .ToList();

        return Task.FromResult<IReadOnlyList<UndoableOperation>>(operations);
    }

    public async Task MarkPublishedAsync(int publishedVersion)
    {
        EnsureInitialized();
        _state.State.MarkPublished(publishedVersion);
        await _state.WriteStateAsync();
    }

    public async Task MarkDraftCreatedAsync()
    {
        EnsureInitialized();
        _state.State.MarkDraftCreated();
        await _state.WriteStateAsync();
    }

    public async Task ClearAsync()
    {
        _state.State.Clear();
        await _state.WriteStateAsync();
    }

    private void EnsureInitialized()
    {
        if (!_state.State.IsInitialized)
        {
            ParseKeyAndInitialize();
        }
    }
}
