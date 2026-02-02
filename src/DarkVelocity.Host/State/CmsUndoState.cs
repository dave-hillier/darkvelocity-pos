using DarkVelocity.Host.Events;

namespace DarkVelocity.Host.State;

// ============================================================================
// CMS Undo State - Manages undo/redo stacks for CMS documents
// ============================================================================

/// <summary>
/// Represents an undoable operation with its change data.
/// </summary>
[GenerateSerializer]
public sealed class UndoableOperation
{
    /// <summary>Unique identifier for this operation.</summary>
    [Id(0)] public string OperationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>When the operation was performed.</summary>
    [Id(1)] public DateTimeOffset PerformedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>The user who performed the operation.</summary>
    [Id(2)] public Guid? PerformedBy { get; set; }

    /// <summary>The type of change that was made.</summary>
    [Id(3)] public CmsChangeType ChangeType { get; set; }

    /// <summary>The version before this operation.</summary>
    [Id(4)] public int FromVersion { get; set; }

    /// <summary>The version after this operation.</summary>
    [Id(5)] public int ToVersion { get; set; }

    /// <summary>The field-level changes that were made.</summary>
    [Id(6)] public List<FieldChange> Changes { get; set; } = [];

    /// <summary>Optional description of the operation.</summary>
    [Id(7)] public string? Description { get; set; }

    /// <summary>
    /// Whether this operation crossed a publish boundary.
    /// When true, undoing this operation requires creating a new draft.
    /// </summary>
    [Id(8)] public bool CrossedPublishBoundary { get; set; }

    /// <summary>
    /// Creates the inverse operation for undo.
    /// </summary>
    public UndoableOperation CreateInverse(Guid? performedBy = null)
    {
        return new UndoableOperation
        {
            OperationId = Guid.NewGuid().ToString(),
            PerformedAt = DateTimeOffset.UtcNow,
            PerformedBy = performedBy,
            ChangeType = ChangeType,
            FromVersion = ToVersion,
            ToVersion = FromVersion,
            Changes = Changes.Select(c => c.Inverse()).ToList(),
            Description = $"Undo: {Description}",
            CrossedPublishBoundary = CrossedPublishBoundary
        };
    }
}

/// <summary>
/// State for managing undo/redo stacks for a CMS document.
/// Key pattern: "{orgId}:{docType}:{docId}:undo"
/// </summary>
/// <remarks>
/// This state is used by companion grains that provide undo/redo functionality
/// for CMS documents. Key behaviors:
/// - Undo computes inverse field changes and applies them
/// - Redo replays the original operation
/// - Cross-version undo: Undo after publish creates a new draft with reverted content
/// - Undo stack persists across sessions
/// - Publishing clears the redo stack but preserves undo history
/// </remarks>
[GenerateSerializer]
public sealed class CmsUndoState
{
    /// <summary>The organization ID.</summary>
    [Id(0)] public Guid OrgId { get; set; }

    /// <summary>The document type (e.g., "MenuItem", "MenuCategory", "Recipe", "RecipeCategory").</summary>
    [Id(1)] public string DocumentType { get; set; } = string.Empty;

    /// <summary>The document ID.</summary>
    [Id(2)] public string DocumentId { get; set; } = string.Empty;

    /// <summary>Whether this undo grain has been initialized.</summary>
    [Id(3)] public bool IsInitialized { get; set; }

    /// <summary>
    /// Stack of operations that can be undone.
    /// Most recent operation is at the end of the list.
    /// </summary>
    [Id(4)] public List<UndoableOperation> UndoStack { get; set; } = [];

    /// <summary>
    /// Stack of operations that can be redone.
    /// Most recently undone operation is at the end of the list.
    /// </summary>
    [Id(5)] public List<UndoableOperation> RedoStack { get; set; } = [];

    /// <summary>Maximum number of operations to keep in the undo stack.</summary>
    [Id(6)] public int MaxUndoStackSize { get; set; } = 100;

    /// <summary>The last published version number.</summary>
    [Id(7)] public int? LastPublishedVersion { get; set; }

    /// <summary>Whether we're currently in a draft state.</summary>
    [Id(8)] public bool HasDraft { get; set; }

    /// <summary>Number of operations available to undo.</summary>
    public int UndoCount => UndoStack.Count;

    /// <summary>Number of operations available to redo.</summary>
    public int RedoCount => RedoStack.Count;

    /// <summary>Whether there are operations available to undo.</summary>
    public bool CanUndo => UndoStack.Count > 0;

    /// <summary>Whether there are operations available to redo.</summary>
    public bool CanRedo => RedoStack.Count > 0;

    /// <summary>
    /// Pushes an operation onto the undo stack.
    /// This clears the redo stack since a new change invalidates redo history.
    /// </summary>
    public void Push(UndoableOperation operation)
    {
        UndoStack.Add(operation);
        RedoStack.Clear();

        // Trim to max size
        while (UndoStack.Count > MaxUndoStackSize)
        {
            UndoStack.RemoveAt(0);
        }
    }

    /// <summary>
    /// Pops the most recent operation from the undo stack and moves it to redo.
    /// </summary>
    public UndoableOperation? PopUndo()
    {
        if (UndoStack.Count == 0)
            return null;

        var operation = UndoStack[^1];
        UndoStack.RemoveAt(UndoStack.Count - 1);
        RedoStack.Add(operation);
        return operation;
    }

    /// <summary>
    /// Pops the most recent operation from the redo stack and moves it to undo.
    /// </summary>
    public UndoableOperation? PopRedo()
    {
        if (RedoStack.Count == 0)
            return null;

        var operation = RedoStack[^1];
        RedoStack.RemoveAt(RedoStack.Count - 1);
        UndoStack.Add(operation);
        return operation;
    }

    /// <summary>
    /// Gets operations to undo without removing them.
    /// </summary>
    public IReadOnlyList<UndoableOperation> PeekUndo(int count = 1)
    {
        count = Math.Min(count, UndoStack.Count);
        return UndoStack.Skip(UndoStack.Count - count).Reverse().ToList();
    }

    /// <summary>
    /// Gets operations to redo without removing them.
    /// </summary>
    public IReadOnlyList<UndoableOperation> PeekRedo(int count = 1)
    {
        count = Math.Min(count, RedoStack.Count);
        return RedoStack.Skip(RedoStack.Count - count).Reverse().ToList();
    }

    /// <summary>
    /// Called when a draft is published.
    /// Marks all undo operations as having crossed the publish boundary.
    /// </summary>
    public void MarkPublished(int publishedVersion)
    {
        LastPublishedVersion = publishedVersion;
        HasDraft = false;

        // Mark all pending undo operations as crossing a publish boundary
        foreach (var op in UndoStack.Where(o => !o.CrossedPublishBoundary))
        {
            op.CrossedPublishBoundary = true;
        }

        // Clear redo stack - can't redo after publish
        RedoStack.Clear();
    }

    /// <summary>
    /// Called when a new draft is created.
    /// </summary>
    public void MarkDraftCreated()
    {
        HasDraft = true;
    }

    /// <summary>
    /// Clears all undo/redo history.
    /// </summary>
    public void Clear()
    {
        UndoStack.Clear();
        RedoStack.Clear();
    }
}
