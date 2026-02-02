using DarkVelocity.Host;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
public class CmsUndoGrainTests
{
    private readonly TestClusterFixture _fixture;

    public CmsUndoGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private ICmsUndoGrain GetGrain(Guid orgId, string docType, string docId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<ICmsUndoGrain>(
            GrainKeys.CmsUndo(orgId, docType, docId));
    }

    [Fact]
    public async Task PushAsync_ShouldAddToUndoStack()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var docId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, "MenuItem", docId);

        var change = new CmsContentChanged(
            DocumentType: "MenuItem",
            DocumentId: docId,
            OrgId: orgId,
            FromVersion: 0,
            ToVersion: 1,
            ChangedBy: Guid.NewGuid(),
            OccurredAt: DateTimeOffset.UtcNow,
            ChangeType: CmsChangeType.Created,
            Changes: [FieldChange.Set("Name", null, "\"Test\"")],
            ChangeNote: "Created");

        // Act
        await grain.PushAsync(change);

        // Assert
        var summary = await grain.GetStackSummaryAsync();
        summary.UndoCount.Should().Be(1);
        summary.RedoCount.Should().Be(0);
    }

    [Fact]
    public async Task UndoAsync_ShouldMoveOperationToRedoStack()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var docId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, "MenuItem", docId);

        var change = new CmsContentChanged(
            DocumentType: "MenuItem",
            DocumentId: docId,
            OrgId: orgId,
            FromVersion: 0,
            ToVersion: 1,
            ChangedBy: null,
            OccurredAt: DateTimeOffset.UtcNow,
            ChangeType: CmsChangeType.Created,
            Changes: [FieldChange.Set("Name", null, "\"Test\"")],
            ChangeNote: null);

        await grain.PushAsync(change);

        // Act
        var result = await grain.UndoAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.NewVersion.Should().Be(0);
        result.ChangesApplied.Should().HaveCount(1);
        result.ChangesApplied[0].FieldPath.Should().Be("Name");
        result.ChangesApplied[0].OldValue.Should().Be("\"Test\""); // Inverse
        result.ChangesApplied[0].NewValue.Should().BeNull();

        var summary = await grain.GetStackSummaryAsync();
        summary.UndoCount.Should().Be(0);
        summary.RedoCount.Should().Be(1);
    }

    [Fact]
    public async Task UndoAsync_WithNoOperations_ShouldFail()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var docId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, "MenuItem", docId);

        // Act
        var result = await grain.UndoAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("0 operations");
    }

    [Fact]
    public async Task RedoAsync_ShouldReplayOperation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var docId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, "MenuItem", docId);

        var change = new CmsContentChanged(
            DocumentType: "MenuItem",
            DocumentId: docId,
            OrgId: orgId,
            FromVersion: 0,
            ToVersion: 1,
            ChangedBy: null,
            OccurredAt: DateTimeOffset.UtcNow,
            ChangeType: CmsChangeType.Created,
            Changes: [FieldChange.Set("Price", null, "10.00")],
            ChangeNote: null);

        await grain.PushAsync(change);
        await grain.UndoAsync();

        // Act
        var result = await grain.RedoAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.NewVersion.Should().Be(1);
        result.ChangesApplied.Should().HaveCount(1);
        result.ChangesApplied[0].FieldPath.Should().Be("Price");
        result.ChangesApplied[0].NewValue.Should().Be("10.00");

        var summary = await grain.GetStackSummaryAsync();
        summary.UndoCount.Should().Be(1);
        summary.RedoCount.Should().Be(0);
    }

    [Fact]
    public async Task UndoAsync_Multiple_ShouldUndoMultipleOperations()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var docId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, "MenuItem", docId);

        var change1 = new CmsContentChanged(
            DocumentType: "MenuItem",
            DocumentId: docId,
            OrgId: orgId,
            FromVersion: 0,
            ToVersion: 1,
            ChangedBy: null,
            OccurredAt: DateTimeOffset.UtcNow,
            ChangeType: CmsChangeType.Created,
            Changes: [FieldChange.Set("Name", null, "\"First\"")],
            ChangeNote: null);

        var change2 = new CmsContentChanged(
            DocumentType: "MenuItem",
            DocumentId: docId,
            OrgId: orgId,
            FromVersion: 1,
            ToVersion: 2,
            ChangedBy: null,
            OccurredAt: DateTimeOffset.UtcNow,
            ChangeType: CmsChangeType.DraftCreated,
            Changes: [FieldChange.Set("Name", "\"First\"", "\"Second\"")],
            ChangeNote: null);

        await grain.PushAsync(change1);
        await grain.PushAsync(change2);

        // Act
        var result = await grain.UndoAsync(count: 2);

        // Assert
        result.Success.Should().BeTrue();
        result.NewVersion.Should().Be(0);
        result.ChangesApplied.Should().HaveCount(2);

        var summary = await grain.GetStackSummaryAsync();
        summary.UndoCount.Should().Be(0);
        summary.RedoCount.Should().Be(2);
    }

    [Fact]
    public async Task PreviewUndoAsync_ShouldNotModifyStack()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var docId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, "MenuItem", docId);

        var change = new CmsContentChanged(
            DocumentType: "MenuItem",
            DocumentId: docId,
            OrgId: orgId,
            FromVersion: 0,
            ToVersion: 1,
            ChangedBy: null,
            OccurredAt: DateTimeOffset.UtcNow,
            ChangeType: CmsChangeType.Created,
            Changes: [FieldChange.Set("Name", null, "\"Preview Test\"")],
            ChangeNote: null);

        await grain.PushAsync(change);

        // Act
        var preview = await grain.PreviewUndoAsync();

        // Assert
        preview.Should().HaveCount(1);
        preview[0].FieldPath.Should().Be("Name");

        // Stack should be unchanged
        var summary = await grain.GetStackSummaryAsync();
        summary.UndoCount.Should().Be(1);
        summary.RedoCount.Should().Be(0);
    }

    [Fact]
    public async Task MarkPublishedAsync_ShouldClearRedoAndMarkCrossPublish()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var docId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, "MenuItem", docId);

        var change = new CmsContentChanged(
            DocumentType: "MenuItem",
            DocumentId: docId,
            OrgId: orgId,
            FromVersion: 0,
            ToVersion: 1,
            ChangedBy: null,
            OccurredAt: DateTimeOffset.UtcNow,
            ChangeType: CmsChangeType.DraftCreated,
            Changes: [],
            ChangeNote: null);

        await grain.PushAsync(change);
        await grain.UndoAsync();

        // At this point we have 1 in redo stack
        var beforePublish = await grain.GetStackSummaryAsync();
        beforePublish.RedoCount.Should().Be(1);

        // Act
        await grain.MarkPublishedAsync(1);

        // Assert
        var summary = await grain.GetStackSummaryAsync();
        summary.RedoCount.Should().Be(0); // Redo cleared
        summary.LastPublishedVersion.Should().Be(1);
        summary.HasDraft.Should().BeFalse();
    }

    [Fact]
    public async Task PushAsync_ShouldClearRedoStack()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var docId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, "MenuItem", docId);

        var change1 = new CmsContentChanged(
            DocumentType: "MenuItem",
            DocumentId: docId,
            OrgId: orgId,
            FromVersion: 0,
            ToVersion: 1,
            ChangedBy: null,
            OccurredAt: DateTimeOffset.UtcNow,
            ChangeType: CmsChangeType.Created,
            Changes: [],
            ChangeNote: null);

        await grain.PushAsync(change1);
        await grain.UndoAsync();

        var beforeNewPush = await grain.GetStackSummaryAsync();
        beforeNewPush.RedoCount.Should().Be(1);

        var change2 = new CmsContentChanged(
            DocumentType: "MenuItem",
            DocumentId: docId,
            OrgId: orgId,
            FromVersion: 0,
            ToVersion: 2,
            ChangedBy: null,
            OccurredAt: DateTimeOffset.UtcNow,
            ChangeType: CmsChangeType.DraftCreated,
            Changes: [],
            ChangeNote: null);

        // Act
        await grain.PushAsync(change2);

        // Assert
        var summary = await grain.GetStackSummaryAsync();
        summary.UndoCount.Should().Be(1);
        summary.RedoCount.Should().Be(0); // New change clears redo
    }

    [Fact]
    public async Task GetUndoStackAsync_ShouldReturnOperationsInOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var docId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, "MenuItem", docId);

        var change1 = new CmsContentChanged(
            DocumentType: "MenuItem",
            DocumentId: docId,
            OrgId: orgId,
            FromVersion: 0,
            ToVersion: 1,
            ChangedBy: null,
            OccurredAt: DateTimeOffset.UtcNow,
            ChangeType: CmsChangeType.Created,
            Changes: [],
            ChangeNote: "First");

        var change2 = new CmsContentChanged(
            DocumentType: "MenuItem",
            DocumentId: docId,
            OrgId: orgId,
            FromVersion: 1,
            ToVersion: 2,
            ChangedBy: null,
            OccurredAt: DateTimeOffset.UtcNow,
            ChangeType: CmsChangeType.DraftCreated,
            Changes: [],
            ChangeNote: "Second");

        await grain.PushAsync(change1);
        await grain.PushAsync(change2);

        // Act
        var stack = await grain.GetUndoStackAsync(10);

        // Assert
        stack.Should().HaveCount(2);
        stack[0].Description.Should().Contain("Second"); // Most recent first
        stack[1].Description.Should().Contain("First");
    }

    [Fact]
    public async Task ClearAsync_ShouldClearBothStacks()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var docId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, "MenuItem", docId);

        var change = new CmsContentChanged(
            DocumentType: "MenuItem",
            DocumentId: docId,
            OrgId: orgId,
            FromVersion: 0,
            ToVersion: 1,
            ChangedBy: null,
            OccurredAt: DateTimeOffset.UtcNow,
            ChangeType: CmsChangeType.Created,
            Changes: [],
            ChangeNote: null);

        await grain.PushAsync(change);
        await grain.UndoAsync();

        // Act
        await grain.ClearAsync();

        // Assert
        var summary = await grain.GetStackSummaryAsync();
        summary.UndoCount.Should().Be(0);
        summary.RedoCount.Should().Be(0);
    }

    [Fact]
    public async Task MarkDraftCreatedAsync_ShouldSetHasDraft()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var docId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, "MenuItem", docId);

        // Act
        await grain.MarkDraftCreatedAsync();

        // Assert
        var summary = await grain.GetStackSummaryAsync();
        summary.HasDraft.Should().BeTrue();
    }
}
