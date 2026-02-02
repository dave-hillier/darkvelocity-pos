using DarkVelocity.Host.Events;

namespace DarkVelocity.Host.State;

// ============================================================================
// CMS History State - Stores field-level change history for CMS documents
// ============================================================================

/// <summary>
/// State for tracking the history of changes to a CMS document.
/// Key pattern: "{orgId}:{docType}:{docId}:history"
/// </summary>
/// <remarks>
/// This state is used by companion grains that track history alongside the main
/// document grains (MenuItemDocument, MenuCategoryDocument, RecipeDocument, etc.).
/// The history provides:
/// - Full audit trail with field-level diffs
/// - Queryable change history
/// - Support for diff between any two versions
/// </remarks>
[GenerateSerializer]
public sealed class CmsHistoryState
{
    /// <summary>The organization ID.</summary>
    [Id(0)] public Guid OrgId { get; set; }

    /// <summary>The document type (e.g., "MenuItem", "MenuCategory", "Recipe", "RecipeCategory").</summary>
    [Id(1)] public string DocumentType { get; set; } = string.Empty;

    /// <summary>The document ID.</summary>
    [Id(2)] public string DocumentId { get; set; } = string.Empty;

    /// <summary>Whether this history grain has been initialized.</summary>
    [Id(3)] public bool IsInitialized { get; set; }

    /// <summary>The list of change events in chronological order.</summary>
    [Id(4)] public List<CmsContentChanged> ChangeEvents { get; set; } = [];

    /// <summary>The current version number of the document.</summary>
    [Id(5)] public int CurrentVersion { get; set; }

    /// <summary>Total number of changes recorded.</summary>
    public int TotalChanges => ChangeEvents.Count;

    /// <summary>
    /// Gets the most recent change event, if any.
    /// </summary>
    public CmsContentChanged? LastChange => ChangeEvents.Count > 0 ? ChangeEvents[^1] : null;

    /// <summary>
    /// Gets changes in a specific version range.
    /// </summary>
    public IReadOnlyList<CmsContentChanged> GetChangesInRange(int fromVersion, int toVersion)
    {
        return ChangeEvents
            .Where(e => e.FromVersion >= fromVersion && e.ToVersion <= toVersion)
            .OrderBy(e => e.OccurredAt)
            .ToList();
    }

    /// <summary>
    /// Gets all changes affecting a specific version.
    /// </summary>
    public IReadOnlyList<CmsContentChanged> GetChangesForVersion(int version)
    {
        return ChangeEvents
            .Where(e => e.ToVersion == version)
            .OrderBy(e => e.OccurredAt)
            .ToList();
    }

    /// <summary>
    /// Computes the aggregate diff between two versions.
    /// </summary>
    public ContentDiff ComputeDiff(int fromVersion, int toVersion)
    {
        var relevantEvents = ChangeEvents
            .Where(e => e.FromVersion >= fromVersion && e.ToVersion <= toVersion)
            .OrderBy(e => e.OccurredAt)
            .ToList();

        // Aggregate all field changes, with later changes overwriting earlier ones
        var aggregatedChanges = new Dictionary<string, FieldChange>();
        foreach (var evt in relevantEvents)
        {
            foreach (var change in evt.Changes)
            {
                if (aggregatedChanges.TryGetValue(change.FieldPath, out var existing))
                {
                    // Merge: keep original OldValue, use new NewValue
                    aggregatedChanges[change.FieldPath] = change with { OldValue = existing.OldValue };
                }
                else
                {
                    aggregatedChanges[change.FieldPath] = change;
                }
            }
        }

        // Remove changes where old and new values are the same (net zero change)
        var netChanges = aggregatedChanges.Values
            .Where(c => c.OldValue != c.NewValue)
            .ToList();

        return new ContentDiff(fromVersion, toVersion, netChanges, relevantEvents);
    }
}
