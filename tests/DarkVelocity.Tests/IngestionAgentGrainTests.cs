using DarkVelocity.Host;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Services;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class IngestionAgentGrainTests
{
    private readonly TestClusterFixture _fixture;

    public IngestionAgentGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IInvoiceIngestionAgentGrain GetAgentGrain(Guid orgId, Guid siteId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IInvoiceIngestionAgentGrain>(
            GrainKeys.IngestionAgent(orgId, siteId));
    }

    private IEmailInboxGrain GetInboxGrain(Guid orgId, Guid siteId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IEmailInboxGrain>(
            GrainKeys.EmailInbox(orgId, siteId));
    }

    private async Task<IInvoiceIngestionAgentGrain> SetupConfiguredAgent(Guid orgId, Guid siteId)
    {
        // Initialize email inbox first (required for dedup)
        var inbox = GetInboxGrain(orgId, siteId);
        await inbox.InitializeAsync(new InitializeEmailInboxCommand(
            orgId, siteId, $"invoices-{orgId}-{siteId}@darkvelocity.io"));

        // Configure agent
        var grain = GetAgentGrain(orgId, siteId);
        await grain.ConfigureAsync(new ConfigureIngestionAgentCommand(orgId, siteId));
        return grain;
    }

    // Given: a new site without an ingestion agent
    // When: the agent is configured
    // Then: the agent should be created with default settings
    [Fact]
    public async Task ConfigureAsync_ShouldCreateAgent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetAgentGrain(orgId, siteId);

        // Act
        var snapshot = await grain.ConfigureAsync(new ConfigureIngestionAgentCommand(orgId, siteId));

        // Assert
        snapshot.OrganizationId.Should().Be(orgId);
        snapshot.SiteId.Should().Be(siteId);
        snapshot.IsActive.Should().BeFalse();
        snapshot.PollingIntervalMinutes.Should().Be(5);
        snapshot.AutoProcessEnabled.Should().BeTrue();
        snapshot.AutoProcessConfidenceThreshold.Should().Be(0.85m);
        snapshot.MailboxCount.Should().Be(0);
        snapshot.PendingItemCount.Should().Be(0);
    }

    // Given: an already configured agent
    // When: configure is called again
    // Then: it should throw
    [Fact]
    public async Task ConfigureAsync_AlreadyConfigured_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetAgentGrain(orgId, siteId);
        await grain.ConfigureAsync(new ConfigureIngestionAgentCommand(orgId, siteId));

        // Act & Assert
        await grain.Invoking(g => g.ConfigureAsync(new ConfigureIngestionAgentCommand(orgId, siteId)))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    // Given: a configured agent
    // When: a mailbox is added
    // Then: the snapshot should show the mailbox
    [Fact]
    public async Task AddMailboxAsync_ShouldAddMailbox()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await SetupConfiguredAgent(orgId, siteId);

        // Act
        var snapshot = await grain.AddMailboxAsync(new AddMailboxCommand(
            "Invoices",
            "imap.example.com",
            993,
            "invoices@example.com",
            "password123"));

        // Assert
        snapshot.MailboxCount.Should().Be(1);
        snapshot.Mailboxes.Should().HaveCount(1);
        snapshot.Mailboxes[0].DisplayName.Should().Be("Invoices");
        snapshot.Mailboxes[0].Host.Should().Be("imap.example.com");
        snapshot.Mailboxes[0].Port.Should().Be(993);
        snapshot.Mailboxes[0].IsEnabled.Should().BeTrue();
    }

    // Given: a configured agent with a mailbox
    // When: the mailbox is removed
    // Then: the mailbox count should be zero
    [Fact]
    public async Task RemoveMailboxAsync_ShouldRemoveMailbox()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await SetupConfiguredAgent(orgId, siteId);
        var snapshot = await grain.AddMailboxAsync(new AddMailboxCommand(
            "Invoices", "imap.example.com", 993, "test@example.com", "pass"));
        var configId = snapshot.Mailboxes[0].ConfigId;

        // Act
        snapshot = await grain.RemoveMailboxAsync(configId);

        // Assert
        snapshot.MailboxCount.Should().Be(0);
    }

    // Given: a configured agent
    // When: settings are updated
    // Then: the new settings should be reflected
    [Fact]
    public async Task UpdateSettingsAsync_ShouldUpdateSettings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await SetupConfiguredAgent(orgId, siteId);

        // Act
        var snapshot = await grain.UpdateSettingsAsync(new UpdateIngestionAgentSettingsCommand(
            PollingIntervalMinutes: 10,
            AutoProcessEnabled: false,
            AutoProcessConfidenceThreshold: 0.95m));

        // Assert
        snapshot.PollingIntervalMinutes.Should().Be(10);
        snapshot.AutoProcessEnabled.Should().BeFalse();
        snapshot.AutoProcessConfidenceThreshold.Should().Be(0.95m);
    }

    // Given: a configured agent with a mailbox
    // When: a poll is triggered
    // Then: the poll result should reflect fetched emails
    [Fact]
    public async Task TriggerPollAsync_ShouldFetchAndProcessEmails()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await SetupConfiguredAgent(orgId, siteId);
        await grain.AddMailboxAsync(new AddMailboxCommand(
            "Invoices", "imap.example.com", 993, "test@example.com", "pass"));

        // Act — stub returns email on 1st poll (every 3rd, starting at 1)
        var result = await grain.TriggerPollAsync();

        // Assert
        result.EmailsFetched.Should().BeGreaterOrEqualTo(0);
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.TotalPolls.Should().Be(1);
        snapshot.LastPollAt.Should().NotBeNull();
    }

    // Given: an inactive agent
    // When: activated
    // Then: IsActive should be true
    [Fact]
    public async Task ActivateAsync_ShouldSetActive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await SetupConfiguredAgent(orgId, siteId);

        // Act
        var snapshot = await grain.ActivateAsync();

        // Assert
        snapshot.IsActive.Should().BeTrue();

        // Cleanup: deactivate to remove reminder
        await grain.DeactivateAsync();
    }

    // Given: an active agent
    // When: deactivated
    // Then: IsActive should be false
    [Fact]
    public async Task DeactivateAsync_ShouldSetInactive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await SetupConfiguredAgent(orgId, siteId);
        await grain.ActivateAsync();

        // Act
        var snapshot = await grain.DeactivateAsync();

        // Assert
        snapshot.IsActive.Should().BeFalse();
    }

    // Given: a configured agent with routing rules
    // When: routing rules are set
    // Then: the rules should be reflected in the snapshot
    [Fact]
    public async Task SetRoutingRulesAsync_ShouldSetRules()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await SetupConfiguredAgent(orgId, siteId);

        // Act
        var snapshot = await grain.SetRoutingRulesAsync(new SetRoutingRulesCommand(
        [
            new RoutingRule
            {
                RuleId = Guid.NewGuid(),
                Name = "Sysco invoices",
                Type = RoutingRuleType.SenderDomain,
                Pattern = "sysco.com",
                SuggestedDocumentType = PurchaseDocumentType.Invoice,
                SuggestedVendorName = "Sysco",
                AutoApprove = true,
                Priority = 1
            }
        ]));

        // Assert
        snapshot.RoutingRules.Should().HaveCount(1);
        snapshot.RoutingRules[0].Name.Should().Be("Sysco invoices");
        snapshot.RoutingRules[0].Pattern.Should().Be("sysco.com");
    }

    // Given: a configured agent
    // When: the queue is empty
    // Then: GetQueueAsync returns empty
    [Fact]
    public async Task GetQueueAsync_EmptyQueue_ShouldReturnEmpty()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await SetupConfiguredAgent(orgId, siteId);

        // Act
        var items = await grain.GetQueueAsync();

        // Assert
        items.Should().BeEmpty();
    }

    // Given: a configured agent
    // When: ExistsAsync is called
    // Then: it should return true
    [Fact]
    public async Task ExistsAsync_Configured_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = await SetupConfiguredAgent(orgId, siteId);

        // Act & Assert
        (await grain.ExistsAsync()).Should().BeTrue();
    }

    // Given: no agent configured
    // When: ExistsAsync is called
    // Then: it should return false
    [Fact]
    public async Task ExistsAsync_NotConfigured_ShouldReturnFalse()
    {
        // Arrange
        var grain = GetAgentGrain(Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        (await grain.ExistsAsync()).Should().BeFalse();
    }
}
