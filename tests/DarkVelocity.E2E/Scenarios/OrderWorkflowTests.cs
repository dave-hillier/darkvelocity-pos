using System.Net;
using DarkVelocity.E2E.Clients;
using DarkVelocity.E2E.Fixtures;
using FluentAssertions;

namespace DarkVelocity.E2E.Scenarios;

[Collection(E2ECollection.Name)]
public class OrderWorkflowTests
{
    private readonly ServiceFixture _fixture;

    public OrderWorkflowTests(ServiceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Full_dine_in_order_lifecycle()
    {
        // Arrange - unique IDs for test isolation
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var client = new DarkVelocityClient(_fixture.HttpClient, _fixture.BaseUrl);
        client.Authenticate(userId, orgId, siteId, sessionId);

        // Create org and site first
        await client.PostAsync("/api/orgs", new
        {
            name = $"Order Test Org {orgId:N}",
            slug = $"order-{orgId:N}"
        });

        await client.PostAsync($"/api/orgs/{orgId}/sites", new
        {
            name = "Order Test Site",
            code = "OTS",
            address = new
            {
                street = "1 High Street",
                city = "London",
                state = "England",
                postalCode = "EC1A 1BB",
                country = "GB"
            },
            timezone = "Europe/London",
            currency = "GBP"
        });

        // Set up SpiceDB relationships for this user to operate on the site
        await _fixture.SpiceDb.SetupUserForSiteOperationsAsync(userId, orgId, siteId);

        var ordersPath = $"/api/orgs/{orgId}/sites/{siteId}/orders";
        var paymentsPath = $"/api/orgs/{orgId}/sites/{siteId}/payments";

        // Act 1 - Create order
        using var createOrderResponse = await client.PostAsync(ordersPath, new
        {
            createdBy = userId,
            type = 0, // DineIn
            guestCount = 2
        });

        createOrderResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            "should create order successfully");
        var orderId = createOrderResponse.GetId();
        orderId.Should().NotBeNullOrEmpty();

        var orderPath = $"{ordersPath}/{orderId}";

        // Act 2 - Add line items
        var menuItemId = Guid.NewGuid();
        using var addLineResponse = await client.PostAsync($"{orderPath}/lines", new
        {
            menuItemId,
            name = "Fish and Chips",
            quantity = 2,
            unitPrice = 14.50m,
            taxRate = 0.20m
        });

        addLineResponse.IsSuccess.Should().BeTrue("should add line item successfully");

        // Act 3 - Send order to kitchen
        using var sendResponse = await client.PostAsync($"{orderPath}/send", new
        {
            sentBy = userId
        });

        sendResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "should send order to kitchen");

        // Act 4 - Verify order state
        using var getOrderResponse = await client.GetAsync(orderPath);

        getOrderResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        getOrderResponse.GetInt("status").Should().Be(1, "status should be Sent (1)");

        // Act 5 - Get totals to know exact amount
        using var totalsResponse = await client.GetAsync($"{orderPath}/totals");
        totalsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var balanceDue = totalsResponse.Root.GetProperty("balanceDue").GetDecimal();
        balanceDue.Should().BeGreaterThan(0);

        // Act 6 - Initiate cash payment for the exact balance
        using var paymentResponse = await client.PostAsync(paymentsPath, new
        {
            orderId,
            method = 0, // Cash
            amount = balanceDue,
            cashierId = userId
        });

        paymentResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            "should initiate payment");
        var paymentId = paymentResponse.GetId();
        paymentId.Should().NotBeNullOrEmpty();

        // Act 7 - Complete cash payment
        using var completeCashResponse = await client.PostAsync(
            $"{paymentsPath}/{paymentId}/complete-cash",
            new
            {
                amountTendered = balanceDue,
                tipAmount = 0m
            });

        completeCashResponse.IsSuccess.Should().BeTrue("should complete cash payment");

        // Act 8 - Close order
        using var closeResponse = await client.PostAsync($"{orderPath}/close", new
        {
            closedBy = userId
        });

        closeResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "should close order");

        // Assert - Final order state
        using var finalOrder = await client.GetAsync(orderPath);

        finalOrder.StatusCode.Should().Be(HttpStatusCode.OK);
        finalOrder.GetInt("status").Should().Be(4, "status should be Closed (4)");
    }

    [Fact]
    public async Task Order_creation_requires_spicedb_authorization()
    {
        // Arrange - user with NO SpiceDB relationships
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var client = new DarkVelocityClient(_fixture.HttpClient, _fixture.BaseUrl);
        client.Authenticate(userId, orgId, siteId);

        // Create org and site (these don't require SpiceDB)
        await client.PostAsync("/api/orgs", new
        {
            name = $"Auth Test Org {orgId:N}",
            slug = $"auth-{orgId:N}"
        });

        await client.PostAsync($"/api/orgs/{orgId}/sites", new
        {
            name = "Auth Test Site",
            code = "ATS",
            address = new
            {
                street = "1 Test Road",
                city = "London",
                state = "England",
                postalCode = "W1A 0AX",
                country = "GB"
            },
            timezone = "Europe/London",
            currency = "GBP"
        });

        // Act - Try to create order WITHOUT SpiceDB setup
        using var response = await client.PostAsync(
            $"/api/orgs/{orgId}/sites/{siteId}/orders",
            new
            {
                createdBy = userId,
                type = 0, // DineIn
                guestCount = 1
            });

        // Assert - Should be forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
