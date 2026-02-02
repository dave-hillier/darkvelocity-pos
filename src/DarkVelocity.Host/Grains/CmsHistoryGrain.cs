using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// CMS History Grain Implementation
// ============================================================================

/// <summary>
/// Companion grain for tracking the history of changes to a CMS document.
/// Key pattern: "{orgId}:{docType}:{docId}:history"
/// </summary>
public class CmsHistoryGrain : Grain, ICmsHistoryGrain
{
    private readonly IPersistentState<CmsHistoryState> _state;

    public CmsHistoryGrain(
        [PersistentState("cmsHistory", "OrleansStorage")]
        IPersistentState<CmsHistoryState> state)
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
        // Key format: "{orgId}:{docType}:{docId}:history"
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

    public async Task RecordChangeAsync(CmsContentChanged change)
    {
        EnsureInitialized();

        _state.State.ChangeEvents.Add(change);
        _state.State.CurrentVersion = Math.Max(_state.State.CurrentVersion, change.ToVersion);

        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<CmsContentChanged>> GetHistoryAsync(int skip = 0, int take = 50)
    {
        EnsureInitialized();

        var result = _state.State.ChangeEvents
            .OrderByDescending(e => e.OccurredAt)
            .Skip(skip)
            .Take(take)
            .ToList();

        return Task.FromResult<IReadOnlyList<CmsContentChanged>>(result);
    }

    public Task<IReadOnlyList<HistoryEntrySummary>> GetHistorySummaryAsync(int skip = 0, int take = 50)
    {
        EnsureInitialized();

        var result = _state.State.ChangeEvents
            .OrderByDescending(e => e.OccurredAt)
            .Skip(skip)
            .Take(take)
            .Select(e => new HistoryEntrySummary(
                ChangeId: e.ChangeId,
                OccurredAt: e.OccurredAt,
                ChangedBy: e.ChangedBy,
                ChangeType: e.ChangeType,
                FromVersion: e.FromVersion,
                ToVersion: e.ToVersion,
                ChangeNote: e.ChangeNote,
                FieldChangeCount: e.Changes.Count))
            .ToList();

        return Task.FromResult<IReadOnlyList<HistoryEntrySummary>>(result);
    }

    public Task<CmsContentChanged?> GetChangeAsync(string changeId)
    {
        EnsureInitialized();

        var change = _state.State.ChangeEvents.FirstOrDefault(e => e.ChangeId == changeId);
        return Task.FromResult(change);
    }

    public Task<ContentDiff> GetDiffAsync(int fromVersion, int toVersion)
    {
        EnsureInitialized();

        var diff = _state.State.ComputeDiff(fromVersion, toVersion);
        return Task.FromResult(diff);
    }

    public Task<IReadOnlyList<CmsContentChanged>> GetChangesForVersionAsync(int version)
    {
        EnsureInitialized();

        var changes = _state.State.GetChangesForVersion(version);
        return Task.FromResult(changes);
    }

    public Task<int> GetTotalChangesAsync()
    {
        return Task.FromResult(_state.State.TotalChanges);
    }

    public Task<CmsContentChanged?> GetLastChangeAsync()
    {
        return Task.FromResult(_state.State.LastChange);
    }

    public async Task ClearHistoryAsync()
    {
        _state.State.ChangeEvents.Clear();
        _state.State.CurrentVersion = 0;
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
