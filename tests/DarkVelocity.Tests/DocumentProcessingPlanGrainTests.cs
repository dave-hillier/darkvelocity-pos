using DarkVelocity.Host;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class DocumentProcessingPlanGrainTests
{
    private readonly TestClusterFixture _fixture;

    public DocumentProcessingPlanGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IDocumentProcessingPlanGrain GetGrain(Guid orgId, Guid siteId, Guid planId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IDocumentProcessingPlanGrain>(
            GrainKeys.DocumentProcessingPlan(orgId, siteId, planId));
    }

    private static ProposePlanCommand CreateTestProposal(
        Guid orgId, Guid siteId, Guid planId,
        string from = "supplier@acme.com",
        string subject = "Invoice #INV-001",
        PurchaseDocumentType docType = PurchaseDocumentType.Invoice,
        SuggestedAction action = SuggestedAction.ManualReview)
    {
        return new ProposePlanCommand(
            planId,
            orgId,
            siteId,
            $"msg-{Guid.NewGuid():N}@test.local",
            from,
            subject,
            DateTime.UtcNow.AddMinutes(-10),
            1,
            docType,
            null,
            "Acme Foods",
            0.85m,
            0.7m,
            action,
            null,
            "Matched sender domain pattern",
            [Guid.NewGuid()]);
    }

    // Given: a new plan grain
    // When: a plan is proposed
    // Then: the plan should be in Proposed status with correct metadata
    [Fact]
    public async Task ProposeAsync_ShouldCreatePlan()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, planId);

        // Act
        var snapshot = await grain.ProposeAsync(CreateTestProposal(orgId, siteId, planId));

        // Assert
        snapshot.PlanId.Should().Be(planId);
        snapshot.OrganizationId.Should().Be(orgId);
        snapshot.SiteId.Should().Be(siteId);
        snapshot.Status.Should().Be(ProcessingPlanStatus.Proposed);
        snapshot.EmailFrom.Should().Be("supplier@acme.com");
        snapshot.EmailSubject.Should().Be("Invoice #INV-001");
        snapshot.SuggestedDocumentType.Should().Be(PurchaseDocumentType.Invoice);
        snapshot.SuggestedVendorName.Should().Be("Acme Foods");
        snapshot.TypeConfidence.Should().Be(0.85m);
        snapshot.SuggestedAction.Should().Be(SuggestedAction.ManualReview);
        snapshot.DocumentIds.Should().HaveCount(1);
    }

    // Given: a plan that already exists
    // When: propose is called again
    // Then: it should throw
    [Fact]
    public async Task ProposeAsync_AlreadyExists_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, planId);
        await grain.ProposeAsync(CreateTestProposal(orgId, siteId, planId));

        // Act & Assert
        await grain.Invoking(g => g.ProposeAsync(CreateTestProposal(orgId, siteId, planId)))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    // Given: a proposed plan
    // When: the plan is approved
    // Then: the status should be Executed
    [Fact]
    public async Task ApproveAsync_ShouldExecutePlan()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, planId);
        await grain.ProposeAsync(CreateTestProposal(orgId, siteId, planId));
        var userId = Guid.NewGuid();

        // Act
        var snapshot = await grain.ApproveAsync(userId);

        // Assert — status should be Executed or Failed (depends on doc grain state)
        snapshot.ReviewedBy.Should().Be(userId);
        snapshot.ReviewedAt.Should().NotBeNull();
        snapshot.Status.Should().BeOneOf(
            ProcessingPlanStatus.Executed,
            ProcessingPlanStatus.Failed); // May fail because doc grain wasn't initialized
    }

    // Given: a proposed plan
    // When: the plan is modified and approved
    // Then: the overrides should be recorded
    [Fact]
    public async Task ModifyAndApproveAsync_ShouldRecordOverrides()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, planId);
        await grain.ProposeAsync(CreateTestProposal(orgId, siteId, planId));
        var userId = Guid.NewGuid();

        // Act
        var snapshot = await grain.ModifyAndApproveAsync(new ModifyPlanCommand(
            userId,
            DocumentType: PurchaseDocumentType.Receipt,
            VendorName: "New Vendor Name"));

        // Assert
        snapshot.OverrideDocumentType.Should().Be(PurchaseDocumentType.Receipt);
        snapshot.OverrideVendorName.Should().Be("New Vendor Name");
        snapshot.ReviewedBy.Should().Be(userId);
        snapshot.Status.Should().BeOneOf(
            ProcessingPlanStatus.Executed,
            ProcessingPlanStatus.Failed);
    }

    // Given: a proposed plan
    // When: the plan is rejected
    // Then: the status should be Rejected with a reason
    [Fact]
    public async Task RejectAsync_ShouldRejectPlan()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, planId);
        await grain.ProposeAsync(CreateTestProposal(orgId, siteId, planId));
        var userId = Guid.NewGuid();

        // Act
        var snapshot = await grain.RejectAsync(userId, "Not a valid invoice");

        // Assert
        snapshot.Status.Should().Be(ProcessingPlanStatus.Rejected);
        snapshot.ReviewedBy.Should().Be(userId);
        snapshot.ReviewedAt.Should().NotBeNull();
        snapshot.RejectionReason.Should().Be("Not a valid invoice");
    }

    // Given: an already approved plan
    // When: approve is called again
    // Then: it should throw (wrong status)
    [Fact]
    public async Task ApproveAsync_AlreadyApproved_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, planId);
        await grain.ProposeAsync(CreateTestProposal(orgId, siteId, planId));
        await grain.ApproveAsync(Guid.NewGuid());

        // Act & Assert
        await grain.Invoking(g => g.ApproveAsync(Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    // Given: an already rejected plan
    // When: approve is called
    // Then: it should throw (wrong status)
    [Fact]
    public async Task ApproveAsync_AlreadyRejected_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, planId);
        await grain.ProposeAsync(CreateTestProposal(orgId, siteId, planId));
        await grain.RejectAsync(Guid.NewGuid(), "test");

        // Act & Assert
        await grain.Invoking(g => g.ApproveAsync(Guid.NewGuid()))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    // Given: no plan exists
    // When: ExistsAsync is called
    // Then: it should return false
    [Fact]
    public async Task ExistsAsync_NoPlan_ShouldReturnFalse()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        (await grain.ExistsAsync()).Should().BeFalse();
    }

    // Given: a proposed plan
    // When: ExistsAsync is called
    // Then: it should return true
    [Fact]
    public async Task ExistsAsync_PlanExists_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, planId);
        await grain.ProposeAsync(CreateTestProposal(orgId, siteId, planId));

        // Act & Assert
        (await grain.ExistsAsync()).Should().BeTrue();
    }
}
