using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Procurement.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Procurement.Tests;

public class PurchaseOrdersControllerTests : IClassFixture<ProcurementApiFixture>
{
    private readonly ProcurementApiFixture _fixture;
    private readonly HttpClient _client;

    public PurchaseOrdersControllerTests(ProcurementApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsPurchaseOrders()
    {
        var response = await _client.GetAsync("/api/purchase-orders");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<PurchaseOrderDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_FiltersByLocation()
    {
        var response = await _client.GetAsync($"/api/purchase-orders?locationId={_fixture.TestLocationId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<PurchaseOrderDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().OnlyContain(po => po.LocationId == _fixture.TestLocationId);
    }

    [Fact]
    public async Task GetById_ReturnsPurchaseOrder()
    {
        var response = await _client.GetAsync($"/api/purchase-orders/{_fixture.TestPurchaseOrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var order = await response.Content.ReadFromJsonAsync<PurchaseOrderDto>();
        order.Should().NotBeNull();
        order!.Id.Should().Be(_fixture.TestPurchaseOrderId);
        order.Status.Should().Be("draft");
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/purchase-orders/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_CreatesPurchaseOrder()
    {
        var request = new CreatePurchaseOrderRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId,
            ExpectedDeliveryDate: DateTime.UtcNow.AddDays(3),
            Notes: "Urgent order");

        var response = await _client.PostAsJsonAsync("/api/purchase-orders", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var order = await response.Content.ReadFromJsonAsync<PurchaseOrderDto>();
        order.Should().NotBeNull();
        order!.SupplierId.Should().Be(_fixture.TestSupplierId);
        order.Status.Should().Be("draft");
        order.OrderNumber.Should().StartWith("PO-");
        order.Notes.Should().Be("Urgent order");
    }

    [Fact]
    public async Task Create_InvalidSupplier_ReturnsBadRequest()
    {
        var request = new CreatePurchaseOrderRequest(
            SupplierId: Guid.NewGuid(), // Non-existent
            LocationId: _fixture.TestLocationId);

        var response = await _client.PostAsJsonAsync("/api/purchase-orders", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddLine_AddsLineToPurchaseOrder()
    {
        // First create a new PO
        var createRequest = new CreatePurchaseOrderRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);
        var createResponse = await _client.PostAsJsonAsync("/api/purchase-orders", createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>();

        // Add a line
        var lineRequest = new AddPurchaseOrderLineRequest(
            IngredientId: _fixture.TestIngredientId,
            IngredientName: "Beef Mince",
            QuantityOrdered: 20m,
            UnitPrice: 5.00m,
            Notes: "Need extra stock");

        var response = await _client.PostAsJsonAsync(
            $"/api/purchase-orders/{order!.Id}/lines",
            lineRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var line = await response.Content.ReadFromJsonAsync<PurchaseOrderLineDto>();
        line!.QuantityOrdered.Should().Be(20m);
        line.UnitPrice.Should().Be(5.00m);
        line.LineTotal.Should().Be(100m);
    }

    [Fact]
    public async Task AddLine_UpdatesOrderTotal()
    {
        // First create a new PO
        var createRequest = new CreatePurchaseOrderRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);
        var createResponse = await _client.PostAsJsonAsync("/api/purchase-orders", createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>();

        // Add a line
        var lineRequest = new AddPurchaseOrderLineRequest(
            IngredientId: _fixture.TestIngredientId,
            IngredientName: "Test Item",
            QuantityOrdered: 10m,
            UnitPrice: 3.00m);

        await _client.PostAsJsonAsync($"/api/purchase-orders/{order!.Id}/lines", lineRequest);

        // Get the updated order
        var getResponse = await _client.GetAsync($"/api/purchase-orders/{order.Id}");
        var updatedOrder = await getResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>();

        updatedOrder!.OrderTotal.Should().Be(30m);
    }

    [Fact]
    public async Task AddLine_ToNonDraftOrder_ReturnsBadRequest()
    {
        // First create and submit a PO
        var createRequest = new CreatePurchaseOrderRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);
        var createResponse = await _client.PostAsJsonAsync("/api/purchase-orders", createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>();

        // Add initial line
        var lineRequest = new AddPurchaseOrderLineRequest(
            IngredientId: _fixture.TestIngredientId,
            IngredientName: "Test Item",
            QuantityOrdered: 10m,
            UnitPrice: 5.00m);
        await _client.PostAsJsonAsync($"/api/purchase-orders/{order!.Id}/lines", lineRequest);

        // Submit
        await _client.PostAsJsonAsync($"/api/purchase-orders/{order.Id}/submit", new SubmitPurchaseOrderRequest());

        // Try to add another line
        var response = await _client.PostAsJsonAsync($"/api/purchase-orders/{order.Id}/lines", lineRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateLine_UpdatesLineValues()
    {
        // First create a PO with a line
        var createRequest = new CreatePurchaseOrderRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);
        var createResponse = await _client.PostAsJsonAsync("/api/purchase-orders", createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>();

        var lineRequest = new AddPurchaseOrderLineRequest(
            IngredientId: _fixture.TestIngredientId,
            IngredientName: "Test Item",
            QuantityOrdered: 10m,
            UnitPrice: 5.00m);
        var lineResponse = await _client.PostAsJsonAsync($"/api/purchase-orders/{order!.Id}/lines", lineRequest);
        var line = await lineResponse.Content.ReadFromJsonAsync<PurchaseOrderLineDto>();

        // Update the line
        var updateRequest = new UpdatePurchaseOrderLineRequest(
            QuantityOrdered: 15m,
            UnitPrice: 4.50m);

        var response = await _client.PatchAsJsonAsync(
            $"/api/purchase-orders/{order.Id}/lines/{line!.Id}",
            updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedLine = await response.Content.ReadFromJsonAsync<PurchaseOrderLineDto>();
        updatedLine!.QuantityOrdered.Should().Be(15m);
        updatedLine.UnitPrice.Should().Be(4.50m);
        updatedLine.LineTotal.Should().Be(67.50m);
    }

    [Fact]
    public async Task RemoveLine_RemovesLineFromOrder()
    {
        // First create a PO with a line
        var createRequest = new CreatePurchaseOrderRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);
        var createResponse = await _client.PostAsJsonAsync("/api/purchase-orders", createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>();

        var lineRequest = new AddPurchaseOrderLineRequest(
            IngredientId: _fixture.TestIngredientId,
            IngredientName: "Test Item",
            QuantityOrdered: 10m,
            UnitPrice: 5.00m);
        var lineResponse = await _client.PostAsJsonAsync($"/api/purchase-orders/{order!.Id}/lines", lineRequest);
        var line = await lineResponse.Content.ReadFromJsonAsync<PurchaseOrderLineDto>();

        // Remove the line
        var response = await _client.DeleteAsync($"/api/purchase-orders/{order.Id}/lines/{line!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify order total is updated
        var getResponse = await _client.GetAsync($"/api/purchase-orders/{order.Id}");
        var updatedOrder = await getResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>();
        updatedOrder!.OrderTotal.Should().Be(0m);
    }

    [Fact]
    public async Task Submit_SubmitsPurchaseOrder()
    {
        // First create a PO with a line
        var createRequest = new CreatePurchaseOrderRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);
        var createResponse = await _client.PostAsJsonAsync("/api/purchase-orders", createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>();

        var lineRequest = new AddPurchaseOrderLineRequest(
            IngredientId: _fixture.TestIngredientId,
            IngredientName: "Test Item",
            QuantityOrdered: 10m,
            UnitPrice: 5.00m);
        await _client.PostAsJsonAsync($"/api/purchase-orders/{order!.Id}/lines", lineRequest);

        // Submit
        var response = await _client.PostAsJsonAsync(
            $"/api/purchase-orders/{order.Id}/submit",
            new SubmitPurchaseOrderRequest());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var submitted = await response.Content.ReadFromJsonAsync<PurchaseOrderDto>();
        submitted!.Status.Should().Be("submitted");
        submitted.SubmittedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Submit_EmptyOrder_ReturnsBadRequest()
    {
        // Create empty PO
        var createRequest = new CreatePurchaseOrderRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);
        var createResponse = await _client.PostAsJsonAsync("/api/purchase-orders", createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>();

        // Try to submit without lines
        var response = await _client.PostAsJsonAsync(
            $"/api/purchase-orders/{order!.Id}/submit",
            new SubmitPurchaseOrderRequest());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cancel_CancelsPurchaseOrder()
    {
        // Create and submit a PO
        var createRequest = new CreatePurchaseOrderRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);
        var createResponse = await _client.PostAsJsonAsync("/api/purchase-orders", createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>();

        var lineRequest = new AddPurchaseOrderLineRequest(
            IngredientId: _fixture.TestIngredientId,
            IngredientName: "Test Item",
            QuantityOrdered: 10m,
            UnitPrice: 5.00m);
        await _client.PostAsJsonAsync($"/api/purchase-orders/{order!.Id}/lines", lineRequest);
        await _client.PostAsJsonAsync($"/api/purchase-orders/{order.Id}/submit", new SubmitPurchaseOrderRequest());

        // Cancel
        var cancelRequest = new CancelPurchaseOrderRequest(Reason: "Supplier out of stock");

        var response = await _client.PostAsJsonAsync(
            $"/api/purchase-orders/{order.Id}/cancel",
            cancelRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var cancelled = await response.Content.ReadFromJsonAsync<PurchaseOrderDto>();
        cancelled!.Status.Should().Be("cancelled");
    }
}
