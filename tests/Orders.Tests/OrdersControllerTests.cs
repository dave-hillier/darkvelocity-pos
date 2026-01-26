using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Orders.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Orders.Tests;

public class OrdersControllerTests : IClassFixture<OrdersApiFixture>
{
    private readonly OrdersApiFixture _fixture;
    private readonly HttpClient _client;

    public OrdersControllerTests(OrdersApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsOrders()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/orders");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<OrderDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetById_ReturnsOrder()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/orders/{_fixture.TestOrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        order.Should().NotBeNull();
        order!.Id.Should().Be(_fixture.TestOrderId);
        order.Status.Should().Be("open");
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/orders/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_CreatesOrder()
    {
        var request = new CreateOrderRequest(
            UserId: _fixture.TestUserId,
            OrderType: "direct_sale",
            CustomerName: "Test Customer");

        var response = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/orders", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        order.Should().NotBeNull();
        order!.OrderType.Should().Be("direct_sale");
        order.Status.Should().Be("open");
        order.CustomerName.Should().Be("Test Customer");
        order.OrderNumber.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AddLine_AddsLineToOrder()
    {
        // First create a new order
        var createRequest = new CreateOrderRequest(UserId: _fixture.TestUserId);
        var createResponse = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/orders", createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>();

        // Add a line
        var lineRequest = new AddOrderLineRequest(
            MenuItemId: _fixture.TestMenuItemId,
            ItemName: "Test Burger",
            Quantity: 2,
            UnitPrice: 12.50m,
            TaxRate: 0.20m,
            Notes: "No onions");

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/orders/{order!.Id}/lines",
            lineRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var line = await response.Content.ReadFromJsonAsync<OrderLineDto>();
        line.Should().NotBeNull();
        line!.ItemName.Should().Be("Test Burger");
        line.Quantity.Should().Be(2);
        line.UnitPrice.Should().Be(12.50m);
        line.LineTotal.Should().Be(25.00m);
        line.Notes.Should().Be("No onions");
    }

    [Fact]
    public async Task AddLine_UpdatesOrderTotals()
    {
        // First create a new order
        var createRequest = new CreateOrderRequest(UserId: _fixture.TestUserId);
        var createResponse = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/orders", createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>();

        // Add a line
        var lineRequest = new AddOrderLineRequest(
            MenuItemId: _fixture.TestMenuItemId,
            ItemName: "Test Item",
            Quantity: 2,
            UnitPrice: 10.00m,
            TaxRate: 0.20m);

        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/orders/{order!.Id}/lines",
            lineRequest);

        // Get the updated order
        var getResponse = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/orders/{order.Id}");
        var updatedOrder = await getResponse.Content.ReadFromJsonAsync<OrderDto>();

        updatedOrder!.Subtotal.Should().Be(20.00m);
        updatedOrder.TaxTotal.Should().Be(4.00m);
        updatedOrder.GrandTotal.Should().Be(24.00m);
    }

    [Fact]
    public async Task UpdateLine_UpdatesQuantity()
    {
        // First create a new order with a line
        var createRequest = new CreateOrderRequest(UserId: _fixture.TestUserId);
        var createResponse = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/orders", createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>();

        var lineRequest = new AddOrderLineRequest(
            MenuItemId: _fixture.TestMenuItemId,
            ItemName: "Test Item",
            Quantity: 1,
            UnitPrice: 10.00m,
            TaxRate: 0.20m);
        var lineResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/orders/{order!.Id}/lines",
            lineRequest);
        var line = await lineResponse.Content.ReadFromJsonAsync<OrderLineDto>();

        // Update quantity
        var updateRequest = new UpdateOrderLineRequest(Quantity: 3);

        var response = await _client.PatchAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/orders/{order.Id}/lines/{line!.Id}",
            updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedLine = await response.Content.ReadFromJsonAsync<OrderLineDto>();
        updatedLine!.Quantity.Should().Be(3);
        updatedLine.LineTotal.Should().Be(30.00m);
    }

    [Fact]
    public async Task RemoveLine_RemovesLineFromOrder()
    {
        // First create a new order with a line
        var createRequest = new CreateOrderRequest(UserId: _fixture.TestUserId);
        var createResponse = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/orders", createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>();

        var lineRequest = new AddOrderLineRequest(
            MenuItemId: _fixture.TestMenuItemId,
            ItemName: "To Remove",
            Quantity: 1,
            UnitPrice: 10.00m,
            TaxRate: 0.20m);
        var lineResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/orders/{order!.Id}/lines",
            lineRequest);
        var line = await lineResponse.Content.ReadFromJsonAsync<OrderLineDto>();

        // Remove the line
        var response = await _client.DeleteAsync(
            $"/api/locations/{_fixture.TestLocationId}/orders/{order.Id}/lines/{line!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's removed
        var getResponse = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/orders/{order.Id}");
        var updatedOrder = await getResponse.Content.ReadFromJsonAsync<OrderDto>();
        updatedOrder!.Lines.Should().BeEmpty();
    }

    [Fact]
    public async Task Send_SendsOrder()
    {
        // First create a new order with a line
        var createRequest = new CreateOrderRequest(UserId: _fixture.TestUserId);
        var createResponse = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/orders", createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>();

        var lineRequest = new AddOrderLineRequest(
            MenuItemId: _fixture.TestMenuItemId,
            ItemName: "Test Item",
            Quantity: 1,
            UnitPrice: 10.00m,
            TaxRate: 0.20m);
        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/orders/{order!.Id}/lines",
            lineRequest);

        // Send the order
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/orders/{order.Id}/send",
            new SendOrderRequest());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var sentOrder = await response.Content.ReadFromJsonAsync<OrderDto>();
        sentOrder!.Status.Should().Be("sent");
        sentOrder.SentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Send_EmptyOrder_ReturnsBadRequest()
    {
        // Create an empty order
        var createRequest = new CreateOrderRequest(UserId: _fixture.TestUserId);
        var createResponse = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/orders", createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>();

        // Try to send without lines
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/orders/{order!.Id}/send",
            new SendOrderRequest());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Complete_CompletesOrder()
    {
        // First create a new order with a line
        var createRequest = new CreateOrderRequest(UserId: _fixture.TestUserId);
        var createResponse = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/orders", createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>();

        var lineRequest = new AddOrderLineRequest(
            MenuItemId: _fixture.TestMenuItemId,
            ItemName: "Test Item",
            Quantity: 1,
            UnitPrice: 10.00m,
            TaxRate: 0.20m);
        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/orders/{order!.Id}/lines",
            lineRequest);

        // Complete the order
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/orders/{order.Id}/complete",
            new CompleteOrderRequest());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var completedOrder = await response.Content.ReadFromJsonAsync<OrderDto>();
        completedOrder!.Status.Should().Be("completed");
        completedOrder.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Void_VoidsOrder()
    {
        // First create a new order
        var createRequest = new CreateOrderRequest(UserId: _fixture.TestUserId);
        var createResponse = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/orders", createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>();

        // Void the order
        var voidRequest = new VoidOrderRequest(
            UserId: _fixture.TestUserId,
            Reason: "Customer changed mind");

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/orders/{order!.Id}/void",
            voidRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var voidedOrder = await response.Content.ReadFromJsonAsync<OrderDto>();
        voidedOrder!.Status.Should().Be("voided");
    }

    [Fact]
    public async Task AddLine_ToNonOpenOrder_ReturnsBadRequest()
    {
        // First create and complete an order
        var createRequest = new CreateOrderRequest(UserId: _fixture.TestUserId);
        var createResponse = await _client.PostAsJsonAsync($"/api/locations/{_fixture.TestLocationId}/orders", createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>();

        var lineRequest = new AddOrderLineRequest(
            MenuItemId: _fixture.TestMenuItemId,
            ItemName: "Test Item",
            Quantity: 1,
            UnitPrice: 10.00m,
            TaxRate: 0.20m);
        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/orders/{order!.Id}/lines",
            lineRequest);

        await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/orders/{order.Id}/complete",
            new CompleteOrderRequest());

        // Try to add another line
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/orders/{order.Id}/lines",
            lineRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
