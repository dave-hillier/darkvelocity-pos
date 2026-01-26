using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Procurement.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Procurement.Tests;

public class DeliveriesControllerTests : IClassFixture<ProcurementApiFixture>
{
    private readonly ProcurementApiFixture _fixture;
    private readonly HttpClient _client;

    public DeliveriesControllerTests(ProcurementApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAll_ReturnsDeliveries()
    {
        var response = await _client.GetAsync("/api/deliveries");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<DeliveryDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_FiltersBySupplier()
    {
        var response = await _client.GetAsync($"/api/deliveries?supplierId={_fixture.TestSupplierId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<DeliveryDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().OnlyContain(d => d.SupplierId == _fixture.TestSupplierId);
    }

    [Fact]
    public async Task GetById_ReturnsDelivery()
    {
        var response = await _client.GetAsync($"/api/deliveries/{_fixture.TestDeliveryId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var delivery = await response.Content.ReadFromJsonAsync<DeliveryDto>();
        delivery.Should().NotBeNull();
        delivery!.Id.Should().Be(_fixture.TestDeliveryId);
        delivery.Status.Should().Be("pending");
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/deliveries/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_CreatesDelivery()
    {
        var request = new CreateDeliveryRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId,
            SupplierInvoiceNumber: "INV-12345",
            Notes: "Partial delivery");

        var response = await _client.PostAsJsonAsync("/api/deliveries", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var delivery = await response.Content.ReadFromJsonAsync<DeliveryDto>();
        delivery.Should().NotBeNull();
        delivery!.SupplierId.Should().Be(_fixture.TestSupplierId);
        delivery.Status.Should().Be("pending");
        delivery.DeliveryNumber.Should().StartWith("DEL-");
        delivery.SupplierInvoiceNumber.Should().Be("INV-12345");
    }

    [Fact]
    public async Task Create_InvalidSupplier_ReturnsBadRequest()
    {
        var request = new CreateDeliveryRequest(
            SupplierId: Guid.NewGuid(), // Non-existent
            LocationId: _fixture.TestLocationId);

        var response = await _client.PostAsJsonAsync("/api/deliveries", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddLine_AddsLineToDelivery()
    {
        // First create a new delivery
        var createRequest = new CreateDeliveryRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);
        var createResponse = await _client.PostAsJsonAsync("/api/deliveries", createRequest);
        var delivery = await createResponse.Content.ReadFromJsonAsync<DeliveryDto>();

        // Add a line
        var lineRequest = new AddDeliveryLineRequest(
            IngredientId: _fixture.TestIngredientId,
            IngredientName: "Beef Mince",
            QuantityReceived: 10m,
            UnitCost: 5.00m,
            BatchNumber: "BATCH-001",
            ExpiryDate: DateTime.UtcNow.AddDays(14));

        var response = await _client.PostAsJsonAsync(
            $"/api/deliveries/{delivery!.Id}/lines",
            lineRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var line = await response.Content.ReadFromJsonAsync<DeliveryLineDto>();
        line!.QuantityReceived.Should().Be(10m);
        line.UnitCost.Should().Be(5.00m);
        line.LineTotal.Should().Be(50m);
        line.BatchNumber.Should().Be("BATCH-001");
    }

    [Fact]
    public async Task AddLine_UpdatesDeliveryTotal()
    {
        // First create a new delivery
        var createRequest = new CreateDeliveryRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);
        var createResponse = await _client.PostAsJsonAsync("/api/deliveries", createRequest);
        var delivery = await createResponse.Content.ReadFromJsonAsync<DeliveryDto>();

        // Add lines
        var lineRequest = new AddDeliveryLineRequest(
            IngredientId: _fixture.TestIngredientId,
            IngredientName: "Test Item",
            QuantityReceived: 10m,
            UnitCost: 3.00m);

        await _client.PostAsJsonAsync($"/api/deliveries/{delivery!.Id}/lines", lineRequest);

        // Get the updated delivery
        var getResponse = await _client.GetAsync($"/api/deliveries/{delivery.Id}");
        var updatedDelivery = await getResponse.Content.ReadFromJsonAsync<DeliveryDto>();

        updatedDelivery!.TotalValue.Should().Be(30m);
    }

    [Fact]
    public async Task AddDiscrepancy_AddsDiscrepancyToDelivery()
    {
        // Create delivery with a line
        var createRequest = new CreateDeliveryRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);
        var createResponse = await _client.PostAsJsonAsync("/api/deliveries", createRequest);
        var delivery = await createResponse.Content.ReadFromJsonAsync<DeliveryDto>();

        var lineRequest = new AddDeliveryLineRequest(
            IngredientId: _fixture.TestIngredientId,
            IngredientName: "Test Item",
            QuantityReceived: 8m, // Expected 10
            UnitCost: 5.00m);
        var lineResponse = await _client.PostAsJsonAsync($"/api/deliveries/{delivery!.Id}/lines", lineRequest);
        var line = await lineResponse.Content.ReadFromJsonAsync<DeliveryLineDto>();

        // Add discrepancy
        var discrepancyRequest = new AddDeliveryDiscrepancyRequest(
            DeliveryLineId: line!.Id,
            DiscrepancyType: "quantity_short",
            QuantityAffected: 2m,
            Description: "2kg missing from delivery");

        var response = await _client.PostAsJsonAsync(
            $"/api/deliveries/{delivery.Id}/discrepancies",
            discrepancyRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var discrepancy = await response.Content.ReadFromJsonAsync<DeliveryDiscrepancyDto>();
        discrepancy!.DiscrepancyType.Should().Be("quantity_short");
        discrepancy.QuantityAffected.Should().Be(2m);
        discrepancy.ActionTaken.Should().Be("pending");
    }

    [Fact]
    public async Task AddDiscrepancy_SetsHasDiscrepanciesFlag()
    {
        // Create delivery with a line
        var createRequest = new CreateDeliveryRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);
        var createResponse = await _client.PostAsJsonAsync("/api/deliveries", createRequest);
        var delivery = await createResponse.Content.ReadFromJsonAsync<DeliveryDto>();

        var lineRequest = new AddDeliveryLineRequest(
            IngredientId: _fixture.TestIngredientId,
            IngredientName: "Test Item",
            QuantityReceived: 10m,
            UnitCost: 5.00m);
        var lineResponse = await _client.PostAsJsonAsync($"/api/deliveries/{delivery!.Id}/lines", lineRequest);
        var line = await lineResponse.Content.ReadFromJsonAsync<DeliveryLineDto>();

        // Add discrepancy
        var discrepancyRequest = new AddDeliveryDiscrepancyRequest(
            DeliveryLineId: line!.Id,
            DiscrepancyType: "quality_issue",
            Description: "Product not fresh");

        await _client.PostAsJsonAsync($"/api/deliveries/{delivery.Id}/discrepancies", discrepancyRequest);

        // Check delivery has discrepancies flag set
        var getResponse = await _client.GetAsync($"/api/deliveries/{delivery.Id}");
        var updatedDelivery = await getResponse.Content.ReadFromJsonAsync<DeliveryDto>();
        updatedDelivery!.HasDiscrepancies.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveDiscrepancy_ResolvesDiscrepancy()
    {
        // Create delivery with discrepancy
        var createRequest = new CreateDeliveryRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);
        var createResponse = await _client.PostAsJsonAsync("/api/deliveries", createRequest);
        var delivery = await createResponse.Content.ReadFromJsonAsync<DeliveryDto>();

        var lineRequest = new AddDeliveryLineRequest(
            IngredientId: _fixture.TestIngredientId,
            IngredientName: "Test Item",
            QuantityReceived: 10m,
            UnitCost: 5.00m);
        var lineResponse = await _client.PostAsJsonAsync($"/api/deliveries/{delivery!.Id}/lines", lineRequest);
        var line = await lineResponse.Content.ReadFromJsonAsync<DeliveryLineDto>();

        var discrepancyRequest = new AddDeliveryDiscrepancyRequest(
            DeliveryLineId: line!.Id,
            DiscrepancyType: "damaged",
            QuantityAffected: 1m);
        var discrepancyResponse = await _client.PostAsJsonAsync(
            $"/api/deliveries/{delivery.Id}/discrepancies",
            discrepancyRequest);
        var discrepancy = await discrepancyResponse.Content.ReadFromJsonAsync<DeliveryDiscrepancyDto>();

        // Resolve it
        var resolveRequest = new ResolveDiscrepancyRequest(
            ActionTaken: "credit_requested",
            ResolutionNotes: "Supplier agreed to credit");

        var response = await _client.PostAsJsonAsync(
            $"/api/deliveries/{delivery.Id}/discrepancies/{discrepancy!.Id}/resolve",
            resolveRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resolved = await response.Content.ReadFromJsonAsync<DeliveryDiscrepancyDto>();
        resolved!.ActionTaken.Should().Be("credit_requested");
        resolved.ResolvedAt.Should().NotBeNull();
        resolved.ResolutionNotes.Should().Be("Supplier agreed to credit");
    }

    [Fact]
    public async Task Accept_AcceptsDelivery()
    {
        // Create delivery with lines
        var createRequest = new CreateDeliveryRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);
        var createResponse = await _client.PostAsJsonAsync("/api/deliveries", createRequest);
        var delivery = await createResponse.Content.ReadFromJsonAsync<DeliveryDto>();

        var lineRequest = new AddDeliveryLineRequest(
            IngredientId: _fixture.TestIngredientId,
            IngredientName: "Test Item",
            QuantityReceived: 10m,
            UnitCost: 5.00m);
        await _client.PostAsJsonAsync($"/api/deliveries/{delivery!.Id}/lines", lineRequest);

        // Accept
        var response = await _client.PostAsJsonAsync(
            $"/api/deliveries/{delivery.Id}/accept",
            new AcceptDeliveryRequest());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var accepted = await response.Content.ReadFromJsonAsync<DeliveryDto>();
        accepted!.Status.Should().Be("accepted");
        accepted.AcceptedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Accept_EmptyDelivery_ReturnsBadRequest()
    {
        // Create empty delivery
        var createRequest = new CreateDeliveryRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);
        var createResponse = await _client.PostAsJsonAsync("/api/deliveries", createRequest);
        var delivery = await createResponse.Content.ReadFromJsonAsync<DeliveryDto>();

        // Try to accept without lines
        var response = await _client.PostAsJsonAsync(
            $"/api/deliveries/{delivery!.Id}/accept",
            new AcceptDeliveryRequest());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Accept_WithUnresolvedDiscrepancies_ReturnsBadRequest()
    {
        // Create delivery with unresolved discrepancy
        var createRequest = new CreateDeliveryRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);
        var createResponse = await _client.PostAsJsonAsync("/api/deliveries", createRequest);
        var delivery = await createResponse.Content.ReadFromJsonAsync<DeliveryDto>();

        var lineRequest = new AddDeliveryLineRequest(
            IngredientId: _fixture.TestIngredientId,
            IngredientName: "Test Item",
            QuantityReceived: 10m,
            UnitCost: 5.00m);
        var lineResponse = await _client.PostAsJsonAsync($"/api/deliveries/{delivery!.Id}/lines", lineRequest);
        var line = await lineResponse.Content.ReadFromJsonAsync<DeliveryLineDto>();

        var discrepancyRequest = new AddDeliveryDiscrepancyRequest(
            DeliveryLineId: line!.Id,
            DiscrepancyType: "damaged",
            QuantityAffected: 1m);
        await _client.PostAsJsonAsync($"/api/deliveries/{delivery.Id}/discrepancies", discrepancyRequest);

        // Try to accept with unresolved discrepancy
        var response = await _client.PostAsJsonAsync(
            $"/api/deliveries/{delivery.Id}/accept",
            new AcceptDeliveryRequest());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reject_RejectsDelivery()
    {
        // Create delivery
        var createRequest = new CreateDeliveryRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);
        var createResponse = await _client.PostAsJsonAsync("/api/deliveries", createRequest);
        var delivery = await createResponse.Content.ReadFromJsonAsync<DeliveryDto>();

        // Reject
        var rejectRequest = new RejectDeliveryRequest(
            Reason: "All items expired");

        var response = await _client.PostAsJsonAsync(
            $"/api/deliveries/{delivery!.Id}/reject",
            rejectRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var rejected = await response.Content.ReadFromJsonAsync<DeliveryDto>();
        rejected!.Status.Should().Be("rejected");
    }

    [Fact]
    public async Task AddLine_ToAcceptedDelivery_ReturnsBadRequest()
    {
        // Create and accept a delivery
        var createRequest = new CreateDeliveryRequest(
            SupplierId: _fixture.TestSupplierId,
            LocationId: _fixture.TestLocationId);
        var createResponse = await _client.PostAsJsonAsync("/api/deliveries", createRequest);
        var delivery = await createResponse.Content.ReadFromJsonAsync<DeliveryDto>();

        var lineRequest = new AddDeliveryLineRequest(
            IngredientId: _fixture.TestIngredientId,
            IngredientName: "Test Item",
            QuantityReceived: 10m,
            UnitCost: 5.00m);
        await _client.PostAsJsonAsync($"/api/deliveries/{delivery!.Id}/lines", lineRequest);

        await _client.PostAsJsonAsync($"/api/deliveries/{delivery.Id}/accept", new AcceptDeliveryRequest());

        // Try to add another line
        var response = await _client.PostAsJsonAsync($"/api/deliveries/{delivery.Id}/lines", lineRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
