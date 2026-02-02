using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// CMS Undo Grain Interface - Undo/Redo Stack Management
// ============================================================================

/// <summary>
/// Summary of the undo/redo stack state.
/// </summary>
[GenerateSerializer]
public record UndoStackSummary(
    [property: Id(0)] int UndoCount,
    [property: Id(1)] int RedoCount,
    [property: Id(2)] bool HasDraft,
    [property: Id(3)] int? LastPublishedVersion);

/// <summary>
/// Companion grain for managing undo/redo operations on a CMS document.
/// Key pattern: "{orgId}:{docType}:{docId}:undo"
/// </summary>
/// <remarks>
/// This grain provides undo/redo functionality for CMS documents:
/// - Push operations when changes occur
/// - Undo computes inverse field changes and returns them for application
/// - Redo replays the original operation
/// - Cross-version undo: Undo after publish returns changes that should create a new draft
/// - Undo stack persists across sessions
///
/// Important behaviors:
/// - The undo grain does NOT directly modify the document
/// - It returns the changes that should be applied by the caller
/// - Publishing clears the redo stack
/// - New changes clear the redo stack
///
/// Supports all CMS document types:
/// - MenuItem, MenuCategory, ModifierBlock
/// - Recipe, RecipeCategory
///
/// Example usage:
/// <code>
/// var undoGrain = grainFactory.GetGrain&lt;ICmsUndoGrain&gt;(
///     GrainKeys.CmsUndo(orgId, "MenuItem", documentId));
///
/// // Record a change
/// await undoGrain.PushAsync(change);
///
/// // Undo the last change
/// var result = await undoGrain.UndoAsync();
/// if (result.Success)
/// {
///     // Apply result.ChangesApplied to the document
/// }
/// </code>
/// </remarks>
public interface ICmsUndoGrain : IGrainWithStringKey
{
    /// <summary>
    /// Pushes a new operation onto the undo stack.
    /// This is called when a change is made to the document.
    /// Clears the redo stack.
    /// </summary>
    /// <param name="change">The change event that was performed.</param>
    Task PushAsync(CmsContentChanged change);

    /// <summary>
    /// Undoes the specified number of operations.
    /// Returns the inverse changes that should be applied to the document.
    /// </summary>
    /// <param name="count">Number of operations to undo (default: 1).</param>
    /// <param name="userId">The user performing the undo.</param>
    /// <returns>Result containing the changes to apply, or an error.</returns>
    Task<UndoResult> UndoAsync(int count = 1, Guid? userId = null);

    /// <summary>
    /// Redoes the specified number of previously undone operations.
    /// Returns the changes that should be applied to the document.
    /// </summary>
    /// <param name="count">Number of operations to redo (default: 1).</param>
    /// <param name="userId">The user performing the redo.</param>
    /// <returns>Result containing the changes to apply, or an error.</returns>
    Task<UndoResult> RedoAsync(int count = 1, Guid? userId = null);

    /// <summary>
    /// Previews what would happen if we undo the specified number of operations.
    /// Does not modify the undo stack.
    /// </summary>
    /// <param name="count">Number of operations to preview (default: 1).</param>
    /// <returns>The changes that would be applied.</returns>
    Task<IReadOnlyList<FieldChange>> PreviewUndoAsync(int count = 1);

    /// <summary>
    /// Previews what would happen if we redo the specified number of operations.
    /// Does not modify the undo stack.
    /// </summary>
    /// <param name="count">Number of operations to preview (default: 1).</param>
    /// <returns>The changes that would be applied.</returns>
    Task<IReadOnlyList<FieldChange>> PreviewRedoAsync(int count = 1);

    /// <summary>
    /// Gets the current sizes of the undo and redo stacks.
    /// </summary>
    Task<UndoStackSummary> GetStackSummaryAsync();

    /// <summary>
    /// Gets a summary of the operations in the undo stack.
    /// </summary>
    /// <param name="count">Maximum number of entries to return.</param>
    /// <returns>List of undoable operations (most recent first).</returns>
    Task<IReadOnlyList<UndoableOperation>> GetUndoStackAsync(int count = 10);

    /// <summary>
    /// Gets a summary of the operations in the redo stack.
    /// </summary>
    /// <param name="count">Maximum number of entries to return.</param>
    /// <returns>List of redoable operations (most recent first).</returns>
    Task<IReadOnlyList<UndoableOperation>> GetRedoStackAsync(int count = 10);

    /// <summary>
    /// Called when the document is published.
    /// Marks existing undo operations as crossing a publish boundary.
    /// Clears the redo stack.
    /// </summary>
    /// <param name="publishedVersion">The version that was published.</param>
    Task MarkPublishedAsync(int publishedVersion);

    /// <summary>
    /// Called when a draft is created.
    /// </summary>
    Task MarkDraftCreatedAsync();

    /// <summary>
    /// Clears all undo/redo history.
    /// </summary>
    Task ClearAsync();
}
