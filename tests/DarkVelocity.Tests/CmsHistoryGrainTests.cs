using DarkVelocity.Host;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
public class CmsHistoryGrainTests
{
    private readonly TestClusterFixture _fixture;

    public CmsHistoryGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private ICmsHistoryGrain GetGrain(Guid orgId, string docType, string docId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<ICmsHistoryGrain>(
            GrainKeys.CmsHistory(orgId, docType, docId));
    }

    [Fact]
    public async Task RecordChangeAsync_ShouldStoreChangeEvent()
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
            Changes: [FieldChange.Set("Name", null, "\"Test Item\"")],
            ChangeNote: "Initial creation");

        // Act
        await grain.RecordChangeAsync(change);

        // Assert
        var totalChanges = await grain.GetTotalChangesAsync();
        totalChanges.Should().Be(1);

        var history = await grain.GetHistoryAsync(0, 10);
        history.Should().HaveCount(1);
        history[0].ChangeType.Should().Be(CmsChangeType.Created);
        history[0].DocumentId.Should().Be(docId);
    }

    [Fact]
    public async Task GetHistoryAsync_ShouldReturnChangesInReverseOrder()
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
            OccurredAt: DateTimeOffset.UtcNow.AddMinutes(-2),
            ChangeType: CmsChangeType.Created,
            Changes: [],
            ChangeNote: "Created");

        var change2 = new CmsContentChanged(
            DocumentType: "MenuItem",
            DocumentId: docId,
            OrgId: orgId,
            FromVersion: 1,
            ToVersion: 2,
            ChangedBy: null,
            OccurredAt: DateTimeOffset.UtcNow.AddMinutes(-1),
            ChangeType: CmsChangeType.DraftCreated,
            Changes: [],
            ChangeNote: "Draft created");

        var change3 = new CmsContentChanged(
            DocumentType: "MenuItem",
            DocumentId: docId,
            OrgId: orgId,
            FromVersion: 2,
            ToVersion: 2,
            ChangedBy: null,
            OccurredAt: DateTimeOffset.UtcNow,
            ChangeType: CmsChangeType.Published,
            Changes: [],
            ChangeNote: "Published");

        await grain.RecordChangeAsync(change1);
        await grain.RecordChangeAsync(change2);
        await grain.RecordChangeAsync(change3);

        // Act
        var history = await grain.GetHistoryAsync(0, 10);

        // Assert
        history.Should().HaveCount(3);
        history[0].ChangeType.Should().Be(CmsChangeType.Published);
        history[1].ChangeType.Should().Be(CmsChangeType.DraftCreated);
        history[2].ChangeType.Should().Be(CmsChangeType.Created);
    }

    [Fact]
    public async Task GetHistorySummaryAsync_ShouldReturnSummaries()
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
            Changes: [
                FieldChange.Set("Name", null, "\"Test\""),
                FieldChange.Set("Price", null, "9.99")
            ],
            ChangeNote: "Created item");

        await grain.RecordChangeAsync(change);

        // Act
        var summaries = await grain.GetHistorySummaryAsync(0, 10);

        // Assert
        summaries.Should().HaveCount(1);
        summaries[0].FieldChangeCount.Should().Be(2);
        summaries[0].ChangeNote.Should().Be("Created item");
    }

    [Fact]
    public async Task GetDiffAsync_ShouldComputeAggregateDiff()
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
            OccurredAt: DateTimeOffset.UtcNow.AddMinutes(-2),
            ChangeType: CmsChangeType.Created,
            Changes: [
                FieldChange.Set("Name", null, "\"Original\""),
                FieldChange.Set("Price", null, "10.00")
            ],
            ChangeNote: null);

        var change2 = new CmsContentChanged(
            DocumentType: "MenuItem",
            DocumentId: docId,
            OrgId: orgId,
            FromVersion: 1,
            ToVersion: 2,
            ChangedBy: null,
            OccurredAt: DateTimeOffset.UtcNow.AddMinutes(-1),
            ChangeType: CmsChangeType.DraftCreated,
            Changes: [
                FieldChange.Set("Name", "\"Original\"", "\"Updated\""),
                FieldChange.Set("Price", "10.00", "15.00")
            ],
            ChangeNote: null);

        var change3 = new CmsContentChanged(
            DocumentType: "MenuItem",
            DocumentId: docId,
            OrgId: orgId,
            FromVersion: 2,
            ToVersion: 3,
            ChangedBy: null,
            OccurredAt: DateTimeOffset.UtcNow,
            ChangeType: CmsChangeType.DraftCreated,
            Changes: [
                FieldChange.Set("Price", "15.00", "12.00")
            ],
            ChangeNote: null);

        await grain.RecordChangeAsync(change1);
        await grain.RecordChangeAsync(change2);
        await grain.RecordChangeAsync(change3);

        // Act
        var diff = await grain.GetDiffAsync(0, 3);

        // Assert
        diff.FromVersion.Should().Be(0);
        diff.ToVersion.Should().Be(3);
        diff.ChangeEvents.Should().HaveCount(3);

        // Should aggregate changes - Name went from null to "Updated", Price from null to 12.00
        diff.Changes.Should().Contain(c => c.FieldPath == "Name" && c.OldValue == null && c.NewValue == "\"Updated\"");
        diff.Changes.Should().Contain(c => c.FieldPath == "Price" && c.OldValue == null && c.NewValue == "12.00");
    }

    [Fact]
    public async Task GetChangesForVersionAsync_ShouldReturnChangesForSpecificVersion()
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
            ChangeNote: "Version 1");

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
            ChangeNote: "Version 2");

        await grain.RecordChangeAsync(change1);
        await grain.RecordChangeAsync(change2);

        // Act
        var changesFor2 = await grain.GetChangesForVersionAsync(2);

        // Assert
        changesFor2.Should().HaveCount(1);
        changesFor2[0].ChangeNote.Should().Be("Version 2");
    }

    [Fact]
    public async Task ClearHistoryAsync_ShouldRemoveAllHistory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var docId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, "MenuItem", docId);

        await grain.RecordChangeAsync(new CmsContentChanged(
            DocumentType: "MenuItem",
            DocumentId: docId,
            OrgId: orgId,
            FromVersion: 0,
            ToVersion: 1,
            ChangedBy: null,
            OccurredAt: DateTimeOffset.UtcNow,
            ChangeType: CmsChangeType.Created,
            Changes: [],
            ChangeNote: null));

        // Act
        await grain.ClearHistoryAsync();

        // Assert
        var total = await grain.GetTotalChangesAsync();
        total.Should().Be(0);
    }
}
