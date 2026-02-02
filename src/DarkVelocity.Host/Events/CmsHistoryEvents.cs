namespace DarkVelocity.Host.Events;

// ============================================================================
// CMS History Events - Field-Level Change Tracking
// ============================================================================

/// <summary>
/// Represents the type of change operation performed on a field.
/// </summary>
public enum ChangeOperation
{
    /// <summary>A field value was set or updated.</summary>
    Set,
    /// <summary>An item was added to a collection.</summary>
    Add,
    /// <summary>An item was removed from a collection.</summary>
    Remove,
    /// <summary>Items in a collection were reordered.</summary>
    Reorder
}

/// <summary>
/// Represents a single field-level change in a CMS document.
/// </summary>
/// <param name="FieldPath">The dot-notation path to the field (e.g., "Pricing.BasePrice", "Content.Translations.en-US.Name").</param>
/// <param name="OldValue">The previous value (JSON-serialized), null for additions.</param>
/// <param name="NewValue">The new value (JSON-serialized), null for removals.</param>
/// <param name="Op">The type of change operation.</param>
[GenerateSerializer]
public sealed record FieldChange(
    [property: Id(0)] string FieldPath,
    [property: Id(1)] string? OldValue,
    [property: Id(2)] string? NewValue,
    [property: Id(3)] ChangeOperation Op)
{
    /// <summary>
    /// Creates a Set change for a field value update.
    /// </summary>
    public static FieldChange Set(string fieldPath, string? oldValue, string? newValue)
        => new(fieldPath, oldValue, newValue, ChangeOperation.Set);

    /// <summary>
    /// Creates an Add change for a collection addition.
    /// </summary>
    public static FieldChange Add(string fieldPath, string? newValue)
        => new(fieldPath, null, newValue, ChangeOperation.Add);

    /// <summary>
    /// Creates a Remove change for a collection removal.
    /// </summary>
    public static FieldChange Remove(string fieldPath, string? oldValue)
        => new(fieldPath, oldValue, null, ChangeOperation.Remove);

    /// <summary>
    /// Creates a Reorder change for collection reordering.
    /// </summary>
    public static FieldChange Reorder(string fieldPath, string? oldOrder, string? newOrder)
        => new(fieldPath, oldOrder, newOrder, ChangeOperation.Reorder);

    /// <summary>
    /// Creates the inverse of this change for undo operations.
    /// </summary>
    public FieldChange Inverse() => Op switch
    {
        ChangeOperation.Set => new FieldChange(FieldPath, NewValue, OldValue, ChangeOperation.Set),
        ChangeOperation.Add => new FieldChange(FieldPath, NewValue, null, ChangeOperation.Remove),
        ChangeOperation.Remove => new FieldChange(FieldPath, null, OldValue, ChangeOperation.Add),
        ChangeOperation.Reorder => new FieldChange(FieldPath, NewValue, OldValue, ChangeOperation.Reorder),
        _ => throw new InvalidOperationException($"Unknown change operation: {Op}")
    };
}

/// <summary>
/// The type of CMS content change.
/// </summary>
public enum CmsChangeType
{
    /// <summary>Document was created.</summary>
    Created,
    /// <summary>A draft version was created.</summary>
    DraftCreated,
    /// <summary>Draft was published to become live.</summary>
    Published,
    /// <summary>Document was reverted to a previous version.</summary>
    Reverted,
    /// <summary>A translation was added or updated.</summary>
    TranslationUpdated,
    /// <summary>A translation was removed.</summary>
    TranslationRemoved,
    /// <summary>Document was archived.</summary>
    Archived,
    /// <summary>Document was restored from archive.</summary>
    Restored,
    /// <summary>Draft was discarded.</summary>
    DraftDiscarded,
    /// <summary>Items in a collection were reordered.</summary>
    ItemsReordered,
    /// <summary>An item was added to a collection.</summary>
    ItemAdded,
    /// <summary>An item was removed from a collection.</summary>
    ItemRemoved
}

/// <summary>
/// Represents a content change event with field-level diff information.
/// This is the primary event stored in the history grain.
/// </summary>
/// <param name="DocumentType">The type of document (e.g., "MenuItem", "MenuCategory", "ModifierBlock").</param>
/// <param name="DocumentId">The unique identifier of the document.</param>
/// <param name="OrgId">The organization ID.</param>
/// <param name="FromVersion">The version before the change (0 for creation).</param>
/// <param name="ToVersion">The version after the change.</param>
/// <param name="ChangedBy">The user who made the change.</param>
/// <param name="OccurredAt">When the change occurred.</param>
/// <param name="ChangeType">The type of change.</param>
/// <param name="Changes">The list of field-level changes.</param>
/// <param name="ChangeNote">Optional note describing the change.</param>
[GenerateSerializer]
public sealed record CmsContentChanged(
    [property: Id(0)] string DocumentType,
    [property: Id(1)] string DocumentId,
    [property: Id(2)] Guid OrgId,
    [property: Id(3)] int FromVersion,
    [property: Id(4)] int ToVersion,
    [property: Id(5)] Guid? ChangedBy,
    [property: Id(6)] DateTimeOffset OccurredAt,
    [property: Id(7)] CmsChangeType ChangeType,
    [property: Id(8)] IReadOnlyList<FieldChange> Changes,
    [property: Id(9)] string? ChangeNote) : IntegrationEvent
{
    public override string EventType => $"cms.{DocumentType.ToLowerInvariant()}.changed";

    /// <summary>
    /// Creates a unique identifier for this change event.
    /// </summary>
    public string ChangeId => $"{DocumentId}:{FromVersion}:{ToVersion}:{OccurredAt.Ticks}";
}

/// <summary>
/// Represents a diff between two versions of a document.
/// </summary>
/// <param name="FromVersion">The starting version.</param>
/// <param name="ToVersion">The ending version.</param>
/// <param name="Changes">All field changes between the versions.</param>
/// <param name="ChangeEvents">The individual change events that make up this diff.</param>
[GenerateSerializer]
public sealed record ContentDiff(
    [property: Id(0)] int FromVersion,
    [property: Id(1)] int ToVersion,
    [property: Id(2)] IReadOnlyList<FieldChange> Changes,
    [property: Id(3)] IReadOnlyList<CmsContentChanged> ChangeEvents);

/// <summary>
/// Result of an undo or redo operation.
/// </summary>
/// <param name="Success">Whether the operation succeeded.</param>
/// <param name="NewVersion">The new version number after the operation.</param>
/// <param name="ChangesApplied">The changes that were applied.</param>
/// <param name="ErrorMessage">Error message if the operation failed.</param>
[GenerateSerializer]
public sealed record UndoResult(
    [property: Id(0)] bool Success,
    [property: Id(1)] int? NewVersion,
    [property: Id(2)] IReadOnlyList<FieldChange> ChangesApplied,
    [property: Id(3)] string? ErrorMessage)
{
    /// <summary>
    /// Creates a successful undo result.
    /// </summary>
    public static UndoResult Succeeded(int newVersion, IReadOnlyList<FieldChange> changes)
        => new(true, newVersion, changes, null);

    /// <summary>
    /// Creates a failed undo result.
    /// </summary>
    public static UndoResult Failed(string message)
        => new(false, null, [], message);
}
