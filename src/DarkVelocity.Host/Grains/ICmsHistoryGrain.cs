using DarkVelocity.Host.Events;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// CMS History Grain Interface - Field-Level Change Tracking
// ============================================================================

/// <summary>
/// Summary of a change event for list views.
/// </summary>
[GenerateSerializer]
public record HistoryEntrySummary(
    [property: Id(0)] string ChangeId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] Guid? ChangedBy,
    [property: Id(3)] CmsChangeType ChangeType,
    [property: Id(4)] int FromVersion,
    [property: Id(5)] int ToVersion,
    [property: Id(6)] string? ChangeNote,
    [property: Id(7)] int FieldChangeCount);

/// <summary>
/// Companion grain for tracking the history of changes to a CMS document.
/// Key pattern: "{orgId}:{docType}:{docId}:history"
/// </summary>
/// <remarks>
/// This grain provides rich history tracking for CMS documents including:
/// - Field-level change tracking (what changed between versions)
/// - Full audit trail with timestamps and user attribution
/// - Diff computation between any two versions
/// - Paginated history queries
///
/// Supports all CMS document types:
/// - MenuItem, MenuCategory, ModifierBlock
/// - Recipe, RecipeCategory
///
/// Example usage:
/// <code>
/// var historyGrain = grainFactory.GetGrain&lt;ICmsHistoryGrain&gt;(
///     GrainKeys.CmsHistory(orgId, "MenuItem", documentId));
/// var history = await historyGrain.GetHistoryAsync(skip: 0, take: 20);
/// var diff = await historyGrain.GetDiffAsync(fromVersion: 1, toVersion: 3);
/// </code>
/// </remarks>
public interface ICmsHistoryGrain : IGrainWithStringKey
{
    /// <summary>
    /// Records a change event in the history.
    /// Called by the main document grain when changes occur.
    /// </summary>
    /// <param name="change">The change event to record.</param>
    Task RecordChangeAsync(CmsContentChanged change);

    /// <summary>
    /// Gets the complete history of changes, paginated.
    /// </summary>
    /// <param name="skip">Number of entries to skip.</param>
    /// <param name="take">Maximum number of entries to return.</param>
    /// <returns>List of change events in reverse chronological order (newest first).</returns>
    Task<IReadOnlyList<CmsContentChanged>> GetHistoryAsync(int skip = 0, int take = 50);

    /// <summary>
    /// Gets a summary of the history for list views (lighter weight than full history).
    /// </summary>
    /// <param name="skip">Number of entries to skip.</param>
    /// <param name="take">Maximum number of entries to return.</param>
    /// <returns>List of history summaries in reverse chronological order.</returns>
    Task<IReadOnlyList<HistoryEntrySummary>> GetHistorySummaryAsync(int skip = 0, int take = 50);

    /// <summary>
    /// Gets a specific change event by its ID.
    /// </summary>
    /// <param name="changeId">The change ID.</param>
    /// <returns>The change event, or null if not found.</returns>
    Task<CmsContentChanged?> GetChangeAsync(string changeId);

    /// <summary>
    /// Computes the diff between two versions.
    /// </summary>
    /// <param name="fromVersion">The starting version.</param>
    /// <param name="toVersion">The ending version.</param>
    /// <returns>A diff containing all field changes between the versions.</returns>
    Task<ContentDiff> GetDiffAsync(int fromVersion, int toVersion);

    /// <summary>
    /// Gets all changes that affected a specific version.
    /// </summary>
    /// <param name="version">The version number.</param>
    /// <returns>List of changes that created or modified this version.</returns>
    Task<IReadOnlyList<CmsContentChanged>> GetChangesForVersionAsync(int version);

    /// <summary>
    /// Gets the total number of recorded changes.
    /// </summary>
    Task<int> GetTotalChangesAsync();

    /// <summary>
    /// Gets the most recent change event.
    /// </summary>
    Task<CmsContentChanged?> GetLastChangeAsync();

    /// <summary>
    /// Clears all history (use with caution - mainly for testing).
    /// </summary>
    Task ClearHistoryAsync();
}
