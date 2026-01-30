using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Customers.Api.Dtos;
using DarkVelocity.Shared.Contracts.Events;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Customers.Tests;

public class CustomerEventPublishingTests : IClassFixture<CustomersApiFixture>
{
    private readonly CustomersApiFixture _fixture;
    private readonly HttpClient _client;

    public CustomerEventPublishingTests(CustomersApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task Create_PublishesCustomerCreatedEvent()
    {
        _fixture.ClearEventLog();

        var request = new CreateCustomerRequest
        {
            Email = $"new.customer.{Guid.NewGuid():N}@example.com",
            FirstName = "New",
            LastName = "Customer",
            Source = "pos"
        };

        var response = await _client.PostAsJsonAsync("/api/customers", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var customer = await response.Content.ReadFromJsonAsync<CustomerDto>();

        var events = _fixture.GetEventBus().GetEventLog();
        var createdEvent = events.OfType<CustomerCreated>().FirstOrDefault(e => e.CustomerId == customer!.Id);

        createdEvent.Should().NotBeNull();
        createdEvent!.TenantId.Should().Be(_fixture.TestTenantId);
        createdEvent.Email.Should().Contain("new.customer");
        createdEvent.FirstName.Should().Be("New");
        createdEvent.LastName.Should().Be("Customer");
        createdEvent.Source.Should().Be("pos");
    }

    [Fact]
    public async Task Update_PublishesCustomerUpdatedEvent()
    {
        // Create a customer first
        var createRequest = new CreateCustomerRequest
        {
            Email = $"update.test.{Guid.NewGuid():N}@example.com",
            FirstName = "Original",
            LastName = "Name",
            Source = "pos"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/customers", createRequest);
        var customer = await createResponse.Content.ReadFromJsonAsync<CustomerDto>();

        _fixture.ClearEventLog();

        // Update the customer
        var updateRequest = new UpdateCustomerRequest
        {
            FirstName = "Updated",
            LastName = "Name",
            Phone = "+1234567890"
        };

        var response = await _client.PutAsJsonAsync($"/api/customers/{customer!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = _fixture.GetEventBus().GetEventLog();
        var updatedEvent = events.OfType<CustomerUpdated>().FirstOrDefault(e => e.CustomerId == customer.Id);

        updatedEvent.Should().NotBeNull();
        updatedEvent!.TenantId.Should().Be(_fixture.TestTenantId);
        updatedEvent.ChangedFields.Should().Contain("FirstName");
        updatedEvent.ChangedFields.Should().Contain("Phone");
    }

    [Fact]
    public async Task Delete_PublishesCustomerDeletedEvent()
    {
        // Create a customer first
        var createRequest = new CreateCustomerRequest
        {
            Email = $"delete.test.{Guid.NewGuid():N}@example.com",
            FirstName = "To",
            LastName = "Delete",
            Source = "pos"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/customers", createRequest);
        var customer = await createResponse.Content.ReadFromJsonAsync<CustomerDto>();

        _fixture.ClearEventLog();

        // Delete the customer
        var response = await _client.DeleteAsync($"/api/customers/{customer!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var events = _fixture.GetEventBus().GetEventLog();
        var deletedEvent = events.OfType<CustomerDeleted>().FirstOrDefault(e => e.CustomerId == customer.Id);

        deletedEvent.Should().NotBeNull();
        deletedEvent!.TenantId.Should().Be(_fixture.TestTenantId);
    }

    [Fact]
    public async Task Enroll_PublishesCustomerEnrolledInLoyaltyEvent()
    {
        // Create a customer first
        var createRequest = new CreateCustomerRequest
        {
            Email = $"enroll.test.{Guid.NewGuid():N}@example.com",
            FirstName = "Enroll",
            LastName = "Test",
            Source = "pos"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/customers", createRequest);
        var customer = await createResponse.Content.ReadFromJsonAsync<CustomerDto>();

        _fixture.ClearEventLog();

        // Enroll in loyalty program
        var enrollRequest = new EnrollCustomerRequest { ProgramId = _fixture.TestProgramId };

        var response = await _client.PostAsJsonAsync(
            $"/api/customers/{customer!.Id}/loyalty/enroll",
            enrollRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var events = _fixture.GetEventBus().GetEventLog();
        var enrolledEvent = events.OfType<CustomerEnrolledInLoyalty>().FirstOrDefault(e => e.CustomerId == customer.Id);

        enrolledEvent.Should().NotBeNull();
        enrolledEvent!.TenantId.Should().Be(_fixture.TestTenantId);
        enrolledEvent.ProgramId.Should().Be(_fixture.TestProgramId);
        enrolledEvent.ProgramName.Should().Be("Test Rewards");
        enrolledEvent.WelcomeBonus.Should().Be(50);
    }

    [Fact]
    public async Task EarnPoints_PublishesPointsEarnedEvent()
    {
        _fixture.ClearEventLog();

        var earnRequest = new EarnPointsRequest
        {
            Points = 100,
            Description = "Test purchase",
            OrderId = Guid.NewGuid(),
            LocationId = Guid.NewGuid()
        };

        var response = await _client.PostAsJsonAsync(
            $"/api/customers/{_fixture.TestCustomerId}/loyalty/earn",
            earnRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = _fixture.GetEventBus().GetEventLog();
        var earnedEvent = events.OfType<PointsEarned>().FirstOrDefault(e => e.CustomerId == _fixture.TestCustomerId);

        earnedEvent.Should().NotBeNull();
        earnedEvent!.TenantId.Should().Be(_fixture.TestTenantId);
        earnedEvent.Points.Should().Be(100); // Multiplied by tier multiplier (1.0)
        earnedEvent.OrderId.Should().NotBeNull();
    }

    [Fact]
    public async Task RedeemPoints_PublishesPointsRedeemedEvent()
    {
        // First earn some points to ensure we have enough
        var earnRequest = new EarnPointsRequest
        {
            Points = 200,
            Description = "Prep for redeem test"
        };

        await _client.PostAsJsonAsync(
            $"/api/customers/{_fixture.TestCustomerId}/loyalty/earn",
            earnRequest);

        _fixture.ClearEventLog();

        var redeemRequest = new RedeemPointsRequest
        {
            Points = 100,
            Description = "Test redemption"
        };

        var response = await _client.PostAsJsonAsync(
            $"/api/customers/{_fixture.TestCustomerId}/loyalty/redeem",
            redeemRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = _fixture.GetEventBus().GetEventLog();
        var redeemedEvent = events.OfType<PointsRedeemed>().FirstOrDefault(e => e.CustomerId == _fixture.TestCustomerId);

        redeemedEvent.Should().NotBeNull();
        redeemedEvent!.TenantId.Should().Be(_fixture.TestTenantId);
        redeemedEvent.Points.Should().Be(100);
    }

    [Fact]
    public async Task EarnPoints_WithTierUpgrade_PublishesTierChangedEvent()
    {
        // Create a new customer near tier threshold
        var createRequest = new CreateCustomerRequest
        {
            Email = $"tier.test.{Guid.NewGuid():N}@example.com",
            FirstName = "Tier",
            LastName = "Upgrade",
            Source = "pos"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/customers", createRequest);
        var customer = await createResponse.Content.ReadFromJsonAsync<CustomerDto>();

        // Enroll in loyalty program
        var enrollRequest = new EnrollCustomerRequest { ProgramId = _fixture.TestProgramId };
        await _client.PostAsJsonAsync($"/api/customers/{customer!.Id}/loyalty/enroll", enrollRequest);

        // Earn enough points to trigger tier upgrade (need 500 for Gold)
        var earnRequest1 = new EarnPointsRequest { Points = 300, Description = "First purchase" };
        await _client.PostAsJsonAsync($"/api/customers/{customer.Id}/loyalty/earn", earnRequest1);

        _fixture.ClearEventLog();

        // This should trigger tier upgrade to Gold
        var earnRequest2 = new EarnPointsRequest { Points = 200, Description = "Tier upgrade purchase" };
        var response = await _client.PostAsJsonAsync($"/api/customers/{customer.Id}/loyalty/earn", earnRequest2);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = _fixture.GetEventBus().GetEventLog();
        var tierChangedEvent = events.OfType<TierChanged>().FirstOrDefault(e => e.CustomerId == customer.Id);

        tierChangedEvent.Should().NotBeNull();
        tierChangedEvent!.TenantId.Should().Be(_fixture.TestTenantId);
        tierChangedEvent.NewTierName.Should().Be("Gold");
        tierChangedEvent.Reason.Should().Be("Points threshold reached");
    }

    [Fact]
    public async Task IssueReward_PublishesRewardIssuedAndPointsRedeemedEvents()
    {
        // Ensure customer has enough points
        var earnRequest = new EarnPointsRequest { Points = 200, Description = "Prep for reward" };
        await _client.PostAsJsonAsync($"/api/customers/{_fixture.TestCustomerId}/loyalty/earn", earnRequest);

        _fixture.ClearEventLog();

        var response = await _client.GetAsync(
            $"/api/rewards/{_fixture.TestRewardId}/issue?customerId={_fixture.TestCustomerId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = _fixture.GetEventBus().GetEventLog();

        // Check PointsRedeemed event
        var pointsRedeemedEvent = events.OfType<PointsRedeemed>()
            .FirstOrDefault(e => e.CustomerId == _fixture.TestCustomerId && e.RewardId == _fixture.TestRewardId);
        pointsRedeemedEvent.Should().NotBeNull();
        pointsRedeemedEvent!.Points.Should().Be(100); // Reward costs 100 points

        // Check RewardIssued event
        var rewardIssuedEvent = events.OfType<RewardIssued>()
            .FirstOrDefault(e => e.CustomerId == _fixture.TestCustomerId && e.RewardId == _fixture.TestRewardId);
        rewardIssuedEvent.Should().NotBeNull();
        rewardIssuedEvent!.RewardName.Should().Be("Free Coffee");
        rewardIssuedEvent.Code.Should().NotBeNullOrEmpty();
    }
}
